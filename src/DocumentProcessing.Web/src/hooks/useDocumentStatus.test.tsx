import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import type { StatusUpdatedMessage } from '../api/types';

type Handler = (...args: unknown[]) => void;

const mockConnection = vi.hoisted(() => ({
  handlers: new Map<string, Handler>(),
  reconnectedHandler: undefined as (() => void) | undefined,
  on: vi.fn(),
  onreconnected: vi.fn(),
  start: vi.fn(),
  stop: vi.fn(),
  invoke: vi.fn(),
}));

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return mockConnection; }
  }
  return { HubConnectionBuilder, LogLevel: { Warning: 3 } };
});

import { useDocumentStatus } from './useDocumentStatus';

const DOC_ID = 'doc-123';

function setup(onStatusChanged?: (status: string) => void) {
  const queryClient = new QueryClient();
  const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const hook = renderHook(() => useDocumentStatus(DOC_ID, onStatusChanged), { wrapper });
  return { ...hook, invalidateSpy };
}

describe('useDocumentStatus', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mockConnection.handlers.clear();
    mockConnection.reconnectedHandler = undefined;
    mockConnection.on.mockImplementation((name: string, handler: Handler) => {
      mockConnection.handlers.set(name, handler);
    });
    mockConnection.onreconnected.mockImplementation((handler: () => void) => {
      mockConnection.reconnectedHandler = handler;
    });
    mockConnection.start.mockResolvedValue(undefined);
    mockConnection.stop.mockResolvedValue(undefined);
    mockConnection.invoke.mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('joins the document group after the connection starts', async () => {
    setup();

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(mockConnection.start).toHaveBeenCalledOnce();
    expect(mockConnection.invoke).toHaveBeenCalledWith('JoinDocumentGroup', DOC_ID);
  });

  it('passes the status from the single payload argument to onStatusChanged', () => {
    const onStatusChanged = vi.fn();
    const { invalidateSpy } = setup(onStatusChanged);

    const payload: StatusUpdatedMessage = { documentId: DOC_ID, status: 'Completed' };
    act(() => {
      mockConnection.handlers.get('StatusUpdated')?.(payload);
    });

    expect(onStatusChanged).toHaveBeenCalledWith('Completed');
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['document', DOC_ID] });
  });

  it('rejoins the document group and refetches after reconnecting', () => {
    const { invalidateSpy } = setup();
    expect(mockConnection.reconnectedHandler).toBeDefined();
    mockConnection.invoke.mockClear();

    act(() => {
      mockConnection.reconnectedHandler?.();
    });

    expect(mockConnection.invoke).toHaveBeenCalledWith('JoinDocumentGroup', DOC_ID);
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['document', DOC_ID] });
  });
});
