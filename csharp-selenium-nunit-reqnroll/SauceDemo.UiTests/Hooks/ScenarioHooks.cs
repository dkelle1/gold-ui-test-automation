using Allure.Net.Commons;
using Reqnroll;
using Reqnroll.BoDi;
using SauceDemo.UiTests.Configuration;
using SauceDemo.UiTests.Drivers;
using SauceDemo.UiTests.Support;
using SauceDemo.UiTests.Users;

namespace SauceDemo.UiTests.Hooks;

/// <summary>
/// Per-scenario lifecycle: acquire a distinct login user, then open a browser for it, then tear both
/// down in the opposite order. Reqnroll instantiates one <see cref="ScenarioHooks"/> per scenario (via
/// its scenario-scoped BoDi container) and reuses that same instance for every [BeforeScenario] and
/// [AfterScenario] hook on it, so state set in one hook method is available to later ones on plain
/// instance fields - no ThreadLocal or static state needed, which is what keeps this safe under
/// `[assembly: Parallelizable(ParallelScope.Children)]`.
///
/// <see cref="Users.UserLease"/> and <see cref="Drivers.BrowserSession"/> are also registered into the
/// scenario's <see cref="IObjectContainer"/> (via <see cref="RegisterForStepDefinitions"/>) so step
/// definition classes can request <c>IWebDriver</c> / <c>UserAccount</c> in their own constructors for
/// free.
/// </summary>
[Binding]
public sealed class ScenarioHooks
{
    private readonly ScenarioContext _scenarioContext;
    private readonly IObjectContainer _objectContainer;

    private UserLease? _userLease;
    private BrowserSession? _browserSession;

    public ScenarioHooks(ScenarioContext scenarioContext, IObjectContainer objectContainer)
    {
        _scenarioContext = scenarioContext;
        _objectContainer = objectContainer;
    }

    [BeforeScenario(Order = 10)]
    public void AcquireUser()
    {
        var taggedUser = TagHelpers.GetTaggedUser(_scenarioContext);

        _userLease = taggedUser is not null
            ? new UserLease(taggedUser)
            : TestRunHooks.UserPool.Acquire(TimeSpan.FromSeconds(ConfigurationLoader.Settings.UserAcquireTimeoutSeconds));

        AllureApi.AddTestParameter("user", _userLease.Account.Username);
        AllureApi.AddTestParameter("worker", NUnit.Framework.TestContext.CurrentContext.WorkerId ?? "single-threaded");
    }

    [BeforeScenario(Order = 20)]
    public void CreateBrowserSession()
    {
        var settings = ConfigurationLoader.Settings;
        _browserSession = new WebDriverFactory(settings).Create();
        _browserSession.Driver.Navigate().GoToUrl(settings.BaseUrl);
    }

    [BeforeScenario(Order = 30)]
    public void RegisterForStepDefinitions()
    {
        _objectContainer.RegisterInstanceAs(_browserSession!.Driver);
        _objectContainer.RegisterInstanceAs(_userLease!.Account);
    }

    [AfterScenario(Order = 10)]
    public void CaptureFailureEvidence()
    {
        if (_scenarioContext.TestError is not null && _browserSession is not null)
        {
            AllureAttachments.AttachFailureEvidence(_browserSession.Driver);
        }
    }

    [AfterScenario(Order = 20)]
    public void QuitBrowser() => _browserSession?.Dispose();

    [AfterScenario(Order = 30)]
    public void ReleaseUser() => _userLease?.Dispose();
}
