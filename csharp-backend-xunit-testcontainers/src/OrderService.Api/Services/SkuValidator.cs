using System.Text.RegularExpressions;

namespace OrderService.Api.Services;

/// <summary>Validates a product SKU against the house format <c>AAA-9999</c> (three uppercase letters,
/// a hyphen, four digits). A pure function with no dependencies - which is exactly why the NUnit suite
/// pins it down with a table of cases rather than mocking anything.</summary>
public static partial class SkuValidator
{
    [GeneratedRegex("^[A-Z]{3}-[0-9]{4}$")]
    private static partial Regex SkuPattern();

    public static bool IsValid(string? sku) => !string.IsNullOrWhiteSpace(sku) && SkuPattern().IsMatch(sku);
}
