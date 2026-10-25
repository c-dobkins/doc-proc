import { useDocumentStatus } from '../hooks/useDocumentStatus';
import type { DocumentStatus } from '../api/types';
import StatusBadge from './StatusBadge';

interface Props {
  documentId: string;
  initialStatus: DocumentStatus;
}

export default function ProcessingStatus({ documentId, initialStatus }: Props) {
  useDocumentStatus(documentId);

  return (
    <div className="flex items-center gap-2">
      <StatusBadge status={initialStatus} />
      {initialStatus === 'Processing' && (
        <span className="text-xs text-gray-500">Watching for updates…</span>
      )}
    </div>
  );
}
