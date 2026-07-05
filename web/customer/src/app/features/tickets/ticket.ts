import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toDataURL } from 'qrcode';
import { ApiService } from '../../core/api/api.service';
import { Ticket } from '../../core/models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-ticket',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, RouterLink, TranslatePipe],
  template: `
    <div class="mx-auto max-w-md">
      @if (ticket(); as t) {
        <div class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div class="flex items-center justify-between bg-brand-600 px-6 py-4 text-white">
            <span class="font-semibold">{{ 'ticket.boardingTicket' | t }}</span>
            <span
              class="rounded-full px-3 py-1 text-xs font-semibold"
              [class.bg-emerald-500]="t.status === 'Confirmed'"
              [class.bg-amber-400]="t.status !== 'Confirmed'"
            >
              {{ t.status }}
            </span>
          </div>

          <div class="space-y-4 px-6 py-5">
            <div>
              <p class="text-2xl font-bold text-slate-900">{{ t.origin }} → {{ t.destination }}</p>
              <p class="text-sm text-slate-500">{{ t.departureUtc | date: 'EEEE, MMM d • HH:mm' }}</p>
            </div>

            <div class="rounded-lg bg-slate-50 p-3">
              <p class="text-xs uppercase tracking-wide text-slate-400">{{ 'ticket.reference' | t }}</p>
              <p class="font-mono text-lg font-semibold text-slate-800">{{ t.reference }}</p>
            </div>

            <div>
              <p class="mb-1 text-xs uppercase tracking-wide text-slate-400">{{ 'ticket.passengers' | t }}</p>
              <ul class="divide-y divide-slate-100">
                @for (p of t.passengers; track p.seatNumber) {
                  <li class="flex justify-between py-1.5 text-sm">
                    <span class="text-slate-700">{{ p.firstName }} {{ p.lastName }}</span>
                    <span class="font-medium text-slate-500">{{ 'ticket.seat' | t: { seat: p.seatNumber } }}</span>
                  </li>
                }
              </ul>
            </div>

            @if (qr(); as qrUrl) {
              <div class="flex flex-col items-center gap-2 pt-2">
                <img [src]="qrUrl" [alt]="'ticket.qrAlt' | t" class="h-44 w-44" />
                <p class="text-xs text-slate-400">{{ 'ticket.showAtBoarding' | t }}</p>
              </div>
            }

            <div class="flex items-center justify-between border-t border-slate-100 pt-4">
              <span class="text-sm text-slate-500">{{ 'ticket.totalPaid' | t }}</span>
              <span class="text-lg font-bold text-slate-900">
                {{ t.totalAmount | number: '1.0-0' }} {{ t.currency }}
              </span>
            </div>
          </div>
        </div>

        <a routerLink="/my-bookings" class="mt-4 block text-center text-sm text-brand-600 hover:text-brand-700">
          {{ 'ticket.viewAll' | t }}
        </a>
      } @else if (!loading()) {
        <p class="text-center text-slate-500">{{ 'ticket.notFound' | t }}</p>
      }
    </div>
  `,
})
export class TicketComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);

  protected readonly ticket = signal<Ticket | null>(null);
  protected readonly qr = signal<string | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('bookingId') ?? '';
    this.api.ticket(id).subscribe({
      next: (ticket) => {
        this.ticket.set(ticket);
        this.loading.set(false);
        // Render the QR client-side so the payload never leaves the browser.
        toDataURL(ticket.qrPayload, { margin: 1, width: 176 })
          .then((url) => this.qr.set(url))
          .catch(() => this.qr.set(null));
      },
      error: () => this.loading.set(false),
    });
  }
}
