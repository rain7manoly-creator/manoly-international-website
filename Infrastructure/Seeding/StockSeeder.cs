using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Infrastructure.Seeding;

/// <summary>
/// Loads the company stock list (Metro, SH, screws, Kent) into StockItems.
///
/// 1. First boot: stock-seed.json (Excel files of 3/11/2026) creates every line.
/// 2. Updates: each embedded stock-update-YYYY-MM-DD.json is applied once, in date
///    order, and recorded in StockImports. An update overwrites the numbers of the
///    lines it contains (matched by family + name + colour), adds any new lines,
///    and writes a StockMovement so the change shows in each line's history.
///    Lines not in the file are left as they are.
/// </summary>
public static class StockSeeder
{
    private const string ResourcePrefix = "ManolyWarehouse.Infrastructure.Seeding.";

    private sealed record Row(
        string family, string name, string? color, string? drawerType, int? height, int? length, int sortOrder,
        string? warehouseLabel, int? warehouse, int? shops, int? ordered, int? onWay, int? total,
        string? qtyText, string? extraJson);

    /// <summary>App product name (as on the warehouse map) → the stock line it is counted in.</summary>
    private sealed record Link(string product, string family, string name, string? color);
    private sealed record UpdateFile(List<Row> rows, List<Link>? links, string? reason);

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (!await db.StockItems.AnyAsync(ct))
        {
            var rows = await ReadAsync("stock-seed.json", ct);
            db.StockItems.AddRange(rows.Select(r => StockItem.Create(
                r.family, r.name, r.color, r.drawerType, r.height, r.length, r.sortOrder,
                r.warehouseLabel, r.warehouse, r.shops, r.ordered, r.onWay, r.total, r.qtyText, r.extraJson)));
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} stock items from the Excel stock lists", rows.Count);
        }

        var updates = typeof(StockSeeder).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix + "stock-update-") && n.EndsWith(".json"))
            .Select(n => n[ResourcePrefix.Length..])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        foreach (var file in updates)
        {
            if (await db.StockImports.AnyAsync(i => i.Name == file, ct)) continue;
            await ApplyUpdateAsync(db, file, logger, ct);
        }
    }

    private static async Task ApplyUpdateAsync(AppDbContext db, string file, ILogger logger, CancellationToken ct)
    {
        var (rows, links, reasonText) = await ReadUpdateAsync(file, ct);
        var reason = reasonText ?? $"تحديث من ملفات Excel {DateLabel(file)}";
        var items = await db.StockItems.ToListAsync(ct);
        // The same code can appear on more than one line (e.g. G-1321200W twice), so lines
        // with the same key are matched in file order.
        var byKey = items.OrderBy(i => i.SortOrder).GroupBy(i => Key(i.Family, i.Name, i.Color))
            .ToDictionary(g => g.Key, g => new Queue<StockItem>(g));
        var nextSort = items.Count == 0 ? 1 : items.Max(i => i.SortOrder) + 1;
        int changed = 0, added = 0;

        foreach (var r in rows)
        {
            if (byKey.TryGetValue(Key(r.family, r.name, r.color), out var queue) && queue.TryDequeue(out var item))
            {
                var same = item.Warehouse == r.warehouse && item.Shops == r.shops && item.Ordered == r.ordered
                           && item.OnWay == r.onWay && item.Total == r.total && item.QtyText == r.qtyText;
                var before = item.Total;
                item.SetValues(r.warehouse, r.shops, r.ordered, r.onWay, r.total, "import");
                item.SetFileData(r.qtyText, r.extraJson);
                if (!same)
                {
                    changed++;
                    db.StockMovements.Add(StockMovement.Create(item,
                        before.HasValue && r.total.HasValue ? r.total - before : null,
                        before, r.total, reason, null, "ملف Excel"));
                }
            }
            else
            {
                db.StockItems.Add(StockItem.Create(
                    r.family, r.name, r.color, r.drawerType, r.height, r.length, nextSort++,
                    r.warehouseLabel, r.warehouse, r.shops, r.ordered, r.onWay, r.total, r.qtyText, r.extraJson));
                added++;
            }
        }

        // Numbers first, so linking below never moves stock (links only affect future shelf changes)
        await db.SaveChangesAsync(ct);

        // Link app products to their stock line by name (only products not linked yet)
        var linked = 0;
        if (links.Count > 0)
        {
            var target = links
                .GroupBy(l => NormName(l.product))
                .ToDictionary(g => g.Key, g => g.First());
            var lines = (await db.StockItems.OrderBy(i => i.SortOrder).ToListAsync(ct))
                .GroupBy(i => Key(i.Family, i.Name, i.Color))
                .ToDictionary(g => g.Key, g => g.First());
            var products = await db.Products.Where(p => p.StockItemId == null).ToListAsync(ct);
            foreach (var p in products)
            {
                if (!target.TryGetValue(NormName(p.Name), out var l)) continue;
                if (!lines.TryGetValue(Key(l.family, l.name, l.color), out var line)) continue;
                p.LinkStock(line.Id, "import");
                linked++;
            }
        }

        db.StockImports.Add(StockImport.Create(file));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Applied {File}: {Changed} stock lines updated, {Added} added, {Linked} products linked",
            file, changed, added, linked);
    }

    // Same rule the link list was built with: "حرار" typo, spacing and case don't matter
    private static string NormName(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s.Replace("حرار", "جرار"), @"\s+", " ").Trim().ToLowerInvariant();

    private static async Task<(List<Row> Rows, List<Link> Links, string? Reason)> ReadUpdateAsync(string file, CancellationToken ct)
    {
        await using var stream = typeof(StockSeeder).Assembly.GetManifestResourceStream(ResourcePrefix + file)
            ?? throw new InvalidOperationException($"{file} is missing from the build.");
        var doc = await JsonSerializer.DeserializeAsync<UpdateFile>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException($"{file} is empty.");
        return (doc.rows, doc.links ?? new(), doc.reason);
    }

    private static string Key(string family, string name, string? color) => $"{family}|{name}|{color}";

    // "stock-update-2026-11-10.json" (or "…-10b.json", a second update that day) → "10/11/2026"
    private static string DateLabel(string file)
    {
        var parts = file.Replace("stock-update-", "").Replace(".json", "").TrimEnd('a', 'b', 'c', 'd').Split('-');
        return parts.Length == 3 ? $"{parts[2].TrimStart('0')}/{parts[1].TrimStart('0')}/{parts[0]}" : file;
    }

    private static async Task<List<Row>> ReadAsync(string file, CancellationToken ct)
    {
        await using var stream = typeof(StockSeeder).Assembly.GetManifestResourceStream(ResourcePrefix + file)
            ?? throw new InvalidOperationException($"{file} is missing from the build.");
        return await JsonSerializer.DeserializeAsync<List<Row>>(stream, cancellationToken: ct) ?? new();
    }
}
