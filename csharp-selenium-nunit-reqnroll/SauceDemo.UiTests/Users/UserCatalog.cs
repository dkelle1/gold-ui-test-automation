namespace SauceDemo.UiTests.Users;

/// <summary>
/// The fixed roster of saucedemo.com accounts (all share the password "secret_sauce"). Login
/// credentials are never faked with Bogus — these accounts must exist on the real demo site.
///
/// Only the three checkout-capable accounts (<see cref="PoolUsers"/>) are safe to hand out from the
/// parallel <see cref="UserPool"/>: the other three are deliberately broken in different ways
/// (locked out entirely, or broken partway through checkout) and would make positive-path scenarios
/// flaky if leased interchangeably. They are instead targeted directly via the `@user:&lt;username&gt;`
/// tag in scenarios that specifically exercise that behaviour.
/// </summary>
public static class UserCatalog
{
    private const string SharedPassword = "secret_sauce";

    /// <summary>Baseline account: login and checkout both work normally.</summary>
    public static readonly UserAccount StandardUser =
        new("standard_user", SharedPassword, UserCapabilities.CanLogIn | UserCapabilities.CanCompleteCheckout);

    /// <summary>Login and checkout work, but the UI has ~5s artificial delays — needs generous waits.</summary>
    public static readonly UserAccount PerformanceGlitchUser =
        new("performance_glitch_user", SharedPassword, UserCapabilities.CanLogIn | UserCapabilities.CanCompleteCheckout);

    /// <summary>Login and checkout work; the site has cosmetic/layout defects only.</summary>
    public static readonly UserAccount VisualUser =
        new("visual_user", SharedPassword, UserCapabilities.CanLogIn | UserCapabilities.CanCompleteCheckout);

    /// <summary>Logs in fine, but the checkout last-name field is broken - cannot complete a purchase.</summary>
    public static readonly UserAccount ProblemUser =
        new("problem_user", SharedPassword, UserCapabilities.CanLogIn);

    /// <summary>Logs in fine, but fails on cart removal / checkout completion.</summary>
    public static readonly UserAccount ErrorUser =
        new("error_user", SharedPassword, UserCapabilities.CanLogIn);

    /// <summary>Login is rejected outright by design.</summary>
    public static readonly UserAccount LockedOutUser =
        new("locked_out_user", SharedPassword, UserCapabilities.None);

    /// <summary>All six accounts, keyed by username, for tag-based lookup (e.g. `@user:problem_user`).</summary>
    public static readonly IReadOnlyDictionary<string, UserAccount> AllUsers =
        new[] { StandardUser, PerformanceGlitchUser, VisualUser, ProblemUser, ErrorUser, LockedOutUser }
            .ToDictionary(u => u.Username);

    /// <summary>
    /// The accounts safe to lease from the parallel pool. Size must be at least
    /// <see cref="Configuration.ParallelSettings.MaxParallelWorkers"/> - <see cref="UserPool"/>'s
    /// constructor enforces this at process start.
    /// </summary>
    public static readonly IReadOnlyList<UserAccount> PoolUsers = new[]
    {
        StandardUser, PerformanceGlitchUser, VisualUser
    };

    public static UserAccount ByUsername(string username)
    {
        if (!AllUsers.TryGetValue(username, out var account))
        {
            throw new KeyNotFoundException(
                $"No saucedemo user named '{username}' in the catalog. Known users: {string.Join(", ", AllUsers.Keys)}.");
        }

        return account;
    }
}
