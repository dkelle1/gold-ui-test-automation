namespace SauceDemo.UiTests.Users;

public interface IUserPool
{
    /// <summary>
    /// Blocks until a distinct account is available (or <paramref name="timeout"/> elapses), then
    /// leases it exclusively to the caller. Dispose the returned lease to return the account.
    /// </summary>
    UserLease Acquire(TimeSpan timeout);
}
