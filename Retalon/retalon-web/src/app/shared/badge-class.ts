const SUCCESS = new Set([
  'confirmed',
  'delivered',
  'succeeded',
  'received',
  'completed',
  'active'
]);

const PENDING = new Set([
  'pending',
  'requested',
  'ordered'
]);

const INFO = new Set([
  'processing',
  'shipped'
]);

const DANGER = new Set([
  'cancelled',
  'canceled',
  'failed',
  'discontinued',
  'inactive'
]);

export function badgeClass(status: string | null | undefined): string {
  const value = (status || '').toLowerCase();

  if (SUCCESS.has(value)) {
    return 'badge badge-success';
  }

  if (PENDING.has(value)) {
    return 'badge badge-pending';
  }

  if (INFO.has(value)) {
    return 'badge badge-info';
  }

  if (DANGER.has(value)) {
    return 'badge badge-danger';
  }

  return 'badge';
}
