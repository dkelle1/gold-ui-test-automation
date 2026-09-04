namespace OrderService.Api.Domain;

/// <summary>A sellable product with a stock level the order flow decrements.</summary>
public class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}
