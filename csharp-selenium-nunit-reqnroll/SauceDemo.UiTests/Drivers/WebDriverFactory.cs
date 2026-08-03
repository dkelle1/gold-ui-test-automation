using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;
using SauceDemo.UiTests.Configuration;

namespace SauceDemo.UiTests.Drivers;

/// <summary>
/// Builds browser sessions from <see cref="TestSettings"/>. Relies on Selenium Manager (bundled with
/// Selenium.WebDriver 4.6+) to resolve driver binaries automatically, so no ChromeDriver/GeckoDriver
/// NuGet package or manual PATH setup is needed. When <see cref="TestSettings.RemoteUrl"/> is set,
/// sessions are created against a remote endpoint (e.g. a Selenium Grid / selenium/standalone-chrome
/// container) instead of a local browser, with no other code changes required.
/// </summary>
public sealed class WebDriverFactory
{
    private readonly TestSettings _settings;

    public WebDriverFactory(TestSettings settings)
    {
        _settings = settings;
    }

    public BrowserSession Create()
    {
        // A plain switch statement, not a switch expression: each case deconstructs its own method's
        // concrete tuple type directly, so there is no cross-arm "common type" for the compiler to
        // infer - Chrome/Edge/Firefox each return an unrelated shape and that's fine here.
        DriverOptions options;
        string? userDataDir = null;

        switch (_settings.Browser)
        {
            case BrowserType.Chrome:
                (ChromeOptions chromeOptions, userDataDir) = BuildChromeOptions();
                options = chromeOptions;
                break;
            case BrowserType.Edge:
                (EdgeOptions edgeOptions, userDataDir) = BuildEdgeOptions();
                options = edgeOptions;
                break;
            case BrowserType.Firefox:
                options = BuildFirefoxOptions();
                break;
            default:
                throw new NotSupportedException($"Unsupported browser: {_settings.Browser}");
        }

        // Uses the plain 2-arg RemoteWebDriver(Uri, DriverOptions) constructor. Both local and remote
        // sessions use Selenium's built-in default command timeout (60s).
        var driver = string.IsNullOrWhiteSpace(_settings.RemoteUrl)
            ? CreateLocalDriver(options)
            : new RemoteWebDriver(new Uri(_settings.RemoteUrl), options);

        try
        {
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(_settings.PageLoadTimeoutSeconds);

            if (!_settings.Headless)
            {
                // Headless browsers are sized by the explicit window-size arguments below instead.
                // Maximize() in headless Chrome/Edge does not enlarge anything - it resizes the window
                // to the headless environment's virtual screen (800x600), silently *undoing*
                // --window-size=1920,1080 and dropping every CI run into saucedemo's narrow/mobile
                // layout. (Confirmed from CI: every failure screenshot was 800x457 despite the flag.)
                driver.Manage().Window.Maximize();
            }
        }
        catch
        {
            // The session already exists at this point - if post-launch setup fails, quit it rather
            // than leaking a live browser process/remote session that nothing will ever dispose.
            driver.Quit();
            throw;
        }

        return new BrowserSession(driver, userDataDir);
    }

    private (ChromeOptions Options, string UserDataDir) BuildChromeOptions()
    {
        var userDataDir = CreateUserDataDir();
        var options = new ChromeOptions();
        options.AddArgument($"--user-data-dir={userDataDir}");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");

        if (_settings.Headless)
        {
            options.AddArgument("--headless=new");
            options.AddArgument("--disable-gpu");
        }

        return (options, userDataDir);
    }

    private (EdgeOptions Options, string UserDataDir) BuildEdgeOptions()
    {
        var userDataDir = CreateUserDataDir();
        var options = new EdgeOptions();
        options.AddArgument($"--user-data-dir={userDataDir}");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");

        if (_settings.Headless)
        {
            options.AddArgument("--headless=new");
        }

        return (options, userDataDir);
    }

    private FirefoxOptions BuildFirefoxOptions()
    {
        var options = new FirefoxOptions();

        if (_settings.Headless)
        {
            // Mirrors Chrome/Edge's --window-size: Window.Maximize() doesn't reliably resize a
            // headless viewport, so headless Firefox would otherwise default to a small window.
            options.AddArgument("-headless");
            options.AddArgument("-width=1920");
            options.AddArgument("-height=1080");
        }

        return options;
    }

    private static IWebDriver CreateLocalDriver(DriverOptions options) => options switch
    {
        ChromeOptions chromeOptions => new ChromeDriver(chromeOptions),
        FirefoxOptions firefoxOptions => new FirefoxDriver(firefoxOptions),
        EdgeOptions edgeOptions => new EdgeDriver(edgeOptions),
        _ => throw new NotSupportedException($"Unsupported driver options type: {options.GetType()}")
    };

    private static string CreateUserDataDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "saucedemo-uitests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
