import type {
  CreateTemplateRequest,
  DashboardMetricsDto,
  DocumentDetailDto,
  DocumentDto,
  PagedResponse,
  TemplateDto,
  UpdateFieldsRequest,
  UpdateTemplateRequest,
  UploadDocumentResponse,
  AuthStatusDto,
} from './types';
import { ACCESS_KEY_HEADER, getAccessKey } from './accessKey';

const BASE = '/api';

/** fetch that attaches the stored access key, if any, to every API call. */
export function apiFetch(url: string, init: RequestInit = {}): Promise<Response> {
  const key = getAccessKey();
  if (!key) return fetch(url, init);

  const headers = new Headers(init.headers);
  headers.set(ACCESS_KEY_HEADER, key);
  return fetch(url, { ...init, headers });
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body: unknown = await res.json().catch(() => null);
    // Minimal API returns either ProblemDetails ({ title }) or a plain JSON string for 400s
    const message =
      typeof body === 'string' && body
        ? body
        : ((body as { title?: string } | null)?.title ?? `Request failed (${res.status})`);
    throw new ApiError(message, res.status);
  }
  // 202/204 responses (e.g. reprocess, approve) may have no body
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

// --- Documents ---

export async function uploadDocument(
  file: File,
  templateId?: string,
): Promise<UploadDocumentResponse> {
  const form = new FormData();
  form.append('file', file);
  if (templateId) form.append('templateId', templateId);

  const res = await apiFetch(`${BASE}/documents/upload`, {
    method: 'POST',
    body: form,
  });
  return handleResponse<UploadDocumentResponse>(res);
}

export async function getDocuments(
  page: number,
  pageSize: number,
  status?: string,
): Promise<PagedResponse<DocumentDto>> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (status) params.set('status', status);

  const res = await apiFetch(`${BASE}/documents?${params}`);
  return handleResponse<PagedResponse<DocumentDto>>(res);
}

export async function getDocumentById(
  id: string,
): Promise<DocumentDetailDto> {
  const res = await apiFetch(`${BASE}/documents/${encodeURIComponent(id)}`);
  return handleResponse<DocumentDetailDto>(res);
}

export async function updateFields(
  id: string,
  request: UpdateFieldsRequest,
): Promise<DocumentDetailDto> {
  const res = await apiFetch(`${BASE}/documents/${encodeURIComponent(id)}/fields`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return handleResponse<DocumentDetailDto>(res);
}

export async function approveDocument(id: string): Promise<void> {
  const res = await apiFetch(`${BASE}/documents/${encodeURIComponent(id)}/approve`, {
    method: 'POST',
  });
  return handleResponse<void>(res);
}

export async function reprocessDocument(id: string): Promise<void> {
  const res = await apiFetch(`${BASE}/documents/${encodeURIComponent(id)}/reprocess`, {
    method: 'POST',
  });
  return handleResponse<void>(res);
}

export function getDocumentDownloadUrl(id: string): string {
  return `${BASE}/documents/${encodeURIComponent(id)}/download`;
}

// --- Templates ---

export async function getTemplates(): Promise<TemplateDto[]> {
  const res = await apiFetch(`${BASE}/templates`);
  return handleResponse<TemplateDto[]>(res);
}

export async function createTemplate(
  request: CreateTemplateRequest,
): Promise<TemplateDto> {
  const res = await apiFetch(`${BASE}/templates`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return handleResponse<TemplateDto>(res);
}

export async function updateTemplate(
  id: string,
  request: UpdateTemplateRequest,
): Promise<TemplateDto> {
  const res = await apiFetch(`${BASE}/templates/${encodeURIComponent(id)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return handleResponse<TemplateDto>(res);
}

export async function deleteTemplate(id: string): Promise<void> {
  const res = await apiFetch(`${BASE}/templates/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  });
  return handleResponse<void>(res);
}

// --- Metrics ---

export async function getDashboardMetrics(): Promise<DashboardMetricsDto> {
  const res = await apiFetch(`${BASE}/metrics/dashboard`);
  return handleResponse<DashboardMetricsDto>(res);
}

// --- Auth ---

export async function getAuthStatus(): Promise<AuthStatusDto> {
  const res = await apiFetch(`${BASE}/auth/status`);
  return handleResponse<AuthStatusDto>(res);
}
