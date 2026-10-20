import { Routes } from '@angular/router';
import { guestGuard, roleGuard } from './core/guards/auth.guards';
import { Roles } from './core/models/auth.models';

// Every page is lazy-loaded. Guards only improve the experience: the API enforces access on every call.
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'EthioServe',
    loadComponent: () => import('./features/marketplace/home').then((m) => m.Home),
  },
  {
    path: 'login',
    title: 'Sign in | EthioServe',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: 'register',
    title: 'Create account | EthioServe',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register').then((m) => m.Register),
  },
  {
    path: 'forgot-password',
    title: 'Forgot password | EthioServe',
    loadComponent: () => import('./features/auth/forgot-password').then((m) => m.ForgotPassword),
  },
  {
    path: 'reset-password',
    title: 'Reset password | EthioServe',
    loadComponent: () => import('./features/auth/reset-password').then((m) => m.ResetPassword),
  },
  {
    path: 'customer',
    title: 'My dashboard | EthioServe',
    canActivate: [roleGuard(Roles.Customer)],
    loadComponent: () => import('./features/customer/customer-dashboard').then((m) => m.CustomerDashboard),
  },
  {
    path: 'provider',
    title: 'Provider dashboard | EthioServe',
    canActivate: [roleGuard(Roles.Provider)],
    loadComponent: () => import('./features/provider/provider-dashboard').then((m) => m.ProviderDashboard),
  },
  {
    path: 'admin',
    title: 'Admin | EthioServe',
    canActivate: [roleGuard(Roles.Admin)],
    loadComponent: () => import('./features/admin/admin-dashboard').then((m) => m.AdminDashboard),
  },
    {
    path: 'providers',
    title: 'Find providers | EthioServe',
    loadComponent: () => import('./features/marketplace/providers').then((m) => m.Providers),
  },
  {
    path: 'providers/:id',
    title: 'Provider | EthioServe',
    loadComponent: () => import('./features/marketplace/provider-detail').then((m) => m.ProviderDetail),
  },
  {
    path: 'forbidden',
    title: 'Access denied | EthioServe',
    loadComponent: () => import('./features/errors/forbidden').then((m) => m.Forbidden),
  },
  {
    path: '**',
    title: 'Page not found | EthioServe',
    loadComponent: () => import('./features/errors/not-found').then((m) => m.NotFound),
  },
];