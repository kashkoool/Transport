import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';
import { AuthService } from './core/auth/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    // errorInterceptor is outermost so it only toasts errors the auth interceptor (inner, closer
    // to the backend) didn't already resolve by silently refreshing + retrying a 401.
    provideHttpClient(withInterceptors([errorInterceptor, authInterceptor])),
    // Restore a session from the HttpOnly refresh cookie before the first route renders, so a
    // returning user lands signed-in and guarded routes work on a hard refresh.
    provideAppInitializer(() => firstValueFrom(inject(AuthService).restoreSession())),
  ],
};
