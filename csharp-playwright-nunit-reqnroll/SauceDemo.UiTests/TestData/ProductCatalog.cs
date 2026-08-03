namespace SauceDemo.UiTests.TestData;

/// <summary>Fixed saucedemo.com product names - reference data, not randomized, since it must match the real UI.</summary>
public static class ProductCatalog
{
    public const string SauceLabsBackpack = "Sauce Labs Backpack";
    public const string SauceLabsBikeLight = "Sauce Labs Bike Light";
    public const string SauceLabsBoltTShirt = "Sauce Labs Bolt T-Shirt";
    public const string SauceLabsFleeceJacket = "Sauce Labs Fleece Jacket";
    public const string SauceLabsOnesie = "Sauce Labs Onesie";
    public const string TestAllTheThingsTShirt = "Test.allTheThings() T-Shirt (Red)";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SauceLabsBackpack,
        SauceLabsBikeLight,
        SauceLabsBoltTShirt,
        SauceLabsFleeceJacket,
        SauceLabsOnesie,
        TestAllTheThingsTShirt
    };
}
