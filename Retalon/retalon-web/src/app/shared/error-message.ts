/**
 * The Retalon API returns errors in a few different shapes depending on
 * which controller handled it:
 *  - { message: "..." } from most controllers' explicit error responses
 *  - a bare string from a few controllers (Payments, Procurement) that
 *    call BadRequest(ex.Message) directly
 *  - RFC 7807 ProblemDetails ({ title, detail, ... }) for unhandled
 *    exceptions and automatic model-validation failures
 * This pulls a displayable string out of any of those shapes, falling
 * back to a caller-supplied default rather than ever rendering an object.
 */
export function getErrorMessage(error: any, fallback: string): string {
  const body = error?.error;

  if (typeof body === 'string' && body.trim()) {
    return body;
  }

  if (typeof body?.message === 'string' && body.message.trim()) {
    return body.message;
  }

  if (typeof body?.detail === 'string' && body.detail.trim()) {
    return body.detail;
  }

  if (body?.errors && typeof body.errors === 'object') {
    const validationMessages = Object.values(body.errors).flat();

    if (validationMessages.length > 0) {
      return validationMessages.join(' ');
    }
  }

  return fallback;
}
