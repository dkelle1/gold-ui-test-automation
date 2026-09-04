using OrderService.Api.Contracts;
using OrderService.Api.Domain;

namespace OrderService.Api.Services;

public enum ProductError
{
    InvalidSku,
    InvalidPrice,
    InvalidStock,
    DuplicateSku,
}

public sealed class ProductResult
{
    private ProductResult(bool isSuccess, Product? product, ProductError? error, string? message)
    {
        IsSuccess = isSuccess;
        Product = product;
        Error = error;
        Message = message;
    }

    public bool IsSuccess { get; }
    public Product? Product { get; }
    public ProductError? Error { get; }
    public string? Message { get; }

    public static ProductResult Success(Product product) => new(true, product, null, null);

    public static ProductResult Fail(ProductError error, string message) => new(false, null, error, message);
}

public interface IProductCatalogService
{
    Task<ProductResult> CreateProductAsync(CreateProductRequest request, CancellationToken ct);
}

/// <summary>Product creation with validation. This is the slice the NUnit suite owns: the SKU-format and
/// price/stock rules are pure (tested directly), and the duplicate-SKU rule needs the repository, which
/// the NUnit tests supply as an NSubstitute mock - so both runners exercise NSubstitute, on different
/// code.</summary>
public sealed class ProductCatalogService : IProductCatalogService
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public ProductCatalogService(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductResult> CreateProductAsync(CreateProductRequest request, CancellationToken ct)
    {
        if (!SkuValidator.IsValid(request.Sku))
        {
            return ProductResult.Fail(ProductError.InvalidSku, "SKU must match the format AAA-9999.");
        }

        if (request.Price <= 0)
        {
            return ProductResult.Fail(ProductError.InvalidPrice, "Price must be greater than zero.");
        }

        if (request.StockQuantity < 0)
        {
            return ProductResult.Fail(ProductError.InvalidStock, "Stock quantity cannot be negative.");
        }

        if (await _products.GetBySkuAsync(request.Sku, ct) is not null)
        {
            return ProductResult.Fail(ProductError.DuplicateSku, $"A product with SKU {request.Sku} already exists.");
        }

        var product = new Product
        {
            Sku = request.Sku,
            Name = request.Name,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
        };

        await _products.AddAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ProductResult.Success(product);
    }
}
