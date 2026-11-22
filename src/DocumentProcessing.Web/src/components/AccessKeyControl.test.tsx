import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import AccessKeyControl from './AccessKeyControl';
import { getAccessKey, setAccessKey } from '../api/accessKey';

const VALID_KEY = 'valid-key-0123456789abcdef';

// Fake /api/auth/status: writes need a key; only VALID_KEY may write
function mockServer() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (_url, init) => {
    const key = new Headers(init?.headers).get('X-Api-Key');
    const body = { writesRequireKey: true, canWrite: key === VALID_KEY };
    return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
  });
}

function renderControl() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <AccessKeyControl />
    </QueryClientProvider>,
  );
}

describe('AccessKeyControl', () => {
  beforeEach(() => setAccessKey(null));
  afterEach(() => vi.restoreAllMocks());

  it('shows read-only mode when no key is stored', async () => {
    mockServer();
    renderControl();

    expect(await screen.findByText('Read-only demo')).toBeInTheDocument();
  });

  it('unlocks editing with a valid key and sends it on requests', async () => {
    const fetchSpy = mockServer();
    renderControl();

    fireEvent.click(await screen.findByText('Enter access key'));
    fireEvent.change(screen.getByLabelText('Access key'), { target: { value: VALID_KEY } });
    fireEvent.click(screen.getByText('Unlock'));

    expect(await screen.findByText('Editing enabled')).toBeInTheDocument();
    expect(getAccessKey()).toBe(VALID_KEY);
    const lastInit = fetchSpy.mock.lastCall?.[1];
    expect(new Headers(lastInit?.headers).get('X-Api-Key')).toBe(VALID_KEY);
  });

  it('rejects an invalid key and does not keep it', async () => {
    mockServer();
    renderControl();

    fireEvent.click(await screen.findByText('Enter access key'));
    fireEvent.change(screen.getByLabelText('Access key'), { target: { value: 'wrong-key' } });
    fireEvent.click(screen.getByText('Unlock'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Key not accepted');
    expect(getAccessKey()).toBeNull();
  });

  it('renders nothing when the deployment does not require a key', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ writesRequireKey: false, canWrite: true }), { status: 200 }),
    );
    const { container } = renderControl();

    await vi.waitFor(() => expect(globalThis.fetch).toHaveBeenCalled());
    expect(container).toBeEmptyDOMElement();
  });
});
