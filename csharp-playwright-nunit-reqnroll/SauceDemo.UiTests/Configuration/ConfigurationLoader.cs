using Microsoft.Extensions.Configuration;

namespace SauceDemo.UiTests.Configuration;

/// <summary>
/// Loads TestSettings from appsettings.json, layers appsettings.ci.json when running under CI
/// (detected via the standard `CI=true` env var that GitHub Actions and most CI systems set), and
/// finally applies TestSettings__* environment variable overrides on top. Cached for the process
/// lifetime since settings never change mid-run.
/// </summary>
public static class ConfigurationLoader
{
    private static readonly Lazy<TestSettings> CachedSettings = new(Load, isThreadSafe: true);

    public static TestSettings Settings => CachedSettings.Value;

    private static TestSettings Load()
    {
        var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        if (isCi)
        {
            builder.AddJsonFile("appsettings.ci.json", optional: true, reloadOnChange: false);
        }

        builder.AddEnvironmentVariables();

        var configuration = builder.Build();

        var settings = new TestSettings();
        configuration.GetSection("TestSettings").Bind(settings);

        Validate(settings);

        return settings;
    }

    private static void Validate(TestSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            throw new InvalidOperationException("TestSettings:BaseUrl must be configured (appsettings.json or the TestSettings__BaseUrl env var).");
        }

        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"TestSettings:BaseUrl is not a valid absolute URL: '{settings.BaseUrl}'.");
        }
    }
}
