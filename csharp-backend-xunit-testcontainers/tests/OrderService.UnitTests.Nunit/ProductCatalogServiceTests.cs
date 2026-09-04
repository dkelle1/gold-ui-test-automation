using NSubstitute;
using NUnit.Framework;
using OrderService.Api.Contracts;
using OrderService.Api.Domain;
using OrderService.Api.Services;

namespace OrderService.UnitTests.Nunit;

/// <summary>Product creation, tested with NUnit + NSubstitute. This is a different slice from the xUnit
/// order-placement tests on purpose: the same stack lists both runners, so each drives a real part of
/// the system rather than one being a copy of the other. The pure rules (SKU format, price, stock) are
/// checked via a case source; the duplicate-SKU rule needs the repository, supplied here as a mock.</summary>
[TestFixture]
public class ProductCatalogServiceTests
{
    private IProductRepository _products = null!;
    private IUnitOfWork _unitOfWork = null!;

    [SetUp]
    public void SetUp()
    {
        _products = Substitute.For<IProductRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        // Default: no existing product with the requested SKU, so the duplicate check passes.
        _products.GetBySkuAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Product?)null);
    }

    private ProductCatalogService CreateSut() => new(_products, _unitOfWork);

    [Test]
    public async Task CreateProduct_WithValidRequest_PersistsAndSucceeds()
    {
        var request = new CreateProductRequest("ABC-1234", "Backpack", 100m, 10);

        var result = await CreateSut().CreateProductAsync(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Product, Is.Not.Null);
        Assert.That(result.Product!.Sku, Is.EqualTo("ABC-1234"));
        await _products.Received(1).AddAsync(Arg.Is<Product>(p => p.Sku == "ABC-1234"), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static IEnumerable<TestCaseData> InvalidRequests()
    {
        yield return new TestCaseData(new CreateProductRequest("abc-1234", "Backpack", 100m, 10), ProductError.InvalidSku)
            .SetName("malformed SKU is rejected");
        yield return new TestCaseData(new CreateProductRequest("ABC-1234", "Backpack", 0m, 10), ProductError.InvalidPrice)
            .SetName("zero price is rejected");
        yield return new TestCaseData(new CreateProductRequest("ABC-1234", "Backpack", -5m, 10), ProductError.InvalidPrice)
            .SetName("negative price is rejected");
        yield return new TestCaseData(new CreateProductRequest("ABC-1234", "Backpack", 100m, -1), ProductError.InvalidStock)
            .SetName("negative stock is rejected");
    }

    [TestCaseSource(nameof(InvalidRequests))]
    public async Task CreateProduct_WithInvalidRequest_FailsWithoutPersisting(
        CreateProductRequest request, ProductError expected)
    {
        var result = await CreateSut().CreateProductAsync(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Is.EqualTo(expected));
        await _products.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateProduct_WithDuplicateSku_FailsAndPersistsNothing()
    {
        _products
            .GetBySkuAsync("ABC-1234", Arg.Any<CancellationToken>())
            .Returns(new Product { Sku = "ABC-1234", Name = "Existing", Price = 1m, StockQuantity = 1 });

        var request = new CreateProductRequest("ABC-1234", "Backpack", 100m, 10);
        var result = await CreateSut().CreateProductAsync(request, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(ProductError.DuplicateSku));
        await _products.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }
}
