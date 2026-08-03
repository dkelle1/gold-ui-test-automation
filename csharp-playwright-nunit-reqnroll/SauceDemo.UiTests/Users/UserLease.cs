namespace SauceDemo.UiTests.Users;

/// <summary>Holds a leased <see cref="UserAccount"/>; disposing returns it to its pool exactly once.</summary>
public sealed class UserLease : IDisposable
{
    private readonly Action<UserAccount>? _release;
    private bool _disposed;

    internal UserLease(UserAccount account, Action<UserAccount> release)
    {
        Account = account;
        _release = release;
    }

    /// <summary>
    /// Wraps an account that was not leased from a pool (e.g. a tag-based `@user:` override that
    /// reads directly from <see cref="UserCatalog"/>). Disposing it is a no-op, since there is no
    /// pool to return the account to.
    /// </summary>
    public UserLease(UserAccount account)
    {
        Account = account;
        _release = null;
    }

    public UserAccount Account { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _release?.Invoke(Account);
    }
}
