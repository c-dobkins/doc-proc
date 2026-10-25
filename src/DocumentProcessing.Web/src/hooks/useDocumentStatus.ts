import { useEffect, useRef } from 'react';
import {
  HubConnectionBuilder,
  HubConnection,
  LogLevel,
} from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import type { StatusUpdatedMessage } from '../api/types';

/** Subscribe to real-time status updates for a specific document. */
export function useDocumentStatus(
  documentId: string | undefined,
  onStatusChanged?: (status: string) => void,
) {
  const connectionRef = useRef<HubConnection | null>(null);
  const qc = useQueryClient();

  useEffect(() => {
    if (!documentId) return;

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/processing')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    const invalidate = () => {
      qc.invalidateQueries({ queryKey: ['document', documentId] });
      qc.invalidateQueries({ queryKey: ['documents'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
    };

    connection.on('StatusUpdated', ({ status }: StatusUpdatedMessage) => {
      onStatusChanged?.(status);
      invalidate();
    });

    // Group membership is per connection and is not restored on reconnect,
    // so rejoin and refetch anything missed while disconnected.
    connection.onreconnected(() => {
      connection.invoke('JoinDocumentGroup', documentId).catch(() => {});
      invalidate();
    });

    // Defer start so React StrictMode cleanup can cancel before negotiation begins
    const timeoutId = setTimeout(() => {
      connection
        .start()
        .then(() => connection.invoke('JoinDocumentGroup', documentId))
        .catch(() => {});
    }, 0);

    return () => {
      clearTimeout(timeoutId);
      connection.stop().catch(() => {});
      connectionRef.current = null;
    };
  }, [documentId, onStatusChanged, qc]);
}
