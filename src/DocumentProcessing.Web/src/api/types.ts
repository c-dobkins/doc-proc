export interface DocumentDto {
  id: string;
  fileName: string;
  fileType: string;
  status: DocumentStatus;
  fileSizeBytes: number;
  source: string | null;
  templateId: string | null;
  confidenceScore: number | null;
  uploadedAt: string;
  processedAt: string | null;
}

export interface DocumentDetailDto {
  id: string;
  fileName: string;
  fileType: string;
  status: DocumentStatus;
  fileSizeBytes: number;
  source: string | null;
  templateId: string | null;
  processingResult: ProcessingResultDto | null;
  uploadedAt: string;
  processedAt: string | null;
}

export interface ProcessingResultDto {
  id: string;
  extractedFields: DocumentFieldDto[];
  confidenceScore: number;
  rawOcrText: string | null;
  aiProvider: string | null;
  metrics: ProcessingMetricsDto;
  createdAt: string;
}

export interface DocumentFieldDto {
  id: string;
  name: string;
  value: string | null;
  confidence: number;
  dataType: string;
}

export interface ProcessingMetricsDto {
  ocrDurationMs: number;
  aiDurationMs: number;
  totalDurationMs: number;
  tokenCount: number;
  estimatedCost: number | null;
}

export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UploadDocumentResponse {
  documentId: string;
  fileName: string;
  status: string;
}

export interface UpdateFieldsRequest {
  fields: FieldUpdateDto[];
}

export interface FieldUpdateDto {
  name: string;
  value: string | null;
}

export interface TemplateDto {
  id: string;
  name: string;
  description: string | null;
  fields: TemplateFieldDto[];
  promptTemplate: string;
  version: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface TemplateFieldDto {
  name: string;
  dataType: string;
  isRequired: boolean;
  description: string | null;
}

export interface CreateTemplateRequest {
  name: string;
  description: string | null;
  fields: TemplateFieldDto[];
  promptTemplate: string;
}

export interface UpdateTemplateRequest {
  name: string;
  description: string | null;
  fields: TemplateFieldDto[];
  promptTemplate: string;
}

export interface DashboardMetricsDto {
  totalDocuments: number;
  uploaded: number;
  processing: number;
  completed: number;
  failed: number;
  needsReview: number;
  approved: number;
}

export interface AuthStatusDto {
  writesRequireKey: boolean;
  canWrite: boolean;
}

export type DocumentStatus =
  | 'Uploaded'
  | 'Processing'
  | 'Completed'
  | 'Failed'
  | 'NeedsReview'
  | 'Approved';

/** Payload of the SignalR "StatusUpdated" message (server: StatusUpdatedMessage). */
export interface StatusUpdatedMessage {
  documentId: string;
  status: string;
}
