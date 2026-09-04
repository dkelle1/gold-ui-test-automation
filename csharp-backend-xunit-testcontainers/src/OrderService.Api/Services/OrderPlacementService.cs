using OrderService.Api.Contracts;
using OrderService.Api.Domain;

namespace OrderService.Api.Services;

public interface IOrderPlacementService
{
    Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct);
}

/// <summary>The business logic under unit test. Every rule here is exercised in isolation by the xUnit
/// unit tests (with NSubstitute standing in for the repositories and clock) and end to end by the
/// integration tests (against a real SQL Server). The two describe the same behaviour from two angles.</summary>
public sealed class OrderPlacementService : IOrderPlacementService
{
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public OrderPlacementService(
        IProductRepository products, IOrderRepository orders, IUnitOfWork unitOfWork, IClock clock)
    {
        _products = products;
        _orders = orders;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return OrderResult.Fail(OrderError.EmptyOrder, "An order must contain at least one item.");
        }

        if (request.Items.Any(i => i.Quantity <= 0))
        {
            return OrderResult.Fail(OrderError.InvalidQuantity, "Every item quantity must be greater than zero.");
        }

        // Collapse duplicate product ids into a single required quantity, so two lines for the same
        // product are validated against stock together rather than each passing on its own.
        var requiredByProduct = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var products = await _products.GetByIdsAsync(requiredByProduct.Keys.ToList(), ct);
        var productsById = products.ToDictionary(p => p.Id);

        var missing = requiredByProduct.Keys.Where(id => !productsById.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return OrderResult.Fail(
                OrderError.UnknownProduct, $"Unknown product id(s): {string.Join(", ", missing.OrderBy(x => x))}.");
        }

        var insufficient = requiredByProduct
            .Where(kv => productsById[kv.Key].StockQuantity < kv.Value)
            .Select(kv => kv.Key)
            .ToList();
        if (insufficient.Count > 0)
        {
            return OrderResult.Fail(
                OrderError.InsufficientStock,
                $"Insufficient stock for product id(s): {string.Join(", ", insufficient.OrderBy(x => x))}.");
        }

        var order = new Order
        {
            CustomerEmail = request.CustomerEmail,
            CreatedUtc = _clock.UtcNow,
            Status = OrderStatus.Placed,
            Lines = requiredByProduct
                .OrderBy(kv => kv.Key)
                .Select(kv => new OrderLine
                {
                    ProductId = kv.Key,
                    Quantity = kv.Value,
                    UnitPrice = productsById[kv.Key].Price,
                })
                .ToList(),
        };
        order.TotalAmount = order.Lines.Sum(l => l.LineTotal);

        // Reserve the stock. These Product entities are tracked, so the decrement is persisted by the
        // same SaveChanges that inserts the order.
        foreach (var (productId, quantity) in requiredByProduct)
        {
            productsById[productId].StockQuantity -= quantity;
        }

        await _orders.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return OrderResult.Success(order);
    }
}
