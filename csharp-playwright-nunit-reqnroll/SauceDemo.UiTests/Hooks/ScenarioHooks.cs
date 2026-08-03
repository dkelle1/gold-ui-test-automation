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
/// definition classes can request <c>IPage</c> / <c>UserAccount</c> in their own constructors for free.
///
/// One Reqnroll behaviour these hooks are written around: hooks of the same type run in Order, and the
/// first one to throw stops the rest of that type from running at all. That cuts both ways here.
/// It is why the null-forgiving operators in <see cref="RegisterForStepDefinitions"/> are safe - it
/// only ever runs if the two hooks before it succeeded. But it also means a [AfterScenario] hook that
/// throws would skip the later ones that release this scenario's resources, so each cleanup hook below
/// swallows and reports its own failures instead of propagating them. ([AfterScenario] hooks do still
/// run when a [BeforeScenario] hook failed, which is what lets the cleanup below be null-guarded rather
/// than assumed-initialised.)
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
    public async Task CreateBrowserSessionAsync()
    {
        var settings = ConfigurationLoader.Settings;
        _browserSession = await new PlaywrightFactory(settings).CreateAsync();
        await _browserSession.Page.GotoAsync(settings.BaseUrl);
    }

    [BeforeScenario(Order = 30)]
    public void RegisterForStepDefinitions()
    {
        _objectContainer.RegisterInstanceAs(_browserSession!.Page);
        _objectContainer.RegisterInstanceAs(_userLease!.Account);
    }

    [AfterScenario(Order = 10)]
    public async Task CaptureFailureEvidenceAsync()
    {
        if (_scenarioContext.TestError is not null && _browserSession is not null)
        {
            await AllureAttachments.AttachFailureEvidenceAsync(_browserSession);
        }
    }

    [AfterScenario(Order = 20)]
    public async Task QuitBrowserAsync()
    {
        if (_browserSession is null)
        {
            return;
        }

        try
        {
            await _browserSession.DisposeAsync();
        }
        catch (Exception ex)
        {
            // A failed browser teardown must not abort the remaining [AfterScenario] hooks: Reqnroll
            // stops firing them at the first one that throws, and the next one is what returns this
            // scenario's pooled login user. Leaking one browser process is recoverable; leaking the
            // lease exhausts the pool and makes every later scenario block for the full
            // UserAcquireTimeoutSeconds before failing with an unrelated-looking error.
            NUnit.Framework.TestContext.Out.WriteLine($"Browser teardown failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [AfterScenario(Order = 30)]
    public void ReleaseUser() => _userLease?.Dispose();
}
