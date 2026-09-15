export interface JobSummary {
  id: string;
  originalFileName: string;
  outputFormat: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  errorCode?: string | null;
  errorMessage?: string | null;
  partCount: number;
}

export interface JobPart {
  id: string;
  partNumber: number;
  totalParts: number;
  sequenceLabel: string;
  fileName: string;
  sizeBytes: number;
}

export interface JobHistory {
  id: string;
  status: string;
  message: string;
  occurredAtUtc: string;
}

export interface JobDetail {
  id: string;
  originalFileName: string;
  outputFormat: string;
  sizeLimitBytes: number;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  completedAtUtc?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  parts: JobPart[];
  history: JobHistory[];
}

export interface ApiError {
  errorCode?: string;
  message?: string;
}
