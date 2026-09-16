import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { JobDetail, JobSummary } from '../../models/job.models';

interface PipelineStep {
  key: string;
  label: string;
  hint: string;
}

@Component({
  selector: 'app-jobs-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './jobs-page.component.html',
  styleUrl: './jobs-page.component.scss'
})
export class JobsPageComponent implements OnInit {
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  jobs: JobSummary[] = [];
  selected: JobDetail | null = null;

  selectedFile: File | null = null;
  outputFormat = 'Html';
  sizeLimitMb: number | null = 2;

  readonly outputFormats = [
    { value: 'Html', label: 'HTML' },
    { value: 'Docx', label: 'DOCX' }
  ];

  /** Stages the backend runs for every job (shown in the UI pipeline). */
  readonly pipelineSteps: PipelineStep[] = [
    { key: 'Queued', label: 'Queued', hint: 'Job accepted and saved' },
    { key: 'Converting', label: 'Convert', hint: 'PDF → HTML intermediate' },
    { key: 'Splitting', label: 'Split', hint: 'Sized in the requested output format' },
    { key: 'Validating', label: 'Validate', hint: 'Exported parts complete and within limit' },
    { key: 'Completed', label: 'Deliver', hint: 'Parts written to storage' }
  ];

  submitting = false;
  loadingList = false;
  loadingDetail = false;
  errorMessage = '';
  successMessage = '';

  constructor(private readonly jobService: JobService) {}

  ngOnInit(): void {
    this.refreshList();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.errorMessage = '';
  }

  clearFileInput(): void {
    this.selectedFile = null;
    if (this.fileInput?.nativeElement) {
      this.fileInput.nativeElement.value = '';
    }
  }

  submit(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (!this.selectedFile) {
      this.errorMessage = 'Choose a PDF file first.';
      return;
    }

    this.submitting = true;
    this.jobService.submit(this.selectedFile, this.outputFormat, this.sizeLimitMb).subscribe({
      next: detail => {
        this.submitting = false;
        this.successMessage = `Job ${detail.status}: ${detail.originalFileName}`;
        this.selected = detail;
        this.clearFileInput();
        this.refreshList();
      },
      error: err => {
        this.submitting = false;
        this.errorMessage = err.message || 'Submit failed.';
      }
    });
  }

  refreshList(): void {
    this.loadingList = true;
    this.jobService.list().subscribe({
      next: jobs => {
        this.jobs = jobs;
        this.loadingList = false;
      },
      error: err => {
        this.loadingList = false;
        this.errorMessage = err.message || 'Could not load jobs.';
      }
    });
  }

  openJob(job: JobSummary): void {
    this.loadingDetail = true;
    this.errorMessage = '';
    this.jobService.getById(job.id).subscribe({
      next: detail => {
        this.selected = detail;
        this.loadingDetail = false;
      },
      error: err => {
        this.loadingDetail = false;
        this.errorMessage = err.message || 'Could not load job detail.';
      }
    });
  }

  downloadPart(partId: string): void {
    if (!this.selected) return;
    window.open(this.jobService.partDownloadUrl(this.selected.id, partId), '_blank');
  }

  /** Visual state for a pipeline step based on the selected job's history. */
  stepState(stepKey: string): 'done' | 'current' | 'todo' | 'failed' | 'review' {
    if (this.submitting) {
      return stepKey === 'Converting' || stepKey === 'Queued' ? 'current' : 'todo';
    }

    if (!this.selected) return 'todo';

    const seen = new Set(this.selected.history.map(h => h.status));
    const status = this.selected.status;

    if (stepKey === 'Completed') {
      if (status === 'Completed') return 'done';
      if (status === 'Failed') return 'failed';
      if (status === 'NeedsReview') return 'review';
      return 'todo';
    }

    if (seen.has(stepKey)) return 'done';
    if (status === stepKey) return 'current';
    return 'todo';
  }

  statusClass(status: string): string {
    return `status status-${(status || '').toLowerCase()}`;
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '—';

    // API timestamps are UTC. A missing offset must not be read as local time.
    const hasOffset = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value);
    const date = new Date(hasOffset ? value : `${value}Z`);
    if (Number.isNaN(date.getTime())) return value;

    return date.toLocaleString();
  }
}
