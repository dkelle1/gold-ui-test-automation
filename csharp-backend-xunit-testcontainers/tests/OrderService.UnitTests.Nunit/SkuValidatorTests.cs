using NUnit.Framework;
using OrderService.Api.Services;

namespace OrderService.UnitTests.Nunit;

/// <summary>NUnit's table-driven style is a natural fit for a pure validator: one method, a row per
/// case, using the <c>[TestCase]</c> attribute and NUnit's constraint-model assertions
/// (<c>Assert.That(..., Is.EqualTo(...))</c>).</summary>
[TestFixture]
public class SkuValidatorTests
{
    [TestCase("ABC-1234", true, TestName = "canonical form is valid")]
    [TestCase("XYZ-0000", true, TestName = "all-zero digits are valid")]
    [TestCase("abc-1234", false, TestName = "lowercase letters are rejected")]
    [TestCase("AB-1234", false, TestName = "two letters are rejected")]
    [TestCase("ABCD-1234", false, TestName = "four letters are rejected")]
    [TestCase("ABC-123", false, TestName = "three digits are rejected")]
    [TestCase("ABC-12345", false, TestName = "five digits are rejected")]
    [TestCase("ABC1234", false, TestName = "missing hyphen is rejected")]
    [TestCase("ABC-12A4", false, TestName = "letter in the digit block is rejected")]
    [TestCase(" ABC-1234", false, TestName = "leading whitespace is rejected")]
    [TestCase("", false, TestName = "empty string is rejected")]
    [TestCase(null, false, TestName = "null is rejected")]
    public void IsValid_ReturnsExpected(string? sku, bool expected)
    {
        Assert.That(SkuValidator.IsValid(sku), Is.EqualTo(expected));
    }
}
