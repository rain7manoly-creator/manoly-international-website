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
}

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
            })
            .ToListAsync(ct);
        return new StockPageViewModel { Items = items, OtherProducts = others };
    }

    public async Task<StockItemDetailViewModel?> GetItemAsync(int id, CancellationToken ct = default)
    {
        var item = await _db.StockItems.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (item is null) return null;
        var linked = await _db.Products.AsNoTracking().Where(p => p.StockItemId == id)
            .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(ct);
        var moves = await _db.StockMovements.AsNoTracking().Where(m => m.StockItemId == id)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(100).ToListAsync(ct);
        return new StockItemDetailViewModel
        {
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
}
