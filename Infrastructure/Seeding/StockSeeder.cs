using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Infrastructure.Seeding;

/// <summary>
/// First boot only: loads the company stock list (Metro, SH, screws, Kent — taken from the
/// Excel files dated 3/11/2026) into StockItems. After that the app is the main copy:
/// it is never re-seeded, so edits and shelf movements are kept.
/// </summary>
public static class StockSeeder
{
    private sealed record Row(
        string family, string name, string? color, string? drawerType, int? height, int? length, int sortOrder,
        string? warehouseLabel, int? warehouse, int? shops, int? ordered, int? onWay, int? total,
        string? qtyText, string? extraJson);

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (await db.StockItems.AnyAsync(ct)) return;

        await using var stream = typeof(StockSeeder).Assembly
            .GetManifestResourceStream("ManolyWarehouse.Infrastructure.Seeding.stock-seed.json")
            ?? throw new InvalidOperationException("stock-seed.json is missing from the build.");
        var rows = await JsonSerializer.DeserializeAsync<List<Row>>(stream, cancellationToken: ct) ?? new();

        db.StockItems.AddRange(rows.Select(r => StockItem.Create(
            r.family, r.name, r.color, r.drawerType, r.height, r.length, r.sortOrder,
            r.warehouseLabel, r.warehouse, r.shops, r.ordered, r.onWay, r.total, r.qtyText, r.extraJson)));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} stock items from the Excel stock lists", rows.Count);
    }
}
