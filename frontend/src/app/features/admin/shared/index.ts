export * from './admin-labels';
export * from './admin-status.component';
export * from './admin-pager.component';
export * from './admin-workspace.component';
// The confirm dialog grew up and moved to the design system; the admin tabs still import it from here.
export { ConfirmDialogComponent } from '../../../shared/components';
export type { ConfirmRequest } from '../../../shared/components';
