import { getErrorMessage } from './error-message';

describe('getErrorMessage', () => {
  it('returns the fallback when there is no error body', () => {
    expect(getErrorMessage({}, 'fallback')).toBe('fallback');
    expect(getErrorMessage(null, 'fallback')).toBe('fallback');
  });

  it('returns a { message } body as-is', () => {
    const error = { error: { message: 'Cart is empty.' } };
    expect(getErrorMessage(error, 'fallback')).toBe('Cart is empty.');
  });

  it('returns a bare string body, as used by Payments/Procurement controllers', () => {
    const error = { error: 'Insufficient inventory for product 17.' };
    expect(getErrorMessage(error, 'fallback')).toBe(
      'Insufficient inventory for product 17.'
    );
  });

  it('falls back to the ProblemDetails "detail" field for unhandled errors', () => {
    // This is the exact shape a live 500 from the API returned during
    // manual testing: an unhandled StripeException surfaced as ProblemDetails.
    const error = {
      error: {
        title: 'Internal Server Error',
        status: 500,
        detail: 'An unexpected error occurred.',
        instance: '/api/payments/confirm-test'
      }
    };

    expect(getErrorMessage(error, 'fallback')).toBe(
      'An unexpected error occurred.'
    );
  });

  it('joins ASP.NET Core automatic model-validation errors into one message', () => {
    const error = {
      error: {
        title: 'One or more validation errors occurred.',
        errors: {
          Email: ['The Email field is required.'],
          Password: ['The Password field must be at least 8 characters.']
        }
      }
    };

    expect(getErrorMessage(error, 'fallback')).toBe(
      'The Email field is required. The Password field must be at least 8 characters.'
    );
  });

  it('never returns "[object Object]" for an unrecognized shape', () => {
    const error = { error: { somethingUnexpected: true } };
    expect(getErrorMessage(error, 'fallback')).toBe('fallback');
  });
});
