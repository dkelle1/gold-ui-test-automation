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
public sealed class WebDriverFactory : IWebDriverFactory
{
    private readonly TestSettings _settings;

    public WebDriverFactory(TestSettings settings)
    {
        _settings = settings;
    }

    public BrowserSession Create()
    {
        (DriverOptions options, string? userDataDir) = _settings.Browser switch
        {
            BrowserType.Chrome => BuildChromeOptions(),
            BrowserType.Edge => BuildEdgeOptions(),
            BrowserType.Firefox => (BuildFirefoxOptions(), null),
            _ => throw new NotSupportedException($"Unsupported browser: {_settings.Browser}")
        };

        options.CommandTimeout = TimeSpan.FromSeconds(_settings.CommandTimeoutSeconds);

        var driver = string.IsNullOrWhiteSpace(_settings.RemoteUrl)
            ? CreateLocalDriver(options)
            : new RemoteWebDriver(new Uri(_settings.RemoteUrl), options);

        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(_settings.PageLoadTimeoutSeconds);
        driver.Manage().Window.Maximize();

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
            options.AddArgument("-headless");
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
