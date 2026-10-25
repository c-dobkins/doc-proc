import { Link } from 'react-router';
import { useDashboardMetrics } from '../api/hooks';
import PageState from '../components/PageState';
import type { DashboardMetricsDto } from '../api/types';

interface StatCardProps {
  label: string;
  value: number;
  color: string;
}

function StatCard({ label, value, color }: StatCardProps) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <p className="text-sm font-medium text-gray-500">{label}</p>
      <p className={`mt-2 text-3xl font-bold ${color}`}>{value}</p>
    </div>
  );
}

export default function Dashboard() {
  const { data, isLoading, error } = useDashboardMetrics();

  return (
    <PageState isLoading={isLoading} error={error} data={data} loadingText="Loading dashboard…">
      {(data: DashboardMetricsDto) => {
        const stats: StatCardProps[] = [
          { label: 'Total Documents', value: data.totalDocuments, color: 'text-gray-900' },
          { label: 'Uploaded', value: data.uploaded, color: 'text-blue-600' },
          { label: 'Processing', value: data.processing, color: 'text-indigo-600' },
          { label: 'Completed', value: data.completed, color: 'text-green-600' },
          { label: 'Needs Review', value: data.needsReview, color: 'text-yellow-600' },
          { label: 'Failed', value: data.failed, color: 'text-red-600' },
          { label: 'Approved', value: data.approved, color: 'text-emerald-600' },
        ];

        return (
          <div>
            <div className="mb-6 flex items-center justify-between">
              <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
              <Link
                to="/upload"
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700"
              >
                Upload Document
              </Link>
            </div>

            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
              {stats.map((s) => (
                <StatCard key={s.label} {...s} />
              ))}
            </div>

            {data.totalDocuments > 0 && (
              <div className="mt-8 rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
                <h2 className="mb-4 text-lg font-semibold text-gray-900">
                  Processing Overview
                </h2>
                <div className="h-4 w-full overflow-hidden rounded-full bg-gray-200">
                  <div className="flex h-full">
                    <div
                      className="bg-emerald-500"
                      style={{
                        width: `${((data.approved + data.completed) / data.totalDocuments) * 100}%`,
                      }}
                      title={`Completed/Approved: ${data.approved + data.completed}`}
                    />
                    <div
                      className="bg-yellow-400"
                      style={{
                        width: `${(data.needsReview / data.totalDocuments) * 100}%`,
                      }}
                      title={`Needs Review: ${data.needsReview}`}
                    />
                    <div
                      className="bg-indigo-500"
                      style={{
                        width: `${(data.processing / data.totalDocuments) * 100}%`,
                      }}
                      title={`Processing: ${data.processing}`}
                    />
                    <div
                      className="bg-red-500"
                      style={{
                        width: `${(data.failed / data.totalDocuments) * 100}%`,
                      }}
                      title={`Failed: ${data.failed}`}
                    />
                  </div>
                </div>
                <div className="mt-2 flex flex-wrap gap-4 text-xs text-gray-500">
                  <span className="flex items-center gap-1">
                    <span className="inline-block h-2 w-2 rounded-full bg-emerald-500" />
                    Completed / Approved
                  </span>
                  <span className="flex items-center gap-1">
                    <span className="inline-block h-2 w-2 rounded-full bg-yellow-400" />
                    Needs Review
                  </span>
                  <span className="flex items-center gap-1">
                    <span className="inline-block h-2 w-2 rounded-full bg-indigo-500" />
                    Processing
                  </span>
                  <span className="flex items-center gap-1">
                    <span className="inline-block h-2 w-2 rounded-full bg-red-500" />
                    Failed
                  </span>
                </div>
              </div>
            )}
          </div>
        );
      }}
    </PageState>
  );
}
