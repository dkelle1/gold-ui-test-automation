using System.Net;
using System.Net.Http.Json;
using OrderService.Api.Contracts;
using OrderService.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderService.IntegrationTests.Tests;

/// <summary>Full-stack tests of the product endpoints: real HTTP request -> minimal API -> service ->
/// EF Core -> SQL Server (in a container) and back. Nothing is mocked here - the point is to prove the
/// pieces the unit tests checked in isolation actually work when wired to the real provider.</summary>
[Collection(IntegrationCollection.Name)]
public class ProductsEndpointsTests : IntegrationTestBase
{
    public ProductsEndpointsTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task CreateProduct_ThenGetById_ReturnsThePersistedProduct()
    {
        var create = await Client.PostAsJsonAsync(
            "/products", new CreateProductRequest("ABC-1234", "Backpack", 100m, 10));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);

        var fetched = await Client.GetFromJsonAsync<ProductResponse>($"/products/{created.Id}");
        Assert.NotNull(fetched);
        Assert.Equal("ABC-1234", fetched!.Sku);
        Assert.Equal(100m, fetched.Price);
        Assert.Equal(10, fetched.StockQuantity);
    }

    [Fact]
    public async Task CreateProduct_WithMalformedSku_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/products", new CreateProductRequest("nope", "Backpack", 100m, 10));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSku_ReturnsBadRequest()
    {
        var first = await Client.PostAsJsonAsync(
            "/products", new CreateProductRequest("DUP-0001", "First", 1m, 1));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await Client.PostAsJsonAsync(
            "/products", new CreateProductRequest("DUP-0001", "Second", 2m, 2));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task GetProduct_WhenItDoesNotExist_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/products/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
