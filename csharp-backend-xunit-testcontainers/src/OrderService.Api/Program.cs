using Microsoft.EntityFrameworkCore;
using OrderService.Api.Contracts;
using OrderService.Api.Data;
using OrderService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// The integration tests replace this DbContext registration with one pointing at the Testcontainers
// SQL Server (see CustomWebApplicationFactory). For a real `dotnet run` set ConnectionStrings:Sql;
// the placeholder below only keeps startup valid when no configuration is provided.
var connectionString = builder.Configuration.GetConnectionString("Sql")
    ?? "Server=localhost;Database=OrderService;Trusted_Connection=True;TrustServerCertificate=True";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IOrderPlacementService, OrderPlacementService>();
builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();

app.MapPost("/products", async (CreateProductRequest request, IProductCatalogService catalog, CancellationToken ct) =>
{
    var result = await catalog.CreateProductAsync(request, ct);
    return result.IsSuccess
        ? Results.Created($"/products/{result.Product!.Id}", ProductResponse.From(result.Product))
        : Results.BadRequest(new { error = result.Error!.Value.ToString(), message = result.Message });
});

app.MapGet("/products", async (IProductRepository products, CancellationToken ct) =>
    Results.Ok((await products.GetAllAsync(ct)).Select(ProductResponse.From)));

app.MapGet("/products/{id:int}", async (int id, IProductRepository products, CancellationToken ct) =>
    await products.GetByIdAsync(id, ct) is { } product
        ? Results.Ok(ProductResponse.From(product))
        : Results.NotFound());

app.MapPost("/orders", async (PlaceOrderRequest request, IOrderPlacementService orders, CancellationToken ct) =>
{
    var result = await orders.PlaceOrderAsync(request, ct);
    return result.IsSuccess
        ? Results.Created($"/orders/{result.Order!.Id}", OrderResponse.From(result.Order))
        : Results.BadRequest(new { error = result.Error!.Value.ToString(), message = result.Message });
});

app.MapGet("/orders/{id:int}", async (int id, IOrderRepository orders, CancellationToken ct) =>
    await orders.GetByIdAsync(id, ct) is { } order
        ? Results.Ok(OrderResponse.From(order))
        : Results.NotFound());

app.Run();

/// <summary>Exposed as a public partial class so the integration tests' WebApplicationFactory&lt;Program&gt;
/// can boot this exact application in-process.</summary>
public partial class Program
{
}
