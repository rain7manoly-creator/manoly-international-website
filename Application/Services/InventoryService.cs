using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Application.Services;

public interface IInventoryService
{
    Task<InventorySummaryViewModel> GetSummaryAsync(
        string? categoryFilter, int page, int pageSize, CancellationToken ct = default);
}

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db) => _db = db;

    public async Task<InventorySummaryViewModel> GetSummaryAsync(
        string? categoryFilter, int page, int pageSize, CancellationToken ct = default)
    {
        // Base query: only products that have stock somewhere
        var query = _db.Products
            .AsNoTracking()
            .Where(p =>
                p.ShelfInventories.Any() ||
                p.AreaZInventories.Any(az => !az.IsDispatched));

        // Apply category filter at DB level so count + pagination are consistent
        if (!string.IsNullOrEmpty(categoryFilter))
            query = query.Where(p => p.Category.Name == categoryFilter);

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
            ActiveCategory = categoryFilter,
            Page           = page,
            TotalPages     = totalPages,
        };
    }
}
