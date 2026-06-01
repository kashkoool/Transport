import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { roleGuard } from './core/auth/role.guard';

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

  // ── Vendor console (VendorManager only) ──
  { path: 'vendor', pathMatch: 'full', redirectTo: 'vendor/trips' },
  {
    path: 'vendor/trips',
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/trips').then((m) => m.VendorTripsComponent),
  },
  {
    path: 'vendor/buses',
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/buses').then((m) => m.VendorBusesComponent),
  },

  // ── Admin console (Admin / SuperAdmin only) ──
  { path: 'admin', pathMatch: 'full', redirectTo: 'admin/companies' },
  {
    path: 'admin/companies',
    canActivate: [roleGuard('Admin', 'SuperAdmin')],
    loadComponent: () => import('./features/admin/companies').then((m) => m.AdminCompaniesComponent),
  },

  { path: '**', redirectTo: 'search' },
];
