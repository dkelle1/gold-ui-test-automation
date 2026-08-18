using System.Net;
using System.Net.Http.Json;
using OrderService.Api.Contracts;
using OrderService.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderService.IntegrationTests.Tests;

/// <summary>Full-stack tests of the order flow. The happy-path test is the one that most justifies a
/// real database: it proves that placing an order both persists the order *and* decrements the product's
/// stock in the same committed transaction - a cross-row invariant an in-memory fake would not exercise
/// faithfully.</summary>
[Collection(IntegrationCollection.Name)]
public class OrdersEndpointsTests : IntegrationTestBase
{
    public OrdersEndpointsTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    private async Task<ProductResponse> SeedProductAsync(string sku, decimal price, int stock)
    {
        var response = await Client.PostAsJsonAsync(
            "/products", new CreateProductRequest(sku, "Product", price, stock));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    [Fact]
    public async Task PlaceOrder_HappyPath_PersistsOrderAndDecrementsStock()
    {
        var product = await SeedProductAsync("ORD-0001", 100m, 10);

        var place = await Client.PostAsJsonAsync(
            "/orders",
            new PlaceOrderRequest("buyer@example.com", [new OrderItemRequest(product.Id, 3)]));

        Assert.Equal(HttpStatusCode.Created, place.StatusCode);
        var order = await place.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal(300m, order!.TotalAmount);
        Assert.Equal("Placed", order.Status);
        Assert.Single(order.Lines);

        // The order is retrievable by its id...
        var fetchedOrder = await Client.GetFromJsonAsync<OrderResponse>($"/orders/{order.Id}");
        Assert.NotNull(fetchedOrder);
        Assert.Equal(order.TotalAmount, fetchedOrder!.TotalAmount);

        // ...and the stock really moved in the database, observed through a fresh read.
        var afterProduct = await Client.GetFromJsonAsync<ProductResponse>($"/products/{product.Id}");
        Assert.Equal(7, afterProduct!.StockQuantity); // 10 - 3
    }

    [Fact]
    public async Task PlaceOrder_WithInsufficientStock_ReturnsBadRequestAndLeavesStockUnchanged()
    {
        var product = await SeedProductAsync("ORD-0002", 50m, 2);

        var place = await Client.PostAsJsonAsync(
            "/orders",
            new PlaceOrderRequest("buyer@example.com", [new OrderItemRequest(product.Id, 5)]));

        Assert.Equal(HttpStatusCode.BadRequest, place.StatusCode);

        var afterProduct = await Client.GetFromJsonAsync<ProductResponse>($"/products/{product.Id}");
        Assert.Equal(2, afterProduct!.StockQuantity); // the failed order committed nothing
    }

    [Fact]
    public async Task PlaceOrder_WithUnknownProduct_ReturnsBadRequest()
    {
        var place = await Client.PostAsJsonAsync(
            "/orders",
            new PlaceOrderRequest("buyer@example.com", [new OrderItemRequest(4242, 1)]));

        Assert.Equal(HttpStatusCode.BadRequest, place.StatusCode);
    }

    [Fact]
    public async Task GetOrder_WhenItDoesNotExist_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/orders/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
