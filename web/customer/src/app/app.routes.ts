import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { roleGuard } from './core/auth/role.guard';

/**
 * Search is public so anyone can browse departures; everything from booking onward requires a
 * signed-in customer. Feature components are lazy-loaded so each route ships its own chunk.
 */
export const routes: Routes = [
  {
    // The landing lives at the root (/), not /search.
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/trips/search').then((m) => m.SearchComponent),
  },
  // Auth pages are noindex (indexing a login portal is a phishing surface). `data.noindex` is
  // applied centrally in App via SeoService — see app.ts.
  {
    path: 'login',
    data: { noindex: true },
    loadComponent: () => import('./features/auth/login').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    data: { noindex: true },
    loadComponent: () => import('./features/auth/register').then((m) => m.RegisterComponent),
  },
  {
    path: 'forgot-password',
    data: { noindex: true },
    loadComponent: () =>
      import('./features/auth/forgot-password').then((m) => m.ForgotPasswordComponent),
  },
  {
    path: 'reset-password',
    data: { noindex: true },
    loadComponent: () =>
      import('./features/auth/reset-password').then((m) => m.ResetPasswordComponent),
  },
  {
    path: 'verify-email',
    data: { noindex: true },
    loadComponent: () => import('./features/auth/verify-email').then((m) => m.VerifyEmailComponent),
  },
  {
    path: 'auth/callback',
    data: { noindex: true },
    loadComponent: () => import('./features/auth/auth-callback').then((m) => m.AuthCallbackComponent),
  },
  { path: 'search', pathMatch: 'full', redirectTo: '' },

  // ── Public SEO landing pages (prerendered, indexable) ──
  {
    path: 'routes',
    pathMatch: 'full',
    loadComponent: () => import('./features/seo/routes-index').then((m) => m.RoutesIndexComponent),
  },
  {
    path: 'bus/:route',
    loadComponent: () => import('./features/seo/route-page').then((m) => m.RoutePageComponent),
  },
  {
    path: 'city/:city',
    loadComponent: () => import('./features/seo/city-page').then((m) => m.CityPageComponent),
  },

  // ── Private customer flow (all noindex) ──
  {
    path: 'book/:tripId',
    data: { noindex: true },
    canActivate: [authGuard],
    loadComponent: () => import('./features/booking/booking').then((m) => m.BookingComponent),
  },
  {
    path: 'pay/:bookingId',
    data: { noindex: true },
    canActivate: [authGuard],
    loadComponent: () => import('./features/payment/pay').then((m) => m.PayComponent),
  },
  {
    path: 'ticket/:bookingId',
    data: { noindex: true },
    canActivate: [authGuard],
    loadComponent: () => import('./features/tickets/ticket').then((m) => m.TicketComponent),
  },
  {
    path: 'my-bookings',
    data: { noindex: true },
    canActivate: [authGuard],
    loadComponent: () => import('./features/tickets/my-bookings').then((m) => m.MyBookingsComponent),
  },
  {
    path: 'account',
    data: { noindex: true },
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/profile').then((m) => m.ProfileComponent),
  },

  // ── Vendor console (VendorManager only; all noindex) ──
  { path: 'vendor', pathMatch: 'full', redirectTo: 'vendor/trips' },
  {
    // Trip management is shared by managers and staff (docs: Manager + Employee).
    path: 'vendor/trips',
    data: { noindex: true },
    canActivate: [roleGuard('VendorManager', 'Staff')],
    loadComponent: () => import('./features/vendor/trips').then((m) => m.VendorTripsComponent),
  },
  {
    path: 'vendor/buses',
    data: { noindex: true },
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/buses').then((m) => m.VendorBusesComponent),
  },
  {
    path: 'vendor/staff',
    data: { noindex: true },
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/staff').then((m) => m.VendorStaffComponent),
  },
  {
    path: 'vendor/drivers',
    data: { noindex: true },
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/drivers').then((m) => m.VendorDriversComponent),
  },
  {
    path: 'vendor/company',
    data: { noindex: true },
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/company').then((m) => m.VendorCompanyComponent),
  },
  {
    path: 'vendor/promo',
    data: { noindex: true },
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/promo').then((m) => m.VendorPromoComponent),
  },
  {
    path: 'vendor/reports',
    data: { noindex: true },
    canActivate: [roleGuard('VendorManager')],
    loadComponent: () => import('./features/vendor/reports').then((m) => m.VendorReportsComponent),
  },
  {
    // The desk is for managers AND staff (counter sales).
    path: 'vendor/desk',
    data: { noindex: true },
    canActivate: [roleGuard('VendorManager', 'Staff')],
    loadComponent: () => import('./features/vendor/desk').then((m) => m.VendorDeskComponent),
  },

  // ── Admin console (Admin / SuperAdmin only; all noindex) ──
  { path: 'admin', pathMatch: 'full', redirectTo: 'admin/companies' },
  {
    path: 'admin/companies',
    data: { noindex: true },
    canActivate: [roleGuard('Admin', 'SuperAdmin')],
    loadComponent: () => import('./features/admin/companies').then((m) => m.AdminCompaniesComponent),
  },
  {
    path: 'admin/users',
    data: { noindex: true },
    canActivate: [roleGuard('Admin', 'SuperAdmin')],
    loadComponent: () => import('./features/admin/users').then((m) => m.AdminUsersComponent),
  },
  {
    path: 'admin/reports',
    data: { noindex: true },
    canActivate: [roleGuard('Admin', 'SuperAdmin')],
    loadComponent: () => import('./features/admin/reports').then((m) => m.AdminReportsComponent),
  },

  { path: '**', redirectTo: 'search' },
];
