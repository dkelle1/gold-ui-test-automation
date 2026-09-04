using OrderService.Api.Domain;

namespace OrderService.Api.Contracts;

// Request/response DTOs kept separate from the domain entities, so the API contract and the persistence
// model can evolve independently (and so the tests assert against a stable shape, not EF internals).

public record CreateProductRequest(string Sku, string Name, decimal Price, int StockQuantity);

public record ProductResponse(int Id, string Sku, string Name, decimal Price, int StockQuantity)
{
    public static ProductResponse From(Product p) => new(p.Id, p.Sku, p.Name, p.Price, p.StockQuantity);
}

public record OrderItemRequest(int ProductId, int Quantity);

public record PlaceOrderRequest(string CustomerEmail, IReadOnlyList<OrderItemRequest> Items);

public record OrderLineResponse(int ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderResponse(
    int Id,
    string CustomerEmail,
    DateTime CreatedUtc,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<OrderLineResponse> Lines)
{
    public static OrderResponse From(Order o) => new(
        o.Id,
        o.CustomerEmail,
        o.CreatedUtc,
        o.Status.ToString(),
        o.TotalAmount,
        o.Lines.Select(l => new OrderLineResponse(l.ProductId, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
}
