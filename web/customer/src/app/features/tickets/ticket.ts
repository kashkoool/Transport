import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toDataURL } from 'qrcode';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { phosphorArrowLeft, phosphorTicket } from '@ng-icons/phosphor-icons/regular';
import { ApiService } from '../../core/api/api.service';
import { Ticket } from '../../core/models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-ticket',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, RouterLink, NgIcon, TranslatePipe],
  providers: [provideIcons({ phosphorArrowLeft, phosphorTicket })],
  template: `
    <div class="mx-auto max-w-md">
      @if (ticket(); as t) {
        <div class="card animate-in overflow-hidden">
          <div class="flex items-center justify-between bg-linear-to-br from-brand-700 to-brand-500 px-6 py-4 text-white">
            <span class="flex items-center gap-2 font-semibold"><ng-icon name="phosphorTicket" /> {{ 'ticket.boardingTicket' | t }}</span>
            <span
              class="badge text-white ring-1"
              [class]="t.status === 'Confirmed' ? 'bg-emerald-500 ring-emerald-300/40' : 'bg-amber-400 text-ink! ring-amber-200/40'"
            >
              {{ t.status }}
            </span>
          </div>

          <div class="space-y-4 px-6 py-5">
            <div>
              <p class="text-2xl font-bold text-slate-900 dark:text-slate-100">{{ t.origin }} → {{ t.destination }}</p>
              <p class="text-sm text-slate-500 dark:text-slate-400">{{ t.departureUtc | date: 'EEEE, MMM d • HH:mm' }}</p>
            </div>

            <div class="rounded-2xl bg-slate-50 p-3 dark:bg-white/5">
              <p class="text-xs uppercase tracking-wide text-slate-400 dark:text-slate-500">{{ 'ticket.reference' | t }}</p>
              <p class="font-mono text-lg font-semibold text-slate-800 dark:text-slate-200">{{ t.reference }}</p>
            </div>

            <div>
              <p class="mb-1 text-xs uppercase tracking-wide text-slate-400 dark:text-slate-500">{{ 'ticket.passengers' | t }}</p>
              <ul class="divide-y divide-slate-100 dark:divide-white/10">
                @for (p of t.passengers; track p.seatNumber) {
                  <li class="flex justify-between py-1.5 text-sm">
                    <span class="text-slate-700 dark:text-slate-300">{{ p.firstName }} {{ p.lastName }}</span>
                    <span class="font-medium text-slate-500 dark:text-slate-400">{{ 'ticket.seat' | t: { seat: p.seatNumber } }}</span>
                  </li>
                }
              </ul>
            </div>

            @if (qr(); as qrUrl) {
              <div class="flex flex-col items-center gap-2 border-t border-dashed border-slate-200 pt-4 dark:border-white/10">
                <div class="rounded-2xl bg-white p-3 ring-1 ring-slate-100">
                  <img [src]="qrUrl" [alt]="'ticket.qrAlt' | t" class="h-40 w-40" />
                </div>
                <p class="text-xs text-slate-400 dark:text-slate-500">{{ 'ticket.showAtBoarding' | t }}</p>
              </div>
            }

            <div class="flex items-center justify-between border-t border-dashed border-slate-200 pt-4 dark:border-white/10">
              <span class="text-sm text-slate-500 dark:text-slate-400">{{ 'ticket.totalPaid' | t }}</span>
              <span class="text-lg font-bold text-slate-900 dark:text-slate-100">
                {{ t.totalAmount | number: '1.0-0' }} {{ t.currency }}
              </span>
            </div>
          </div>
        </div>

        <a routerLink="/my-bookings" class="btn btn-ghost mt-4 w-full">
          <ng-icon name="phosphorArrowLeft" class="rtl:rotate-180" /> {{ 'ticket.viewAll' | t }}
        </a>
      } @else if (!loading()) {
        <div class="card flex flex-col items-center gap-2 p-12 text-center">
          <ng-icon name="phosphorTicket" class="text-4xl text-slate-300 dark:text-slate-600" />
          <p class="text-slate-500 dark:text-slate-400">{{ 'ticket.notFound' | t }}</p>
          <a routerLink="/my-bookings" class="btn btn-soft mt-2">{{ 'ticket.viewAll' | t }}</a>
        </div>
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
