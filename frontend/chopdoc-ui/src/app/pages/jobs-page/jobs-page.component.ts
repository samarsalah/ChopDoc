import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { JobDetail, JobSummary } from '../../models/job.models';

@Component({
  selector: 'app-jobs-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './jobs-page.component.html',
  styleUrl: './jobs-page.component.scss'
})
export class JobsPageComponent implements OnInit {
  jobs: JobSummary[] = [];
  selected: JobDetail | null = null;

  selectedFile: File | null = null;
  outputFormat = 'Html';
  sizeLimitMb: number | null = 2;

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
        this.selectedFile = null;
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
    return new Date(value).toLocaleString();
  }
}
