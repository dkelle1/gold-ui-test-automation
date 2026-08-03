using SauceDemo.UiTests.Configuration;
using SauceDemo.UiTests.Users;

namespace SauceDemo.UiTests.Support;

/// <summary>
/// Writes Allure's environment.properties file into the allure-results directory so the report's
/// "Environment" tab shows what this run actually exercised (base URL, browser, worker/pool sizing,
/// CI context). Written relative to the test output directory, matching where Allure.Reqnroll itself
/// writes allure-results by default.
/// </summary>
public static class EnvironmentWriter
{
    public static void Write(TestSettings settings)
    {
        var resultsDirectory = Path.Combine(AppContext.BaseDirectory, "allure-results");
        Directory.CreateDirectory(resultsDirectory);

        var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

        var lines = new[]
        {
            $"BaseUrl={settings.BaseUrl}",
            $"Browser={settings.Browser}",
            $"Headless={settings.Headless}",
            $"Workers={ParallelSettings.MaxParallelWorkers}",
            $"UserPoolSize={UserCatalog.PoolUsers.Count}",
            $"PlaywrightVersion={typeof(Microsoft.Playwright.IPlaywright).Assembly.GetName().Version}",
            $"OS={Environment.OSVersion}",
            $"CI={isCi}",
            $"GITHUB_RUN_ID={Environment.GetEnvironmentVariable("GITHUB_RUN_ID")}"
        };

        File.WriteAllLines(Path.Combine(resultsDirectory, "environment.properties"), lines);
    }
}
