namespace SauceDemo.UiTests.Drivers;

/// <summary>
/// Playwright's three bundled engines. Deliberately not named Chrome/Firefox/Edge (as the Selenium
/// sibling framework's enum is): Playwright downloads and drives these engines directly rather than
/// automating a locally-installed vendor browser, so "Chrome" and "Edge" aren't distinct download
/// targets here - both are Chromium under the hood, and selecting either one via Playwright's
/// `Channel` option requires that vendor browser to already be installed on the machine, which isn't
/// guaranteed in CI or a fresh dev box the way the bundled engines are. WebKit is a genuine gain over
/// the Selenium sibling: it runs Safari's engine on Linux CI with no macOS machine required.
/// </summary>
public enum BrowserType
{
    Chromium,
    Firefox,
    WebKit
}
