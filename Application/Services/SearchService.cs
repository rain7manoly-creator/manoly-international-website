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

        // Split into tokens so "جرار زجاج" matches "جرار ميترو زجاج ابيض 55/20"
        // Each token must appear somewhere in the target — AND semantics.
        var tokens = q
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        // ── Products ──────────────────────────────────────────────────────
        // Each token is checked against name OR category name (case-insensitive).
        var productQuery = _db.Products.AsNoTracking();
        foreach (var token in tokens)
        {
            var pat = $"%{token}%";
            productQuery = productQuery.Where(p =>
                EF.Functions.ILike(p.Name, pat) ||
                EF.Functions.ILike(p.Category.Name, pat));
        }
        var products = await productQuery
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

        // ── Shelves ───────────────────────────────────────────────────────
        // Shelf codes are short (e.g. "A1") — use the full query as one pattern.
        var shelfPattern = $"%{q}%";
        var shelves = await _db.Shelves
            .AsNoTracking()
            .Where(s => EF.Functions.ILike(s.Code, shelfPattern))
            .OrderBy(s => s.Code)
            .Take(20)
            .Select(s => new ShelfSearchResult
            {
                ShelfId = s.Id,
                Code = s.Code,
                OccupiedSlots = s.Inventories.Count
            })
            .ToListAsync(ct);

        // ── Suppliers ─────────────────────────────────────────────────────
        // Supplier names can be multi-word — tokenise the same way as products.
        var supplierQuery = _db.PurchaseOrders.AsNoTracking();
        foreach (var token in tokens)
        {
            var pat = $"%{token}%";
            supplierQuery = supplierQuery.Where(po => EF.Functions.ILike(po.Supplier, pat));
        }
        var suppliers = await supplierQuery
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
