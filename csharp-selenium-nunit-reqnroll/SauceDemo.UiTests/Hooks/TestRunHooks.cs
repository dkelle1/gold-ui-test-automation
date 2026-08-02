using Reqnroll;
using SauceDemo.UiTests.Configuration;
using SauceDemo.UiTests.Support;
using SauceDemo.UiTests.Users;

namespace SauceDemo.UiTests.Hooks;

/// <summary>
/// [BeforeTestRun]/[AfterTestRun] run exactly once per test run - Reqnroll/NUnit guarantee this even
/// under parallel execution, blocking other workers until the hook completes - which makes this the
/// right (and only) place to build the one shared <see cref="UserPool"/> that every parallel worker
/// leases distinct accounts from.
/// </summary>
[Binding]
public static class TestRunHooks
{
    private static UserPool? _userPool;

    public static IUserPool UserPool => _userPool
        ?? throw new InvalidOperationException("UserPool was not initialised - [BeforeTestRun] should have run before any scenario.");

    [BeforeTestRun]
    public static void BeforeTestRun()
    {
        // Touch Settings here (not just lazily on first use) so a misconfigured BaseUrl fails the
        // whole run immediately instead of surfacing as a confusing per-scenario failure later.
        _ = ConfigurationLoader.Settings;

        _userPool = new UserPool(UserCatalog.PoolUsers);
    }

    [AfterTestRun]
    public static void AfterTestRun()
    {
        EnvironmentWriter.Write(ConfigurationLoader.Settings);
    }
}
