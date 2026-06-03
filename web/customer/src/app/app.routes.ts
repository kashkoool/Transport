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
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/auth/forgot-password').then((m) => m.ForgotPasswordComponent),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./features/auth/reset-password').then((m) => m.ResetPasswordComponent),
  },
  {
    path: 'verify-email',
    loadComponent: () => import('./features/auth/verify-email').then((m) => m.VerifyEmailComponent),
  },
  {
    path: 'auth/callback',
    loadComponent: () => import('./features/auth/auth-callback').then((m) => m.AuthCallbackComponent),
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
  {
    path: 'account',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/profile').then((m) => m.ProfileComponent),
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
  {
    path: 'vendor/staff',
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/staff').then((m) => m.VendorStaffComponent),
  },
  {
    path: 'vendor/drivers',
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/drivers').then((m) => m.VendorDriversComponent),
  },
  {
    path: 'vendor/company',
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/company').then((m) => m.VendorCompanyComponent),
  },
  {
    path: 'vendor/promo',
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/promo').then((m) => m.VendorPromoComponent),
  },
  {
    path: 'vendor/reports',
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/reports').then((m) => m.VendorReportsComponent),
  },
  {
    // The desk is for managers AND staff (counter sales).
    path: 'vendor/desk',
    canActivate: [roleGuard('VendorManager', 'Staff')],
    loadComponent: () => import('./features/vendor/desk').then((m) => m.VendorDeskComponent),
  },

  // ── Admin console (Admin / SuperAdmin only) ──
  { path: 'admin', pathMatch: 'full', redirectTo: 'admin/companies' },
  {
    path: 'admin/companies',
    canActivate: [roleGuard('Admin', 'SuperAdmin')],
    loadComponent: () => import('./features/admin/companies').then((m) => m.AdminCompaniesComponent),
  },
  {
    path: 'admin/reports',
    canActivate: [roleGuard('Admin', 'SuperAdmin')],
    loadComponent: () => import('./features/admin/reports').then((m) => m.AdminReportsComponent),
  },

  { path: '**', redirectTo: 'search' },
];
