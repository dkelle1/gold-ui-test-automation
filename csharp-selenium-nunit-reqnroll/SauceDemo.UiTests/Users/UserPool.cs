using System.Collections.Concurrent;
using SauceDemo.UiTests.Configuration;

namespace SauceDemo.UiTests.Users;

/// <summary>
/// Process-wide pool of login-capable users, one leased per running scenario, so concurrently
/// executing NUnit workers never race to use the same saucedemo account. Backed by a
/// <see cref="BlockingCollection{T}"/>: <see cref="Acquire"/> blocks (up to a timeout) until an
/// account is available, and disposing the returned <see cref="UserLease"/> returns it.
///
/// The pool must contain at least <see cref="ParallelSettings.MaxParallelWorkers"/> accounts - the
/// constructor asserts this so a misconfigured pool fails fast at test-run start instead of causing
/// mysterious timeouts partway through a run.
/// </summary>
public class UserPool : IUserPool
{
    private readonly BlockingCollection<UserAccount> _available;

    public UserPool(IReadOnlyList<UserAccount> users)
    {
        if (users.Count == 0)
        {
            throw new ArgumentException("User pool must contain at least one account.", nameof(users));
        }

        if (users.Count < ParallelSettings.MaxParallelWorkers)
        {
            throw new ArgumentException(
                $"User pool has {users.Count} account(s) but ParallelSettings.MaxParallelWorkers is " +
                $"{ParallelSettings.MaxParallelWorkers}. Every parallel worker needs its own account, or " +
                "scenarios will block waiting for one to free up.",
                nameof(users));
        }

        _available = new BlockingCollection<UserAccount>(new ConcurrentQueue<UserAccount>(users));
    }

    /// <summary>Snapshot of how many accounts are currently free. For diagnostics/tests only.</summary>
    public int AvailableCount => _available.Count;

    public UserLease Acquire(TimeSpan timeout)
    {
        if (!_available.TryTake(out var account, timeout))
        {
            throw new TimeoutException(
                $"Timed out after {timeout} waiting for a free saucedemo user ({_available.Count} " +
                "currently available). This usually means NUnit's worker count (LevelOfParallelism / " +
                $"-- NUnit.NumberOfTestWorkers) exceeds the pool size ({ParallelSettings.MaxParallelWorkers}), " +
                "so more scenarios are running concurrently than there are distinct users to log in with.");
        }

        return new UserLease(account, Release);
    }

    private void Release(UserAccount account) => _available.Add(account);
}
