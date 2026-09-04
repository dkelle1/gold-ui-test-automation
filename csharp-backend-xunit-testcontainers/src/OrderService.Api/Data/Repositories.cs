using Microsoft.EntityFrameworkCore;
using OrderService.Api.Domain;
using OrderService.Api.Services;

namespace OrderService.Api.Data;

/// <summary>EF Core implementations of the persistence abstractions. Thin on purpose - the business
/// rules live in the services; these just translate the abstractions to EF queries. The integration
/// tests exercise exactly these against a real SQL Server; the unit tests never touch them.</summary>
public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct) =>
        await _db.Products.OrderBy(p => p.Id).ToListAsync(ct);

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct) =>
        await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct) =>
        await _db.Products.FirstOrDefaultAsync(p => p.Sku == sku, ct);

    public async Task AddAsync(Product product, CancellationToken ct) =>
        await _db.Products.AddAsync(product, ct);
}

public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Order order, CancellationToken ct) =>
        await _db.Orders.AddAsync(order, ct);

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct) =>
        await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
}

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public EfUnitOfWork(AppDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
