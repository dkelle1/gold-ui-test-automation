namespace OrderService.Api.Domain;

public enum OrderStatus
{
    Placed = 1,
}

/// <summary>A placed order and its lines. <see cref="TotalAmount"/> is persisted (the sum of its line
/// totals at placement time) so historical orders are not re-priced if a product's price later changes.</summary>
public class Order
{
    public int Id { get; set; }
    public required string CustomerEmail { get; set; }
    public DateTime CreatedUtc { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }

    /// <summary>The product's price captured at placement time (an order line is a historical record).</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
