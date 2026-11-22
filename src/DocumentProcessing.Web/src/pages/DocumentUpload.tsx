import { useState } from 'react';
import { useNavigate } from 'react-router';
import UploadDropzone from '../components/UploadDropzone';
import { useAuthStatus, useUploadDocument, useTemplates } from '../api/hooks';

interface FileUpload {
  file: File;
  status: 'pending' | 'uploading' | 'done' | 'error';
  documentId?: string;
  error?: string;
}

export default function DocumentUpload() {
  const [files, setFiles] = useState<FileUpload[]>([]);
  const [templateId, setTemplateId] = useState('');
  const { data: templates } = useTemplates();
  const upload = useUploadDocument();
  const { data: auth } = useAuthStatus();
  const readOnly = auth?.canWrite === false;
  const navigate = useNavigate();

  function handleFilesSelected(selected: File[]) {
    const newFiles = selected.map((f) => ({
      file: f,
      status: 'pending' as const,
    }));
    setFiles((prev) => [...prev, ...newFiles]);
  }

  async function handleUploadAll() {
    for (let i = 0; i < files.length; i++) {
      const item = files[i];
      if (!item || item.status !== 'pending') continue;

      setFiles((prev) =>
        prev.map((f, idx) => (idx === i ? { ...f, status: 'uploading' } : f)),
      );

      try {
        const result = await upload.mutateAsync({
          file: item.file,
          templateId: templateId || undefined,
        });
        setFiles((prev) =>
          prev.map((f, idx) =>
            idx === i
              ? { ...f, status: 'done', documentId: result.documentId }
              : f,
          ),
        );
      } catch (err) {
        setFiles((prev) =>
          prev.map((f, idx) =>
            idx === i
              ? { ...f, status: 'error', error: (err as Error).message }
              : f,
          ),
        );
      }
    }
  }

  function removeFile(index: number) {
    setFiles((prev) => prev.filter((_, i) => i !== index));
  }

  const pendingCount = files.filter((f) => f.status === 'pending').length;

  return (
    <div>
      <h1 className="mb-6 text-2xl font-bold text-gray-900">Upload Documents</h1>

      {readOnly && (
        <div className="mb-4 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          This is a read-only demo. Browse the sample documents, or enter an access key (top right) to upload.
        </div>
      )}

      <div className="mb-4">
        <label className="block text-sm font-medium text-gray-700">
          Template (optional)
        </label>
        <select
          value={templateId}
          onChange={(e) => setTemplateId(e.target.value)}
          className="mt-1 block w-full max-w-xs rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
        >
          <option value="">Auto-detect</option>
          {templates?.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
      </div>

      <UploadDropzone onFilesSelected={handleFilesSelected} />

      {files.length > 0 && (
        <div className="mt-6">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-lg font-semibold text-gray-900">
              Files ({files.length})
            </h2>
            {pendingCount > 0 && (
              <button
                onClick={handleUploadAll}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700"
              >
                Upload All ({pendingCount})
              </button>
            )}
          </div>

          <ul className="space-y-2">
            {files.map((f, i) => (
              <li
                key={i}
                className="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-3"
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-gray-900">
                    {f.file.name}
                  </p>
                  <p className="text-xs text-gray-500">
                    {(f.file.size / 1024).toFixed(1)} KB
                  </p>
                </div>
                <div className="ml-4 flex items-center gap-2">
                  {f.status === 'pending' && (
                    <span className="text-xs text-gray-500">Pending</span>
                  )}
                  {f.status === 'uploading' && (
                    <span className="text-xs text-indigo-600 animate-pulse">
                      Uploading…
                    </span>
                  )}
                  {f.status === 'done' && (
                    <button
                      onClick={() => navigate(`/documents/${f.documentId}`)}
                      className="text-xs font-medium text-green-600 hover:text-green-700"
                    >
                      ✓ View
                    </button>
                  )}
                  {f.status === 'error' && (
                    <span className="text-xs text-red-600">
                      ✗ {f.error ?? 'Failed'}
                    </span>
                  )}
                  {f.status === 'pending' && (
                    <button
                      onClick={() => removeFile(i)}
                      className="text-gray-400 hover:text-red-500"
                    >
                      ×
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
