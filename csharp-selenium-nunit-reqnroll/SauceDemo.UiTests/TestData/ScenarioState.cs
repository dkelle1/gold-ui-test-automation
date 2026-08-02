namespace SauceDemo.UiTests.TestData;

/// <summary>
/// Scenario-scoped state shared between step definition classes. Has a parameterless constructor, so
/// Reqnroll's DI container (BoDi) auto-constructs one fresh instance per scenario and injects it into
/// any step-definition constructor that asks for it - the typed alternative to passing data between
/// steps via <c>ScenarioContext</c> string keys, and safe under parallel execution since every
/// scenario gets its own instance.
/// </summary>
public sealed class ScenarioState
{
    public List<string> ProductsInCart { get; } = [];

    public CheckoutInfo? CheckoutInfo { get; set; }
}
