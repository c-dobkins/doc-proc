import type { DocumentStatus } from '../api/types';

interface Props {
  status: DocumentStatus;
}

const statusStyles: Record<DocumentStatus, string> = {
  Uploaded: 'bg-blue-100 text-blue-800',
  Processing: 'bg-indigo-100 text-indigo-800 animate-pulse',
  Completed: 'bg-green-100 text-green-800',
  Failed: 'bg-red-100 text-red-800',
  NeedsReview: 'bg-yellow-100 text-yellow-800',
  Approved: 'bg-emerald-100 text-emerald-800',
};

export default function StatusBadge({ status }: Props) {
  const style = statusStyles[status];

  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${style}`}
    >
      {status === 'NeedsReview' ? 'Needs Review' : status}
    </span>
  );
}
