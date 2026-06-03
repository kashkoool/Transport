import { inject, Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { AuthService } from '../auth/auth.service';

/**
 * Live seat-availability updates for a single trip. The booking screen calls
 * {@link watchTrip} on entry and the returned disposer on leave. A failed connection
 * degrades gracefully — the seat map still works, just without live refreshes.
 */
@Injectable({ providedIn: 'root' })
export class TripRealtimeService {
  private readonly auth = inject(AuthService);

  /**
   * Subscribe to seat-availability changes for {@link tripId}. `onSeatUpdate` fires whenever
   * another booking/cancellation changes the trip. Returns a disposer that unsubscribes and
   * closes the connection.
   */
  watchTrip(tripId: string, onSeatUpdate: () => void): () => void {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/realtime', { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('seat-update', () => onSeatUpdate());

    // Re-join the trip group after a reconnect (group membership is per-connection).
    connection.onreconnected(() => {
      connection.invoke('SubscribeToTrip', tripId).catch(() => undefined);
    });

    let disposed = false;
    connection
      .start()
      .then(() => {
        if (disposed) return;
        return connection.invoke('SubscribeToTrip', tripId);
      })
      .catch(() => undefined);

    return () => {
      disposed = true;
      connection.stop().catch(() => undefined);
    };
  }
}
