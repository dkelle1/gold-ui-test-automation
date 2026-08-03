using NUnit.Framework;
using SauceDemo.UiTests.Users;

namespace SauceDemo.UiTests.Tests;

/// <summary>
/// Plain NUnit unit tests (no Reqnroll, no browser) covering the one genuinely novel piece of this
/// framework: that <see cref="UserPool"/> never hands the same account to two concurrent leases, and
/// fails predictably rather than deadlocking when misconfigured. Runs serially (it deliberately
/// exhausts a small pool), independent of the parallel BDD scenarios.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class UserPoolTests
{
    private static UserAccount[] ThreeTestUsers() =>
    [
        new UserAccount("user-a", "pw", UserCapabilities.CanLogIn),
        new UserAccount("user-b", "pw", UserCapabilities.CanLogIn),
        new UserAccount("user-c", "pw", UserCapabilities.CanLogIn)
    ];

    [Test]
    public void Acquire_ReturnsDistinctAccounts_ForConcurrentLeases()
    {
        var pool = new UserPool(ThreeTestUsers());

        using var lease1 = pool.Acquire(TimeSpan.FromSeconds(1));
        using var lease2 = pool.Acquire(TimeSpan.FromSeconds(1));
        using var lease3 = pool.Acquire(TimeSpan.FromSeconds(1));

        var usernames = new[] { lease1.Account.Username, lease2.Account.Username, lease3.Account.Username };
        Assert.That(usernames.Distinct().Count(), Is.EqualTo(3), "Every concurrently-held lease must hold a different account.");
    }

    [Test]
    public void Acquire_TimesOut_WhenPoolIsExhausted()
    {
        var pool = new UserPool(ThreeTestUsers());
        using var lease1 = pool.Acquire(TimeSpan.FromSeconds(1));
        using var lease2 = pool.Acquire(TimeSpan.FromSeconds(1));
        using var lease3 = pool.Acquire(TimeSpan.FromSeconds(1));

        Assert.Throws<TimeoutException>(() => pool.Acquire(TimeSpan.FromMilliseconds(200)));
    }

    [Test]
    public void Dispose_ReturnsAccountToPool_ForReuseByAnotherLease()
    {
        var pool = new UserPool(ThreeTestUsers());

        var lease1 = pool.Acquire(TimeSpan.FromSeconds(1));
        lease1.Dispose();

        using var lease2 = pool.Acquire(TimeSpan.FromSeconds(1));
        using var lease3 = pool.Acquire(TimeSpan.FromSeconds(1));

        // A fourth concurrent acquire only succeeds if releasing lease1 actually replenished the pool.
        Assert.DoesNotThrow(() => pool.Acquire(TimeSpan.FromSeconds(1)).Dispose());
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var pool = new UserPool(ThreeTestUsers());
        var lease = pool.Acquire(TimeSpan.FromSeconds(1));

        lease.Dispose();
        Assert.DoesNotThrow(() => lease.Dispose());

        Assert.That(pool.AvailableCount, Is.EqualTo(3), "Double-dispose must not return the account to the pool twice.");
    }

    [Test]
    public void Constructor_Throws_WhenPoolSmallerThanMaxParallelWorkers()
    {
        var tooFewUsers = new[] { new UserAccount("solo", "pw", UserCapabilities.CanLogIn) };

        Assert.Throws<ArgumentException>(() => new UserPool(tooFewUsers));
    }

    [Test]
    public void Constructor_Throws_WhenPoolIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new UserPool(Array.Empty<UserAccount>()));
    }
}
