import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

/**
 * Search is public so anyone can browse departures; everything from booking onward requires a
 * signed-in customer. Feature components are lazy-loaded so each route ships its own chunk.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'search' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register').then((m) => m.RegisterComponent),
  },
  {
    path: 'search',
    loadComponent: () => import('./features/trips/search').then((m) => m.SearchComponent),
  },
  {
    path: 'book/:tripId',
    canActivate: [authGuard],
    loadComponent: () => import('./features/booking/booking').then((m) => m.BookingComponent),
  },
  {
    path: 'pay/:bookingId',
    canActivate: [authGuard],
    loadComponent: () => import('./features/payment/pay').then((m) => m.PayComponent),
  },
  {
    path: 'ticket/:bookingId',
    canActivate: [authGuard],
    loadComponent: () => import('./features/tickets/ticket').then((m) => m.TicketComponent),
  },
  {
    path: 'my-bookings',
    canActivate: [authGuard],
    loadComponent: () => import('./features/tickets/my-bookings').then((m) => m.MyBookingsComponent),
  },
  { path: '**', redirectTo: 'search' },
];
