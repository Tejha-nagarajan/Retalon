namespace Retalon.Models.Enums;

public enum SecurityEventType
{
    FailedLogin,
    AccountLocked,
    UnauthorizedAccess,
    RateLimitExceeded,
    TokenRevoked,
    SuspiciousActivity
}