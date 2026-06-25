using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Application.Services;

public interface IInventoryService
{
    Task<InventorySummaryViewModel> GetSummaryAsync(
        string? categoryFilter, int page, int pageSize, CancellationToken ct = default);

    Task<OutOfStockViewModel> GetOutOfStockAsync(
        int page, int pageSize, CancellationToken ct = default);
}

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db) => _db = db;

    public async Task<InventorySummaryViewModel> GetSummaryAsync(
        string? categoryFilter, int page, int pageSize, CancellationToken ct = default)
    {
        // Base query: only products that have stock somewhere
        var baseQuery = _db.Products
            .AsNoTracking()
            .Where(p =>
                p.ShelfInventories.Any() ||
                p.AreaZInventories.Any(az => !az.IsDispatched));

        // All categories across the full stock — never filtered — for the filter pills
        var allCategories = await baseQuery
            .GroupBy(p => p.Category.Name)
            .Select(g => new InventoryCategoryMeta
            {
                CategoryName = g.Key,
                ProductCount = g.Count()
            })
            .OrderBy(c => c.CategoryName)
            .ToListAsync(ct);

        // Apply category filter at DB level so count + pagination are consistent
        var query = string.IsNullOrEmpty(categoryFilter)
            ? baseQuery
            : baseQuery.Where(p => p.Category.Name == categoryFilter);

        var totalCount = await query.CountAsync(ct);
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;
        page = Math.Clamp(page, 1, Math.Max(1, totalPages));

        var products = await query
            .OrderBy(p => p.Category.Name)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                CategoryName = p.Category.Name,

                ShelfLocations = p.ShelfInventories
                    .OrderBy(si => si.Shelf.Label)
                    .ThenBy(si => si.Shelf.Number)
                    .ThenBy(si => si.Position)
                    .Select(si => new InventoryShelfLocation
                    {
                        ShelfCode      = si.Shelf.Code,
                        Position       = si.Position,
                        BundleCount    = si.BundleCount,
                        UnitsPerBundle = si.UnitsPerBundle
                    })
                    .ToList(),

                AreaZBundles = p.AreaZInventories
                    .Where(az => !az.IsDispatched)
                    .Sum(az => (int?)az.BundleCount) ?? 0,

                AreaZUnits = p.AreaZInventories
                    .Where(az => !az.IsDispatched)
                    .Sum(az => (int?)(az.BundleCount * az.UnitsPerBundle)) ?? 0,
            })
            .ToListAsync(ct);

        var rows = products.Select(p => new InventoryProductRow
        {
            ProductId          = p.Id,
            ProductName        = p.Name,
            CategoryName       = p.CategoryName,
            ShelfBundles       = p.ShelfLocations.Sum(l => l.BundleCount),
            ShelfUnits         = p.ShelfLocations.Sum(l => l.TotalQuantity),
            ShelfLocationCount = p.ShelfLocations.Count,
            AreaZBundles       = p.AreaZBundles,
            AreaZUnits         = p.AreaZUnits,
            Locations          = p.ShelfLocations,
        }).ToList();

        var groups = rows
            .GroupBy(r => r.CategoryName)
            .Select(g => new InventoryCategoryGroup
            {
                CategoryName = g.Key,
                ProductCount = g.Count(),
                TotalBundles = g.Sum(r => r.TotalBundles),
                Products     = g.ToList(),
            })
            .OrderBy(g => g.CategoryName)
            .ToList();

        return new InventorySummaryViewModel
        {
            Categories     = groups,
            AllCategories  = allCategories,
            ActiveCategory = categoryFilter,
            Page           = page,
            TotalPages     = totalPages,
        };
    }

    public async Task<OutOfStockViewModel> GetOutOfStockAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        // Every catalog product with zero shelf inventory AND no active Area Z stock.
        var query = _db.Products
            .AsNoTracking()
            .Where(p =>
                !p.ShelfInventories.Any() &&
                !p.AreaZInventories.Any(az => !az.IsDispatched));

        var totalMissing = await query.CountAsync(ct);
        var totalPages   = pageSize > 0 ? (int)Math.Ceiling((double)totalMissing / pageSize) : 1;
        page = Math.Clamp(page, 1, Math.Max(1, totalPages));

        var rows = await query
            .OrderBy(p => p.Category.Name)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new OutOfStockProductRow
            {
                ProductId    = p.Id,
                ProductName  = p.Name,
                CategoryName = p.Category.Name,
            })
            .ToListAsync(ct);

        var groups = rows
            .GroupBy(r => r.CategoryName)
            .Select(g => new OutOfStockCategoryGroup
            {
                CategoryName = g.Key,
                Products     = g.ToList(),
            })
            .OrderBy(g => g.CategoryName)
            .ToList();

        return new OutOfStockViewModel
        {
            Categories   = groups,
            Page         = page,
            TotalPages   = totalPages,
            TotalMissing = totalMissing,
        };
    }
}
