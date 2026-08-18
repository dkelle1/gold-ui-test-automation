using NSubstitute;
using OrderService.Api.Contracts;
using OrderService.Api.Domain;
using OrderService.Api.Services;
using Xunit;

namespace OrderService.UnitTests.Xunit;

/// <summary>Unit tests for the order-placement business logic, in complete isolation from a database.
/// The repositories, unit of work and clock are NSubstitute mocks, so these tests are fast and pin down
/// exactly one thing: the rules. Whether those rules also work against a real SQL Server is the
/// integration suite's job.</summary>
public class OrderPlacementServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc);

    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public OrderPlacementServiceTests()
    {
        _clock.UtcNow.Returns(FixedNow);
    }

    private OrderPlacementService CreateSut() => new(_products, _orders, _unitOfWork, _clock);

    private void GivenProducts(params Product[] products) =>
        _products
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(products.ToList());

    private static Product Product(int id, decimal price, int stock) =>
        new() { Id = id, Sku = $"SKU-{id:0000}", Name = $"Product {id}", Price = price, StockQuantity = stock };

    private static PlaceOrderRequest Request(params (int productId, int quantity)[] items) =>
        new("buyer@example.com", items.Select(i => new OrderItemRequest(i.productId, i.quantity)).ToList());

    [Fact]
    public async Task PlaceOrder_WithNoItems_FailsAsEmptyOrderAndPersistsNothing()
    {
        var result = await CreateSut().PlaceOrderAsync(Request(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderError.EmptyOrder, result.Error);
        await _orders.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task PlaceOrder_WithNonPositiveQuantity_FailsAsInvalidQuantity(int quantity)
    {
        GivenProducts(Product(1, price: 10m, stock: 100));

        var result = await CreateSut().PlaceOrderAsync(Request((1, quantity)), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderError.InvalidQuantity, result.Error);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceOrder_WithUnknownProduct_FailsAsUnknownProduct()
    {
        // The repository returns only product 1; the request also references product 2.
        GivenProducts(Product(1, price: 10m, stock: 100));

        var result = await CreateSut().PlaceOrderAsync(Request((1, 1), (2, 1)), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderError.UnknownProduct, result.Error);
        Assert.Contains("2", result.Message);
        await _orders.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceOrder_WithInsufficientStock_FailsAndLeavesStockUnchanged()
    {
        var product = Product(1, price: 10m, stock: 3);
        GivenProducts(product);

        var result = await CreateSut().PlaceOrderAsync(Request((1, 5)), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderError.InsufficientStock, result.Error);
        Assert.Equal(3, product.StockQuantity); // not reserved
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceOrder_HappyPath_PersistsOrderDecrementsStockAndComputesTotal()
    {
        var backpack = Product(1, price: 100m, stock: 10);
        var sticker = Product(2, price: 5m, stock: 50);
        GivenProducts(backpack, sticker);

        var result = await CreateSut().PlaceOrderAsync(Request((1, 2), (2, 3)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Order);
        Assert.Equal(2 * 100m + 3 * 5m, result.Order!.TotalAmount);
        Assert.Equal(OrderStatus.Placed, result.Order.Status);
        Assert.Equal(FixedNow, result.Order.CreatedUtc);

        Assert.Equal(8, backpack.StockQuantity); // 10 - 2
        Assert.Equal(47, sticker.StockQuantity); // 50 - 3

        // Persisted exactly once - and with the very order instance the service returned, then committed.
        await _orders.Received(1).AddAsync(result.Order, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceOrder_WithTwoLinesForTheSameProduct_SumsQuantitiesForStockAndTotal()
    {
        var product = Product(1, price: 10m, stock: 5);
        GivenProducts(product);

        // 3 + 2 = 5 requested, exactly the available stock.
        var result = await CreateSut().PlaceOrderAsync(Request((1, 3), (1, 2)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50m, result.Order!.TotalAmount); // 5 * 10
        Assert.Single(result.Order.Lines); // collapsed to one line
        Assert.Equal(5, result.Order.Lines[0].Quantity);
        Assert.Equal(0, product.StockQuantity); // fully reserved
    }
}
