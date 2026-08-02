using SauceDemo.UiTests.Drivers;

namespace SauceDemo.UiTests.Configuration;

/// <summary>Bound from the "TestSettings" section of appsettings*.json, overridable via TestSettings__* env vars.</summary>
public sealed class TestSettings
{
    public string BaseUrl { get; init; } = "https://www.saucedemo.com/";

    public BrowserType Browser { get; init; } = BrowserType.Chrome;

    public bool Headless { get; init; }

    /// <summary>Selenium Grid / remote WebDriver endpoint. Null means launch a local browser.</summary>
    public string? RemoteUrl { get; init; }

    public int CommandTimeoutSeconds { get; init; } = 60;

    public int ExplicitWaitSeconds { get; init; } = 20;

    public int PageLoadTimeoutSeconds { get; init; } = 30;
}
