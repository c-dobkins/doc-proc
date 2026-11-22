import { lazy, Suspense, useState } from 'react';
import { useParams, useNavigate } from 'react-router';
import {
  useDocument,
  useUpdateFields,
  useApproveDocument,
  useReprocessDocument,
} from '../api/hooks';
import { useDocumentStatus } from '../hooks/useDocumentStatus';
import type { DocumentDetailDto, DocumentFieldDto } from '../api/types';
import FieldEditor from '../components/FieldEditor';
import StatusBadge from '../components/StatusBadge';
import ConfidenceBadge from '../components/ConfidenceBadge';
import PageState from '../components/PageState';

// pdf.js is large; load the viewer only when a document is actually opened
const PdfViewer = lazy(() => import('../components/PdfViewer'));

export default function DocumentReview() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: doc, isLoading, error } = useDocument(id!);
  const updateFields = useUpdateFields(id!);
  const approve = useApproveDocument();
  const reprocess = useReprocessDocument();
  const [selectedField, setSelectedField] = useState<DocumentFieldDto | null>(null);

  useDocumentStatus(id);

  return (
    <PageState
      isLoading={isLoading}
      error={error}
      data={doc}
      loadingText="Loading document…"
      notFoundText="Document not found."
    >
      {(doc: DocumentDetailDto) => {
        const result = doc.processingResult;

        function handleFieldSave(name: string, value: string | null) {
          updateFields.mutate({ fields: [{ name, value }] });
        }

        async function handleApprove() {
          await approve.mutateAsync(doc.id);
          navigate('/documents');
        }

        async function handleReprocess() {
          await reprocess.mutateAsync(doc.id);
        }

        return (
          <div>
            <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
              <div>
                <h1 className="text-2xl font-bold text-gray-900">{doc.fileName}</h1>
                <div className="mt-1 flex items-center gap-3 text-sm text-gray-500">
                  <StatusBadge status={doc.status} />
                  <span>{doc.fileType}</span>
                  <span>{(doc.fileSizeBytes / 1024).toFixed(1)} KB</span>
                  {result && (
                    <span className="flex items-center gap-1">
                      Overall:{' '}
                      <ConfidenceBadge confidence={result.confidenceScore} />
                    </span>
                  )}
                </div>
              </div>
              <div className="flex gap-2">
                {(doc.status === 'Completed' || doc.status === 'NeedsReview') && (
                  <button
                    onClick={handleApprove}
                    disabled={approve.isPending}
                    className="rounded-md bg-green-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-green-700 disabled:opacity-50"
                  >
                    {approve.isPending ? 'Approving…' : 'Approve'}
                  </button>
                )}
                {doc.status !== 'Processing' && (
                  <button
                    onClick={handleReprocess}
                    disabled={reprocess.isPending}
                    className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700 disabled:opacity-50"
                  >
                    {reprocess.isPending ? 'Reprocessing…' : 'Reprocess'}
                  </button>
                )}
              </div>
            </div>

            <div className="grid gap-6 lg:grid-cols-2">
              {/* Left: Document viewer */}
              <div>
                <h2 className="mb-3 text-lg font-semibold text-gray-900">Document</h2>
                <Suspense fallback={<div className="h-96 animate-pulse rounded-lg bg-gray-100" />}>
                  <PdfViewer documentId={doc.id} fileType={doc.fileType} highlightText={selectedField?.value} />
                </Suspense>
              </div>

              {/* Right: Extracted fields */}
              <div>
                <h2 className="mb-3 text-lg font-semibold text-gray-900">
                  Extracted Fields
                </h2>
                {result ? (
                  <div className="space-y-2">
                    {result.extractedFields.map((field) => (
                      <FieldEditor
                        key={field.id}
                        field={field}
                        onSave={handleFieldSave}
                        isSelected={selectedField?.id === field.id}
                        onSelect={(f) => setSelectedField(prev => prev?.id === f.id ? null : f)}
                      />
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-gray-500">
                    {doc.status === 'Processing'
                      ? 'Processing in progress…'
                      : 'No extraction results available.'}
                  </p>
                )}

                {result?.metrics && (
                  <div className="mt-6 rounded-lg border border-gray-200 bg-white p-4">
                    <h3 className="mb-2 text-sm font-semibold text-gray-700">
                      Processing Metrics
                    </h3>
                    <dl className="grid grid-cols-2 gap-2 text-sm">
                      <dt className="text-gray-500">OCR Duration</dt>
                      <dd className="text-gray-900">{result.metrics.ocrDurationMs} ms</dd>
                      <dt className="text-gray-500">AI Duration</dt>
                      <dd className="text-gray-900">{result.metrics.aiDurationMs} ms</dd>
                      <dt className="text-gray-500">Total Duration</dt>
                      <dd className="text-gray-900">{result.metrics.totalDurationMs} ms</dd>
                      <dt className="text-gray-500">Tokens Used</dt>
                      <dd className="text-gray-900">{result.metrics.tokenCount}</dd>
                      {result.metrics.estimatedCost != null && (
                        <>
                          <dt className="text-gray-500">Estimated Cost</dt>
                          <dd className="text-gray-900">
                            ${result.metrics.estimatedCost.toFixed(4)}
                          </dd>
                        </>
                      )}
                      {result.aiProvider && (
                        <>
                          <dt className="text-gray-500">AI Provider</dt>
                          <dd className="text-gray-900">{result.aiProvider}</dd>
                        </>
                      )}
                    </dl>
                  </div>
                )}

                {result?.rawOcrText && (
                  <div className="mt-6 rounded-lg border border-gray-200 bg-white p-4">
                    <h3 className="mb-2 text-sm font-semibold text-gray-700">
                      OCR Extracted Text
                    </h3>
                    <pre className="max-h-64 overflow-auto whitespace-pre-wrap rounded bg-gray-50 p-3 text-xs text-gray-700">
                      {result.rawOcrText}
                    </pre>
                  </div>
                )}
              </div>
            </div>
          </div>
        );
      }}
    </PageState>
  );
}
