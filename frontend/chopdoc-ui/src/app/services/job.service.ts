import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { ApiError, JobDetail, JobSummary } from '../models/job.models';

@Injectable({ providedIn: 'root' })
export class JobService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private readonly http: HttpClient) {}

  list(): Observable<JobSummary[]> {
    return this.http.get<JobSummary[]>(`${this.baseUrl}/jobs`).pipe(
      catchError(err => this.handleError(err))
    );
  }

  getById(id: string): Observable<JobDetail> {
    return this.http.get<JobDetail>(`${this.baseUrl}/jobs/${id}`).pipe(
      catchError(err => this.handleError(err))
    );
  }

  submit(file: File, outputFormat: string, sizeLimitMb?: number | null): Observable<JobDetail> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('outputFormat', outputFormat);
    if (sizeLimitMb != null && !Number.isNaN(sizeLimitMb)) {
      form.append('sizeLimitMb', String(sizeLimitMb));
    }

    return this.http.post<JobDetail>(`${this.baseUrl}/jobs`, form).pipe(
      catchError(err => this.handleError(err))
    );
  }

  partDownloadUrl(jobId: string, partId: string): string {
    return `${this.baseUrl}/jobs/${jobId}/parts/${partId}`;
  }

  private handleError(error: HttpErrorResponse) {
    const body = error.error as ApiError | string | null;
    let message = 'Request failed.';

    if (typeof body === 'string' && body.trim()) {
      message = body;
    } else if (body && typeof body === 'object') {
      message = body.message || body.errorCode || message;
    } else if (error.message) {
      message = error.message;
    }

    return throwError(() => new Error(message));
  }
}
