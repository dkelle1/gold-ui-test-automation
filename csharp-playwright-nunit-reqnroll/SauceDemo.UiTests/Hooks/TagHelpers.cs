using Reqnroll;
using SauceDemo.UiTests.Users;

namespace SauceDemo.UiTests.Hooks;

/// <summary>Parses typed data out of Gherkin `@tag` values on the current scenario.</summary>
public static class TagHelpers
{
    private const string UserTagPrefix = "user:";

    /// <summary>
    /// Returns the account named by a `@user:&lt;username&gt;` tag on the scenario, if present. Lets a
    /// scenario bypass the parallel pool and target one specific (often deliberately broken) saucedemo
    /// account directly, e.g. `@user:locked_out_user`.
    /// </summary>
    public static UserAccount? GetTaggedUser(ScenarioContext scenarioContext)
    {
        var tag = scenarioContext.ScenarioInfo.Tags
            .FirstOrDefault(t => t.StartsWith(UserTagPrefix, StringComparison.OrdinalIgnoreCase));

        return tag is null ? null : UserCatalog.ByUsername(tag[UserTagPrefix.Length..]);
    }
}
