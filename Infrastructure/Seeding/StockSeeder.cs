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

        const string smirdexStep = "smirdex-from-area-z-2026-11-10";
        if (!await db.StockImports.AnyAsync(i => i.Name == smirdexStep, ct))
            await FillSmirdexFromAreaZAsync(db, smirdexStep, logger, ct);

        // Metro/SH/screws/Kent = what is in the warehouse (shelves + Area Z), with products linked by name
        const string matchStep = "match-warehouse-2026-11-10";
        if (!await db.StockImports.AnyAsync(i => i.Name == matchStep, ct))
        {
            var (linked, changed) = await ManolyWarehouse.Application.Services.StockService.MatchWithWarehouseAsync(db, null, "مطابقة", ct);
            db.StockImports.Add(StockImport.Create(matchStep));
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Match with warehouse: {Linked} products linked, {Changed} stock lines set", linked, changed);
        }

        // Second pass with the wider rule (names like "رول اسفنج 180"): fills only lines still empty
        const string smirdexStep2 = "smirdex-from-area-z-2026-11-10b";
        if (!await db.StockImports.AnyAsync(i => i.Name == smirdexStep2, ct))
            await FillSmirdexFromAreaZAsync(db, smirdexStep2, logger, ct, onlyEmpty: true);
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

    private static readonly int[] PaperGrits = { 40, 60, 80, 100, 120, 150, 180, 220, 240, 280, 320, 400 };
    private static readonly int[] SpongeGrits = { 120, 150, 180, 220, 240, 320, 400 };

    /// <summary>
    /// One time: Smirdex lines take their numbers (cartons) from what is in Area Z now,
    /// and those Area Z products are linked so later Area Z changes move the Smirdex stock.
    /// A product counts as Smirdex when its name mentions حف / smirdex / سميردكس / سنفرة;
    /// اسفنج or رول in the name means a sponge roll; the grit (P40, 120 …) picks the line.
    /// </summary>
    private static async Task FillSmirdexFromAreaZAsync(AppDbContext db, string step, ILogger logger, CancellationToken ct,
        bool onlyEmpty = false)
    {
        var lines = await db.StockItems.Where(s => s.Family == "smirdex").ToListAsync(ct);
        var allLines = lines;
        if (onlyEmpty) lines = lines.Where(l => l.Total == null).ToList();
        var entries = await db.AreaZInventory.Where(a => !a.IsDispatched)
            .Select(a => new { a.ProductId, a.Product.Name, a.BundleCount }).ToListAsync(ct);

        var sums = new Dictionary<StockItem, (int Cartons, List<string> Names, HashSet<int> Products)>();
        foreach (var e in entries)
        {
            var line = MatchSmirdex(e.Name, allLines);
            if (line is null || !lines.Contains(line)) continue;
            if (!sums.TryGetValue(line, out var acc)) acc = (0, new List<string>(), new HashSet<int>());
            acc.Cartons += e.BundleCount;
            if (!acc.Names.Contains(e.Name)) acc.Names.Add(e.Name);
            acc.Products.Add(e.ProductId);
            sums[line] = acc;
        }

        foreach (var (line, acc) in sums)
        {
            var before = line.Total;
            line.SetValues(line.Warehouse, line.Shops, line.Ordered, line.OnWay, acc.Cartons, "import");
            var reason = $"من منطقة Z: {string.Join("، ", acc.Names.Select(n => $"«{n}»"))}";
            db.StockMovements.Add(StockMovement.Create(line, acc.Cartons - (before ?? 0), before, acc.Cartons,
                reason.Length > 300 ? reason[..300] : reason, null, "منطقة Z"));
            var products = await db.Products.Where(p => acc.Products.Contains(p.Id) && p.StockItemId == null).ToListAsync(ct);
            foreach (var p in products) p.LinkStock(line.Id, "import");
        }

        db.StockImports.Add(StockImport.Create(step));
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Smirdex from Area Z: {Lines} lines filled from {Entries} Area Z entries", sums.Count, sums.Values.Sum(v => v.Names.Count));
    }

    internal static StockItem? MatchSmirdex(string productName, IReadOnlyList<StockItem> lines)
    {
        var n = productName.ToLowerInvariant();
        foreach (var (ar, d) in new[] { ('٠', '0'), ('١', '1'), ('٢', '2'), ('٣', '3'), ('٤', '4'), ('٥', '5'), ('٦', '6'), ('٧', '7'), ('٨', '8'), ('٩', '9') })
            n = n.Replace(ar, d);
        var sponge = n.Contains("سفنج") || n.Contains("sponge");   // اسفنج / إسفنج / اسفنجة
        var isSmirdex = sponge || n.Contains("حف") || n.Contains("smirdex") || n.Contains("سميردكس")
                        || n.Contains("سمردكس") || n.Contains("سنفر");
        if (!isSmirdex) return null;
        sponge = sponge || n.Contains("رول");

        // Prefer an explicit "P120"; otherwise the first number that is a known grit
        int? grit = null;
        var p = System.Text.RegularExpressions.Regex.Match(n, @"p\s*(\d{2,3})");
        if (p.Success) grit = int.Parse(p.Groups[1].Value);
        else
        {
            var allowed = sponge ? SpongeGrits : PaperGrits;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(n, @"\d{2,3}"))
                if (allowed.Contains(int.Parse(m.Value))) { grit = int.Parse(m.Value); break; }
        }
        if (grit is null) return null;
        var name = sponge ? $"رول اسفنج حف {grit}" : $"ورق حف P{grit}";
        return lines.FirstOrDefault(l => l.Name == name);
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
