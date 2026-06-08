using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Application.Services;

public interface IInventoryService
{
    Task<InventorySummaryViewModel> GetSummaryAsync(string? categoryFilter, CancellationToken ct = default);
}

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db) => _db = db;

    public async Task<InventorySummaryViewModel> GetSummaryAsync(
        string? categoryFilter, CancellationToken ct = default)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p =>
                p.ShelfInventories.Any() ||
                p.AreaZInventories.Any(az => !az.IsDispatched))
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
            .OrderBy(p => p.CategoryName)
            .ThenBy(p => p.Name)
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

        var allGroups = rows
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

        var filteredGroups = string.IsNullOrEmpty(categoryFilter)
            ? allGroups
            : allGroups.Where(g => g.CategoryName == categoryFilter).ToList();

        return new InventorySummaryViewModel
        {
            Categories     = filteredGroups,
            ActiveCategory = categoryFilter,
            TotalProducts  = rows.Count,
            TotalBundles   = rows.Sum(r => r.TotalBundles),
            TotalUnits     = rows.Sum(r => r.TotalUnits),
        };
    }
}
