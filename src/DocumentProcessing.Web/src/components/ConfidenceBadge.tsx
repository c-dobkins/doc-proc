interface Props {
  confidence: number;
}

export default function ConfidenceBadge({ confidence }: Props) {
  const pct = Math.round(confidence);
  let color: string;

  if (pct >= 90) color = 'bg-green-100 text-green-800';
  else if (pct >= 70) color = 'bg-yellow-100 text-yellow-800';
  else color = 'bg-red-100 text-red-800';

  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${color}`}
    >
      {pct}%
    </span>
  );
}
