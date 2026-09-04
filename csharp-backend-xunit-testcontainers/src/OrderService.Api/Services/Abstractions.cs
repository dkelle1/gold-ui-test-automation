using OrderService.Api.Domain;

namespace OrderService.Api.Services;

/// <summary>The persistence operations the order flow needs, behind interfaces so the unit tests can
/// substitute them with NSubstitute and the integration tests can bind the real EF Core implementations.
/// The service loads tracked <see cref="Product"/> entities, mutates their stock, and adds an order;
/// a single <see cref="IUnitOfWork.SaveChangesAsync"/> commits both in one transaction.</summary>
public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct);

    Task<Product?> GetByIdAsync(int id, CancellationToken ct);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct);

    Task AddAsync(Product product, CancellationToken ct);
}

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct);

    Task<Order?> GetByIdAsync(int id, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
