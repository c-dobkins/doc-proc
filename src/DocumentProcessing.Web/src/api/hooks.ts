import {
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query';
import type { QueryKey } from '@tanstack/react-query';
import * as api from './client';
import type {
  CreateTemplateRequest,
  UpdateFieldsRequest,
  UpdateTemplateRequest,
} from './types';

function useApiMutation<TArgs, TResult>(
  mutationFn: (args: TArgs) => Promise<TResult>,
  invalidateKeys: QueryKey[],
) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () =>
      invalidateKeys.forEach((key) => qc.invalidateQueries({ queryKey: key })),
  });
}

// --- Documents ---

export function useDocuments(page: number, pageSize: number, status?: string) {
  return useQuery({
    queryKey: ['documents', page, pageSize, status],
    queryFn: () => api.getDocuments(page, pageSize, status),
  });
}

export function useDocument(id: string) {
  return useQuery({
    queryKey: ['document', id],
    queryFn: () => api.getDocumentById(id),
    enabled: !!id,
  });
}

export function useUploadDocument() {
  return useApiMutation(
    ({ file, templateId }: { file: File; templateId?: string }) =>
      api.uploadDocument(file, templateId),
    [['documents']],
  );
}

export function useUpdateFields(documentId: string) {
  return useApiMutation(
    (request: UpdateFieldsRequest) => api.updateFields(documentId, request),
    [['document', documentId], ['documents']],
  );
}

export function useApproveDocument() {
  return useApiMutation(
    (id: string) => api.approveDocument(id),
    [['documents'], ['dashboard']],
  );
}

export function useReprocessDocument() {
  return useApiMutation(
    (id: string) => api.reprocessDocument(id),
    [['documents']],
  );
}

// --- Templates ---

export function useTemplates() {
  return useQuery({
    queryKey: ['templates'],
    queryFn: api.getTemplates,
  });
}

export function useCreateTemplate() {
  return useApiMutation(
    (request: CreateTemplateRequest) => api.createTemplate(request),
    [['templates']],
  );
}

export function useUpdateTemplate(id: string) {
  return useApiMutation(
    (request: UpdateTemplateRequest) => api.updateTemplate(id, request),
    [['templates']],
  );
}

export function useDeleteTemplate() {
  return useApiMutation(
    (id: string) => api.deleteTemplate(id),
    [['templates']],
  );
}

// --- Metrics ---

export function useDashboardMetrics() {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: api.getDashboardMetrics,
    refetchInterval: 10_000,
  });
}

// --- Auth ---

export function useAuthStatus() {
  return useQuery({
    queryKey: ['auth-status'],
    queryFn: api.getAuthStatus,
    staleTime: Infinity,
  });
}
