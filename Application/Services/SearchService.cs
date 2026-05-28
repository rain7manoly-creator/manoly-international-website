using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Application.Services;

public class SearchService : ISearchService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SearchService> _logger;

    public SearchService(AppDbContext db, ILogger<SearchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SearchResultsViewModel> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new SearchResultsViewModel { Query = string.Empty };

        var q = query.Trim();
        // EF.Functions.ILike is PostgreSQL case-insensitive LIKE
        var pattern = $"%{q}%";

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Name, pattern))
            .OrderBy(p => p.Name)
            .Take(20)
            .Select(p => new ProductSearchResult
            {
                ProductId = p.Id,
                Name = p.Name,
                CategoryName = p.Category.Name,
                LocationCount = p.ShelfInventories.Count,
                IsInAreaZ = p.AreaZInventories.Any(az => !az.IsDispatched)
            })
            .ToListAsync(ct);

        var shelves = await _db.Shelves
            .AsNoTracking()
            .Where(s => EF.Functions.ILike(s.Code, pattern))
            .OrderBy(s => s.Code)
            .Take(20)
            .Select(s => new ShelfSearchResult
            {
                ShelfId = s.Id,
                Code = s.Code,
                OccupiedSlots = s.Inventories.Count
            })
            .ToListAsync(ct);

        var suppliers = await _db.PurchaseOrders
            .AsNoTracking()
            .Where(po => EF.Functions.ILike(po.Supplier, pattern))
            .GroupBy(po => po.Supplier)
            .Select(g => new SupplierSearchResult
            {
                Supplier = g.Key,
                OrderCount = g.Count()
            })
            .OrderBy(s => s.Supplier)
            .Take(20)
            .ToListAsync(ct);

        _logger.LogDebug(
            "Search '{Query}': {Products} products, {Shelves} shelves, {Suppliers} suppliers",
            q, products.Count, shelves.Count, suppliers.Count);

        return new SearchResultsViewModel
        {
            Query = q,
            Products = products,
            Shelves = shelves,
            Suppliers = suppliers
        };
    }
}
