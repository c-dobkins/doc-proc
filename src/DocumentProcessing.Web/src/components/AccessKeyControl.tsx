import { useState } from 'react';
import type { FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useAuthStatus } from '../api/hooks';
import { getAccessKey, setAccessKey } from '../api/accessKey';
import { getAuthStatus } from '../api/client';
import type { AuthStatusDto } from '../api/types';

/**
 * Shows whether the user can make changes. On deployments where writes need an access key,
 * lets them enter one (validated against /api/auth/status) or clear it.
 */
export default function AccessKeyControl() {
  const { data: status } = useAuthStatus();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const [rejected, setRejected] = useState(false);

  if (!status?.writesRequireKey) return null;

  const hasKey = getAccessKey() !== null;

  async function apply(key: string | null): Promise<AuthStatusDto> {
    setAccessKey(key);
    return queryClient.fetchQuery({ queryKey: ['auth-status'], queryFn: getAuthStatus, staleTime: 0 });
  }

  async function submit(e: FormEvent) {
    e.preventDefault();
    const result = await apply(draft.trim() || null);
    if (result.canWrite) {
      setEditing(false);
      setDraft('');
      setRejected(false);
    } else {
      setAccessKey(null);
      setRejected(true);
    }
  }

  if (status.canWrite) {
    return (
      <div className="flex items-center gap-2 text-sm">
        <span className="rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800">
          Editing enabled
        </span>
        {hasKey && (
          <button
            type="button"
            className="text-gray-500 hover:text-gray-900"
            onClick={() => void apply(null)}
          >
            Remove key
          </button>
        )}
      </div>
    );
  }

  if (!editing) {
    return (
      <div className="flex items-center gap-2 text-sm">
        <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-medium text-amber-800">
          Read-only demo
        </span>
        <button
          type="button"
          className="font-medium text-indigo-600 hover:text-indigo-800"
          onClick={() => setEditing(true)}
        >
          Enter access key
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="flex items-center gap-2 text-sm">
      <label htmlFor="access-key" className="sr-only">
        Access key
      </label>
      <input
        id="access-key"
        type="password"
        autoComplete="off"
        value={draft}
        onChange={(e) => {
          setDraft(e.target.value);
          setRejected(false);
        }}
        placeholder="Access key"
        className={`w-44 rounded-md border px-2 py-1 ${rejected ? 'border-red-400' : 'border-gray-300'}`}
        aria-invalid={rejected}
      />
      <button type="submit" className="rounded-md bg-indigo-600 px-3 py-1 font-medium text-white hover:bg-indigo-700">
        Unlock
      </button>
      <button type="button" className="text-gray-500 hover:text-gray-900" onClick={() => setEditing(false)}>
        Cancel
      </button>
      {rejected && (
        <span role="alert" className="text-xs text-red-600">
          Key not accepted
        </span>
      )}
    </form>
  );
}
