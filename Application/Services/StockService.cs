using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Domain.Exceptions;
using ManolyWarehouse.Infrastructure.Persistence;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Application.Services;

// ─── View models ───────────────────────────────────────────────────────

public class StockItemDto
{
    public int Id { get; init; }
    public string Family { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Color { get; init; }
    public string? Type { get; init; }
    public int? H { get; init; }
    public int? Len { get; init; }
    public int? Total { get; init; }
    public string? QtyText { get; init; }
    public int Linked { get; init; }

    /// <summary>Where the linked products are right now (same unit as the line: pieces, or cartons for screws/Kent/Smirdex).</summary>
    public int? Shelves { get; set; }
    /// <summary>Area Z, per warehouse inside it.</summary>
    public List<WhereQty> AreaZ { get; set; } = new();
}

public record WhereQty(string Name, int Qty);

public class StockPageViewModel
{
    public IReadOnlyList<StockItemDto> Items { get; init; } = Array.Empty<StockItemDto>();
    /// <summary>App products not linked to a stock line, with their shelf + Area Z bundles.</summary>
    public IReadOnlyList<ManolyWarehouse.Application.ViewModels.StockLookupRow> OtherProducts { get; init; }
        = Array.Empty<ManolyWarehouse.Application.ViewModels.StockLookupRow>();
}

public class StockItemDetailViewModel
{
    public StockItem Item { get; init; } = default!;
    public IReadOnlyList<(int Id, string Name)> LinkedProducts { get; init; } = Array.Empty<(int, string)>();
    public IReadOnlyList<StockMovement> Movements { get; init; } = Array.Empty<StockMovement>();
    /// <summary>Each shelf holding a linked product (code, qty in the line's unit).</summary>
    public IReadOnlyList<WhereQty> Shelves { get; init; } = Array.Empty<WhereQty>();
    public IReadOnlyList<WhereQty> AreaZ { get; init; } = Array.Empty<WhereQty>();
}

public interface IStockService
{
    Task<StockPageViewModel> GetPageAsync(CancellationToken ct = default);
    Task<StockItemDetailViewModel?> GetItemAsync(int id, CancellationToken ct = default);
    Task UpdateAsync(int id, int? warehouse, int? shops, int? ordered, int? onWay, int? total, string? note, CancellationToken ct = default);
    Task<IReadOnlyList<StockItemDto>> ListForLinkingAsync(CancellationToken ct = default);
    Task<StockItemDto?> GetLinkForProductAsync(int productId, CancellationToken ct = default);
    Task LinkProductAsync(int productId, int? stockItemId, CancellationToken ct = default);
    Task<byte[]> ExportExcelAsync(CancellationToken ct = default);
    Task<(int Linked, int Changed)> MatchNowAsync(CancellationToken ct = default);
    Task<StockCheckViewModel> GetCheckAsync(CancellationToken ct = default);
    /// <summary>From the check page: link to an existing line ("line:12") or create a new line ("new:metro").</summary>
    Task<string> ConnectProductAsync(int productId, string target, CancellationToken ct = default);
    Task<ConnectViewModel?> GetConnectAsync(int productId, CancellationToken ct = default);
    Task<LinkCandidatesViewModel?> GetLinkCandidatesAsync(int stockItemId, CancellationToken ct = default);
}

public class ConnectViewModel
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public string Where { get; init; } = "";
    public int? CurrentId { get; init; }
    public int? SuggestedId { get; init; }
    public string? GuessFamily { get; init; }
    public IReadOnlyList<StockItemDto> Lines { get; init; } = Array.Empty<StockItemDto>();
}

public class LinkCandidatesViewModel
{
    public StockItem Item { get; init; } = default!;
    /// <summary>(Id, Name, Where, LinkedTo) — unlinked products in the warehouse first.</summary>
    public IReadOnlyList<(int Id, string Name, string Where, string? LinkedTo)> Products { get; init; } = Array.Empty<(int, string, string, string?)>();
}

public class StockService : IStockService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogService _activityLog;

    public StockService(AppDbContext db, ICurrentUserService currentUser, IActivityLogService activityLog)
    {
        _db = db;
        _currentUser = currentUser;
        _activityLog = activityLog;
    }

    public static readonly IReadOnlyDictionary<string, string> FamilyNames = new Dictionary<string, string>
    {
        ["metro"] = "جرار ميترو", ["metro-tandem"] = "سحاب ميترو تندم", ["metro-code"] = "ميترو — أكواد G",
        ["sh"] = "جرار SH", ["screws"] = "براغي اتيكو", ["kent"] = "الكنت", ["smirdex"] = "سميردكس"
    };

    private IQueryable<StockItemDto> Dtos() => _db.StockItems.AsNoTracking()
        .OrderBy(s => s.SortOrder)
        .Select(s => new StockItemDto
        {
            Id = s.Id, Family = s.Family, Name = s.Name, Color = s.Color, Type = s.DrawerType,
            H = s.Height, Len = s.Length, Total = s.Total, QtyText = s.QtyText,
            Linked = _db.Products.Count(p => p.StockItemId == s.Id)
        });

    public async Task<StockPageViewModel> GetPageAsync(CancellationToken ct = default)
    {
        var items = await Dtos().ToListAsync(ct);
        await FillSplitAsync(items, ct);
        var others = await _db.Products.AsNoTracking()
            .Where(p => p.StockItemId == null)
            .OrderBy(p => p.Category.Name).ThenBy(p => p.Name)
            .Select(p => new ManolyWarehouse.Application.ViewModels.StockLookupRow
            {
                ProductId = p.Id,
                ProductName = p.Name,
                CategoryName = p.Category.Name,
                TotalBundles =
                    (p.ShelfInventories.Sum(si => (int?)si.BundleCount) ?? 0) +
                    (p.AreaZInventories.Where(az => !az.IsDispatched).Sum(az => (int?)az.BundleCount) ?? 0),
                ShelfBundles = p.ShelfInventories.Sum(si => (int?)si.BundleCount) ?? 0,
            })
            .ToListAsync(ct);

        // Area Z per warehouse for the other products (the rest is on the shelves)
        var otherIds = others.Select(o => o.ProductId).ToList();
        var otherZ = await _db.AreaZInventory.AsNoTracking()
            .Where(a => !a.IsDispatched && otherIds.Contains(a.ProductId))
            .GroupBy(a => new { a.ProductId, a.Warehouse.Name })
            .Select(g => new { g.Key.ProductId, g.Key.Name, Qty = g.Sum(a => a.BundleCount) })
            .ToListAsync(ct);
        foreach (var o in others)
            o.AreaZ = otherZ.Where(z => z.ProductId == o.ProductId).Select(z => new WhereQty(z.Name, z.Qty)).ToList();

        return new StockPageViewModel { Items = items, OtherProducts = others };
    }

    /// <summary>
    /// Split every stock line into the warehouse and Area Z (per Area Z warehouse), in the line's unit.
    /// Area Z comes from the linked products in Area Z; the warehouse is the rest
    /// (the warehouse column for Metro/SH, otherwise the total), since Area Z moves are already in it.
    /// </summary>
    private async Task FillSplitAsync(List<StockItemDto> items, CancellationToken ct)
    {
        var z = await _db.AreaZInventory.AsNoTracking()
            .Where(a => !a.IsDispatched && a.Product.StockItemId != null)
            .Select(a => new { Sid = a.Product.StockItemId!.Value, a.BundleCount, a.UnitsPerBundle, W = a.Warehouse.Name, WOrder = a.Warehouse.SortOrder })
            .ToListAsync(ct);
        var whCol = await _db.StockItems.AsNoTracking().Select(s => new { s.Id, s.Warehouse }).ToDictionaryAsync(s => s.Id, s => s.Warehouse, ct);

        foreach (var i in items)
        {
            if (i.QtyText is not null) continue;            // codes written as "2×20": no split
            var cartons = StockItem.CountsInCartons(i.Family);
            i.AreaZ = z.Where(a => a.Sid == i.Id)
                .GroupBy(a => new { a.W, a.WOrder }).OrderBy(g => g.Key.WOrder)
                .Select(g => new WhereQty(g.Key.W, g.Sum(a => cartons ? a.BundleCount : a.BundleCount * a.UnitsPerBundle)))
                .ToList();
            var baseQty = whCol.GetValueOrDefault(i.Id) ?? i.Total;
            if (baseQty is null && i.AreaZ.Count == 0) continue;   // no numbers at all yet
            i.Shelves = (baseQty ?? 0) - i.AreaZ.Sum(a => a.Qty);
        }
    }

    public async Task<StockItemDetailViewModel?> GetItemAsync(int id, CancellationToken ct = default)
    {
        var item = await _db.StockItems.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (item is null) return null;
        var linked = await _db.Products.AsNoTracking().Where(p => p.StockItemId == id)
            .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(ct);
        var moves = await _db.StockMovements.AsNoTracking().Where(m => m.StockItemId == id)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(100).ToListAsync(ct);
        var cartons = StockItem.CountsInCartons(item.Family);
        var shelves = (await _db.ShelfInventory.AsNoTracking()
                .Where(si => si.Product.StockItemId == id)
                .Select(si => new { si.Shelf.Code, si.Position, si.BundleCount, si.UnitsPerBundle })
                .ToListAsync(ct))
            .OrderBy(s => s.Code).ThenBy(s => s.Position)
            .Select(s => new WhereQty($"{s.Code} · {s.Position}", cartons ? s.BundleCount : s.BundleCount * s.UnitsPerBundle))
            .ToList();
        var areaZ = (await _db.AreaZInventory.AsNoTracking()
                .Where(a => !a.IsDispatched && a.Product.StockItemId == id)
                .Select(a => new { a.Warehouse.Name, a.Warehouse.SortOrder, a.BundleCount, a.UnitsPerBundle })
                .ToListAsync(ct))
            .GroupBy(a => new { a.Name, a.SortOrder }).OrderBy(g => g.Key.SortOrder)
            .Select(g => new WhereQty(g.Key.Name, g.Sum(a => cartons ? a.BundleCount : a.BundleCount * a.UnitsPerBundle)))
            .ToList();

        return new StockItemDetailViewModel
        {
            Shelves = shelves,
            AreaZ = areaZ,
            Item = item,
            LinkedProducts = linked.Select(p => (p.Id, p.Name)).ToList(),
            Movements = moves
        };
    }

    public async Task UpdateAsync(int id, int? warehouse, int? shops, int? ordered, int? onWay, int? total,
        string? note, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("No authenticated user in context.");
        var item = await _db.StockItems.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("صنف المخزون غير موجود.");

        if (item.Warehouse == warehouse && item.Shops == shops && item.Ordered == ordered
            && item.OnWay == onWay && item.Total == total)
            return;

        var before = item.Total;
        item.SetValues(warehouse, shops, ordered, onWay, total, userId);
        var reason = "تعديل يدوي" + (string.IsNullOrWhiteSpace(note) ? "" : $" — {note.Trim()}");
        _db.StockMovements.Add(StockMovement.Create(item, total.HasValue && before.HasValue ? total - before : null,
            before, total, reason.Length > 300 ? reason[..300] : reason, userId, _currentUser.UserName));
        await _db.SaveChangesAsync(ct);

        await _activityLog.RecordAsync(ActivityArea.Stock,
            $"عدّل مخزون «{item.DisplayName}» — المجموع من {before?.ToString() ?? "—"} إلى {total?.ToString() ?? "—"}", ct);
    }

    public async Task<IReadOnlyList<StockItemDto>> ListForLinkingAsync(CancellationToken ct = default)
        => await Dtos().Where(d => d.Family != "metro-code").ToListAsync(ct);

    public async Task<StockItemDto?> GetLinkForProductAsync(int productId, CancellationToken ct = default)
    {
        var id = await _db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.StockItemId).FirstOrDefaultAsync(ct);
        return id is null ? null : await Dtos().FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task LinkProductAsync(int productId, int? stockItemId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("No authenticated user in context.");
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new DomainException("المنتج غير موجود.");
        if (product.StockItemId == stockItemId) return;

        StockItem? item = null;
        if (stockItemId is not null)
            item = await _db.StockItems.FirstOrDefaultAsync(s => s.Id == stockItemId, ct)
                ?? throw new DomainException("صنف المخزون غير موجود.");

        product.LinkStock(stockItemId, userId);
        await _db.SaveChangesAsync(ct);
        await _activityLog.RecordAsync(ActivityArea.Stock, item is null
            ? $"ألغى ربط «{product.Name}» بالمخزون"
            : $"ربط «{product.Name}» بمخزون «{item.DisplayName}»", ct);
    }

    // ─── Excel download for preparing orders ───────────────────────────
    public async Task<byte[]> ExportExcelAsync(CancellationToken ct = default)
    {
        var items = await _db.StockItems.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        using var wb = new XLWorkbook();
        wb.RightToLeft = true;

        void Drawers(string family, string sheet)
        {
            var ws = wb.Worksheets.Add(sheet);
            ws.RightToLeft = true;
            var h = new[] { "الصنف", "اللون", "المخزن", "محلات", "للطلب", "بالطريق", "مجموع" };
            for (int c = 0; c < h.Length; c++) ws.Cell(1, c + 1).Value = h[c];
            int r = 2;
            foreach (var s in items.Where(i => i.Family == family))
            {
                ws.Cell(r, 1).Value = s.Name;
                ws.Cell(r, 2).Value = s.ColorLabel;
                Set(ws.Cell(r, 3), s.Warehouse); Set(ws.Cell(r, 4), s.Shops); Set(ws.Cell(r, 5), s.Ordered);
                Set(ws.Cell(r, 6), s.OnWay); Set(ws.Cell(r, 7), s.Total);
                r++;
            }
            Style(ws);
        }

        Drawers("metro", "جرار ميترو");
        Drawers("sh", "جرار SH");

        {
            var ws = wb.Worksheets.Add("ميترو أكواد وتندم");
            ws.RightToLeft = true;
            ws.Cell(1, 1).Value = "الصنف"; ws.Cell(1, 2).Value = "اللون"; ws.Cell(1, 3).Value = "الكمية";
            int r = 2;
            foreach (var s in items.Where(i => i.Family is "metro-code" or "metro-tandem"))
            {
                ws.Cell(r, 1).Value = s.Name; ws.Cell(r, 2).Value = s.ColorLabel;
                if (s.QtyText is not null) ws.Cell(r, 3).Value = s.QtyText; else Set(ws.Cell(r, 3), s.Total);
                r++;
            }
            Style(ws);
        }

        {
            var ws = wb.Worksheets.Add("براغي اتيكو");
            ws.RightToLeft = true;
            var h = new[] { "الصنف", "العدد", "Carton", "Box", "ordering" };
            for (int c = 0; c < h.Length; c++) ws.Cell(1, c + 1).Value = h[c];
            int r = 2;
            foreach (var s in items.Where(i => i.Family == "screws"))
            {
                var x = Extra(s);
                ws.Cell(r, 1).Value = s.Name; Set(ws.Cell(r, 2), s.Total);
                ws.Cell(r, 3).Value = x.GetValueOrDefault("Carton")?.ToString() ?? "";
                ws.Cell(r, 4).Value = x.GetValueOrDefault("Box")?.ToString() ?? "";
                ws.Cell(r, 5).Value = x.GetValueOrDefault("ordering")?.ToString() ?? "";
                r++;
            }
            Style(ws);
        }

        {
            var ws = wb.Worksheets.Add("الكنت");
            ws.RightToLeft = true;
            var kent = items.Where(i => i.Family == "kent").ToList();
            var orderCols = kent.SelectMany(k => Extra(k).Keys).Distinct().ToList();
            ws.Cell(1, 1).Value = "الاسم"; ws.Cell(1, 2).Value = "العدد";
            for (int c = 0; c < orderCols.Count; c++) ws.Cell(1, c + 3).Value = orderCols[c];
            int r = 2;
            foreach (var k in kent)
            {
                var x = Extra(k);
                ws.Cell(r, 1).Value = k.Name; Set(ws.Cell(r, 2), k.Total);
                for (int c = 0; c < orderCols.Count; c++)
                    if (x.TryGetValue(orderCols[c], out var v) && v is not null) ws.Cell(r, c + 3).Value = v.ToString();
                r++;
            }
            Style(ws);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();

        static void Set(IXLCell cell, int? v) { if (v.HasValue) cell.Value = v.Value; }
        static void Style(IXLWorksheet ws)
        {
            var head = ws.Row(1);
            head.Style.Font.Bold = true;
            head.Style.Fill.BackgroundColor = XLColor.FromHtml("#CA292E");
            head.Style.Font.FontColor = XLColor.White;
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();
        }
    }

    private static Dictionary<string, object?> Extra(StockItem s)
    {
        if (string.IsNullOrEmpty(s.ExtraJson)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(s.ExtraJson)!
                .ToDictionary(kv => kv.Key, kv => (object?)(kv.Value.ValueKind switch
                {
                    JsonValueKind.Number => kv.Value.GetDouble(),
                    JsonValueKind.String => kv.Value.GetString(),
                    _ => null
                }));
        }
        catch (JsonException) { return new(); }
    }

    // ─── Match with the warehouse ───────────────────────────────────────
    private static readonly string[] WarehouseFamilies = { "metro", "sh", "screws", "kent" };

    /// <summary>
    /// 1. Links every unlinked product (shelves, Area Z, catalog) to its Metro/SH/screws/Kent line by name.
    /// 2. Sets every line that has linked products to exactly shelves + Area Z (pieces for drawers,
    ///    cartons for screws/Kent). Lines with no linked product are left as they are.
    /// The previous number stays in each line's history.
    /// </summary>
    public static async Task<(int Linked, int Changed)> MatchWithWarehouseAsync(
        AppDbContext db, string? userId, string? userName, CancellationToken ct = default)
    {
        var lines = await db.StockItems.Where(s => WarehouseFamilies.Contains(s.Family)).OrderBy(s => s.SortOrder).ToListAsync(ct);

        var linked = 0;
        foreach (var p in await db.Products.Where(p => p.StockItemId == null).ToListAsync(ct))
        {
            var line = StockMatcher.Match(p.Name, lines);
            if (line is null) continue;
            p.LinkStock(line.Id, userId ?? "import");
            linked++;
        }
        await db.SaveChangesAsync(ct);

        var ids = lines.Select(l => l.Id).ToList();
        var shelf = await db.ShelfInventory.AsNoTracking()
            .Where(si => si.Product.StockItemId != null && ids.Contains(si.Product.StockItemId!.Value))
            .Select(si => new { Sid = si.Product.StockItemId!.Value, si.BundleCount, si.UnitsPerBundle }).ToListAsync(ct);
        var z = await db.AreaZInventory.AsNoTracking()
            .Where(a => !a.IsDispatched && a.Product.StockItemId != null && ids.Contains(a.Product.StockItemId!.Value))
            .Select(a => new { Sid = a.Product.StockItemId!.Value, a.BundleCount, a.UnitsPerBundle }).ToListAsync(ct);
        var hasLink = (await db.Products.Where(p => p.StockItemId != null && ids.Contains(p.StockItemId!.Value))
            .Select(p => p.StockItemId!.Value).Distinct().ToListAsync(ct)).ToHashSet();

        var changed = 0;
        foreach (var line in lines.Where(l => hasLink.Contains(l.Id)))
        {
            var cartons = StockItem.CountsInCartons(line.Family);
            var qty = shelf.Where(s => s.Sid == line.Id).Sum(s => cartons ? s.BundleCount : s.BundleCount * s.UnitsPerBundle)
                    + z.Where(a => a.Sid == line.Id).Sum(a => cartons ? a.BundleCount : a.BundleCount * a.UnitsPerBundle);
            var drawer = line.Family is "metro" or "sh";
            if (line.Total == qty && (!drawer || line.Warehouse == qty)) continue;
            var before = line.Total;
            line.SetValues(drawer ? qty : line.Warehouse, line.Shops, line.Ordered, line.OnWay, qty, userId ?? "import");
            db.StockMovements.Add(StockMovement.Create(line, qty - (before ?? 0), before, qty,
                "مطابقة مع المستودع (الرفوف + Area Z)", userId, userName ?? "مطابقة"));
            changed++;
        }
        await db.SaveChangesAsync(ct);
        return (linked, changed);
    }

    public async Task<string> ConnectProductAsync(int productId, string target, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("No authenticated user in context.");
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new DomainException("المنتج غير موجود.");

        StockItem line;
        if (target.StartsWith("line:") && int.TryParse(target[5..], out var lineId))
        {
            line = await _db.StockItems.FirstOrDefaultAsync(s => s.Id == lineId, ct)
                ?? throw new DomainException("صنف المخزون غير موجود.");
        }
        else if (target.StartsWith("new:") && FamilyNames.ContainsKey(target[4..]) && !target.EndsWith("metro-code") && !target.EndsWith("metro-tandem"))
        {
            var family = target[4..];
            var sort = (await _db.StockItems.MaxAsync(s => (int?)s.SortOrder, ct) ?? 0) + 1;
            line = StockItem.Create(family, product.Name.Trim(), null, "أخرى", null, null, sort,
                null, family is "metro" or "sh" ? 0 : null, null, null, null, 0, null, null);
            _db.StockItems.Add(line);
            await _db.SaveChangesAsync(ct);
        }
        else throw new DomainException("اختر صنفاً من القائمة.");

        product.LinkStock(line.Id, userId);
        await _db.SaveChangesAsync(ct);

        // The line now follows the warehouse: set it to everything its products have on shelves + Area Z
        var cartons = StockItem.CountsInCartons(line.Family);
        var shelf = await _db.ShelfInventory.Where(si => si.Product.StockItemId == line.Id)
            .Select(si => new { si.BundleCount, si.UnitsPerBundle }).ToListAsync(ct);
        var z = await _db.AreaZInventory.Where(a => !a.IsDispatched && a.Product.StockItemId == line.Id)
            .Select(a => new { a.BundleCount, a.UnitsPerBundle }).ToListAsync(ct);
        var qty = shelf.Sum(s => cartons ? s.BundleCount : s.BundleCount * s.UnitsPerBundle)
                + z.Sum(a => cartons ? a.BundleCount : a.BundleCount * a.UnitsPerBundle);
        var before = line.Total;
        if (before != qty || (line.Family is "metro" or "sh" && line.Warehouse != qty))
        {
            line.SetValues(line.Family is "metro" or "sh" ? qty : line.Warehouse, line.Shops, line.Ordered, line.OnWay, qty, userId);
            _db.StockMovements.Add(StockMovement.Create(line, qty - (before ?? 0), before, qty,
                $"ربط «{product.Name}» — مطابقة مع المستودع", userId, _currentUser.UserName));
            await _db.SaveChangesAsync(ct);
        }
        await _activityLog.RecordAsync(ActivityArea.Stock, $"ربط «{product.Name}» بمخزون «{line.DisplayName}»", ct);
        return line.DisplayName;
    }

    public async Task<ConnectViewModel?> GetConnectAsync(int productId, CancellationToken ct = default)
    {
        var p = await _db.Products.AsNoTracking().Where(x => x.Id == productId)
            .Select(x => new
            {
                x.Id, x.Name, x.StockItemId,
                Shelves = x.ShelfInventories.Select(si => si.Shelf.Code).ToList(),
                InZ = x.AreaZInventories.Any(a => !a.IsDispatched)
            }).FirstOrDefaultAsync(ct);
        if (p is null) return null;
        var lines = await _db.StockItems.AsNoTracking().Where(s => s.QtyText == null && s.Family != "metro-tandem")
            .OrderBy(s => s.SortOrder).ToListAsync(ct);
        var suggested = StockMatcher.Match(p.Name, lines);
        return new ConnectViewModel
        {
            ProductId = p.Id, ProductName = p.Name, CurrentId = p.StockItemId, SuggestedId = suggested?.Id,
            GuessFamily = suggested?.Family ?? GuessFamily(p.Name),
            Where = string.Join(" ", p.Shelves.Distinct().OrderBy(x => x)) + (p.InZ ? " Area Z" : ""),
            Lines = lines.Select(l => new StockItemDto { Id = l.Id, Family = l.Family, Name = l.Name, Color = l.Color, Total = l.Total }).ToList()
        };
    }

    public async Task<LinkCandidatesViewModel?> GetLinkCandidatesAsync(int stockItemId, CancellationToken ct = default)
    {
        var item = await _db.StockItems.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stockItemId, ct);
        if (item is null) return null;
        var rows = await _db.Products.AsNoTracking()
            .Where(p => p.StockItemId != stockItemId && (p.ShelfInventories.Any() || p.AreaZInventories.Any(a => !a.IsDispatched)))
            .Select(p => new
            {
                p.Id, p.Name,
                Shelves = p.ShelfInventories.Select(si => si.Shelf.Code).ToList(),
                InZ = p.AreaZInventories.Any(a => !a.IsDispatched),
                LinkedTo = p.StockItem == null ? null : p.StockItem.Name
            }).ToListAsync(ct);
        var fam = item.Family;
        return new LinkCandidatesViewModel
        {
            Item = item,
            Products = rows
                .OrderBy(r => r.LinkedTo != null)                       // not connected first
                .ThenBy(r => GuessFamily(r.Name) == fam ? 0 : 1)         // same family next
                .ThenBy(r => r.Name)
                .Select(r => (r.Id, r.Name, string.Join(" ", r.Shelves.Distinct().OrderBy(x => x)) + (r.InZ ? " Area Z" : ""), r.LinkedTo))
                .ToList()
        };
    }

    public static string? GuessFamily(string n)
    {
        var x = n.ToLowerInvariant();
        if (x.Contains("كنت")) return "kent";
        if (x.Contains("براغي") || x.Contains("برغي")) return "screws";
        if (x.Contains("حف") || x.Contains("سفنج")) return "smirdex";
        if (x.Contains("ميترو") || x.Contains("metro")) return "metro";
        if (System.Text.RegularExpressions.Regex.IsMatch(x, @"\bsh\b")) return "sh";
        return null;
    }

    public async Task<(int Linked, int Changed)> MatchNowAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("No authenticated user in context.");
        var r = await MatchWithWarehouseAsync(_db, userId, _currentUser.UserName, ct);
        await _activityLog.RecordAsync(ActivityArea.Stock,
            $"طابق المخزون مع المستودع — ربط {r.Linked} منتج، تعديل {r.Changed} صنف", ct);
        return r;
    }

    /// <summary>What still doesn't match: lines with no product in the warehouse, and products that look like
    /// Metro/SH/screws/Kent but couldn't be matched to a line.</summary>
    public async Task<StockCheckViewModel> GetCheckAsync(CancellationToken ct = default)
    {
        var lines = await _db.StockItems.AsNoTracking().Where(s => WarehouseFamilies.Contains(s.Family)).OrderBy(s => s.SortOrder).ToListAsync(ct);
        var linkedIds = (await _db.Products.Where(p => p.StockItemId != null).Select(p => p.StockItemId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
        var noProduct = lines.Where(l => !linkedIds.Contains(l.Id) && l.QtyText == null)
            .Select(l => new StockItemDto { Id = l.Id, Family = l.Family, Name = l.Name, Color = l.Color, Total = l.Total }).ToList();

        var look = new System.Text.RegularExpressions.Regex(@"ميترو|metro|\bsh\b|كنت|براغي|اتيكو", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var candidates = await _db.Products.AsNoTracking().Where(p => p.StockItemId == null)
            .Select(p => new
            {
                p.Id, p.Name,
                Shelves = p.ShelfInventories.Select(si => si.Shelf.Code).ToList(),
                InZ = p.AreaZInventories.Any(a => !a.IsDispatched)
            }).ToListAsync(ct);
        var proper = lines.Where(l => l.DrawerType != "أخرى").ToList();
        var unmatched = candidates.Where(c => look.IsMatch(c.Name) && (c.Shelves.Count > 0 || c.InZ))
            .Select(c => (c.Id, c.Name, Where: string.Join(" ", c.Shelves.Distinct().OrderBy(x => x)) + (c.InZ ? " Area Z" : ""),
                          SuggestedId: StockMatcher.Match(c.Name, proper)?.Id))
            .OrderBy(c => c.Name).ToList();
        var all = lines.Concat(await _db.StockItems.AsNoTracking().Where(s => s.Family == "smirdex").OrderBy(s => s.SortOrder).ToListAsync(ct))
            .Where(l => l.QtyText == null)
            .Select(l => new StockItemDto { Id = l.Id, Family = l.Family, Name = l.Name, Color = l.Color, Total = l.Total }).ToList();
        return new StockCheckViewModel { LinesWithoutProduct = noProduct, UnmatchedProducts = unmatched, AllLines = all };
    }
}

public class StockCheckViewModel
{
    public IReadOnlyList<StockItemDto> LinesWithoutProduct { get; init; } = Array.Empty<StockItemDto>();
    public IReadOnlyList<(int Id, string Name, string Where, int? SuggestedId)> UnmatchedProducts { get; init; } = Array.Empty<(int, string, string, int?)>();
    public IReadOnlyList<StockItemDto> AllLines { get; init; } = Array.Empty<StockItemDto>();
}
