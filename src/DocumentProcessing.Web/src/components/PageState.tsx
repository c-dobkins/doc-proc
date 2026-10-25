import type { ReactNode } from 'react';

interface Props<T> {
  isLoading: boolean;
  error: Error | null;
  data: T | undefined;
  loadingText?: string;
  notFoundText?: string;
  children: (data: T) => ReactNode;
}

export default function PageState<T>({
  isLoading,
  error,
  data,
  loadingText = 'Loading…',
  notFoundText,
  children,
}: Props<T>) {
  if (isLoading) return <p className="text-gray-500">{loadingText}</p>;
  if (error) return <p className="text-red-600">Error: {error.message}</p>;
  if (data == null)
    return notFoundText ? <p className="text-gray-500">{notFoundText}</p> : null;
  return <>{children(data)}</>;
}
