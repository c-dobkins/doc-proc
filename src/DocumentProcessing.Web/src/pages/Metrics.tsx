import { useDashboardMetrics, useDocuments } from '../api/hooks';

export default function Metrics() {
  const { data: dashboard, isLoading: dashLoading } = useDashboardMetrics();
  const { data: recentDocs, isLoading: docsLoading } = useDocuments(1, 50);

  if (dashLoading || docsLoading) return <p className="text-gray-500">Loading metrics…</p>;
  if (!dashboard) return null;

  // Filter for completed/approved documents
  const completedDocs = (recentDocs?.items ?? []).filter(
    (d) => d.status === 'Completed' || d.status === 'Approved',
  );
  const processedCount =
    dashboard.completed + dashboard.approved + dashboard.failed + dashboard.needsReview;
  const accuracyRate =
    processedCount > 0
      ? ((dashboard.completed + dashboard.approved) / processedCount) * 100
      : 0;
  const failureRate =
    processedCount > 0
      ? (dashboard.failed / processedCount) * 100
      : 0;
  const reviewRate =
    processedCount > 0
      ? (dashboard.needsReview / processedCount) * 100
      : 0;

  return (
    <div>
      <h1 className="mb-6 text-2xl font-bold text-gray-900">Metrics</h1>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Processing Rates */}
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-lg font-semibold text-gray-900">
            Processing Rates
          </h2>
          <dl className="space-y-3">
            <div className="flex items-center justify-between">
              <dt className="text-sm text-gray-500">Success Rate</dt>
              <dd className="text-lg font-semibold text-green-600">
                {accuracyRate.toFixed(1)}%
              </dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-sm text-gray-500">Review Rate</dt>
              <dd className="text-lg font-semibold text-yellow-600">
                {reviewRate.toFixed(1)}%
              </dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-sm text-gray-500">Failure Rate</dt>
              <dd className="text-lg font-semibold text-red-600">
                {failureRate.toFixed(1)}%
              </dd>
            </div>
          </dl>
        </div>

        {/* Throughput */}
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-lg font-semibold text-gray-900">Throughput</h2>
          <dl className="space-y-3">
            <div className="flex items-center justify-between">
              <dt className="text-sm text-gray-500">Total Processed</dt>
              <dd className="text-lg font-semibold text-gray-900">
                {dashboard.completed + dashboard.approved + dashboard.failed}
              </dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-sm text-gray-500">Currently Processing</dt>
              <dd className="text-lg font-semibold text-indigo-600">
                {dashboard.processing}
              </dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-sm text-gray-500">Awaiting Review</dt>
              <dd className="text-lg font-semibold text-yellow-600">
                {dashboard.needsReview}
              </dd>
            </div>
          </dl>
        </div>

        {/* Confidence Distribution from recent docs */}
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm lg:col-span-2">
          <h2 className="mb-4 text-lg font-semibold text-gray-900">
            Recent Completed Documents
          </h2>
          {completedDocs.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200 text-sm">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-3 py-2 text-left text-xs font-medium uppercase text-gray-500">
                      File
                    </th>
                    <th className="px-3 py-2 text-left text-xs font-medium uppercase text-gray-500">
                      Confidence
                    </th>
                    <th className="px-3 py-2 text-left text-xs font-medium uppercase text-gray-500">
                      Processed At
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {completedDocs.map((doc) => (
                    <tr key={doc.id}>
                      <td className="px-3 py-2 text-gray-900">{doc.fileName}</td>
                      <td className="px-3 py-2">
                        {doc.confidenceScore != null ? (
                          <span
                            className={`font-medium ${
                              doc.confidenceScore >= 90
                                ? 'text-green-600'
                                : doc.confidenceScore >= 70
                                  ? 'text-yellow-600'
                                  : 'text-red-600'
                            }`}
                          >
                            {Math.round(doc.confidenceScore)}%
                          </span>
                        ) : (
                          '—'
                        )}
                      </td>
                      <td className="px-3 py-2 text-gray-500">
                        {doc.processedAt
                          ? new Date(doc.processedAt).toLocaleString()
                          : '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-gray-500">
              No completed documents yet.
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
