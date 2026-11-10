using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Application.Services;

/// <summary>
/// Produces the team's own stock files (SH, Metro, براغي اتيكو, كنت) from the app.
///
/// Each file starts from a template — the team's original Excel with the numbers
/// cleared — so columns, row order, widths, fonts and totals rows are exactly theirs.
/// A cell map (Templates/{name}.json) says which stock line and which field goes in
/// each cell; the app writes its current numbers there.
/// </summary>
public interface IStockFileExport
{
    /// <summary>key: sh · metro · screws · kent. Returns null for an unknown key.</summary>
    Task<(byte[] Bytes, string FileName)?> BuildAsync(string key, CancellationToken ct = default);
}

public class StockFileExport : IStockFileExport
{
    private const string Prefix = "ManolyWarehouse.Infrastructure.Templates.";

    private static readonly Dictionary<string, (string Template, string FileName)> Files = new()
    {
        ["sh"] = ("SH", "SH"),
        ["metro"] = ("Metro", "ميترو"),
        ["screws"] = ("Screws", "براغي اتيكو"),
        ["kent"] = ("Kent", "كنت"),
    };

    private sealed record CellMap(string cell, string family, string name, string? color, int occ, string field);

    private readonly AppDbContext _db;
    public StockFileExport(AppDbContext db) => _db = db;

    public async Task<(byte[] Bytes, string FileName)?> BuildAsync(string key, CancellationToken ct = default)
    {
        if (!Files.TryGetValue(key, out var f)) return null;

        var asm = typeof(StockFileExport).Assembly;
        await using var tpl = asm.GetManifestResourceStream(Prefix + f.Template + ".xlsx")
            ?? throw new InvalidOperationException($"Template {f.Template}.xlsx missing.");
        await using var mapStream = asm.GetManifestResourceStream(Prefix + f.Template + ".json")
            ?? throw new InvalidOperationException($"Cell map {f.Template}.json missing.");
        var map = await JsonSerializer.DeserializeAsync<List<CellMap>>(mapStream, cancellationToken: ct) ?? new();

        // Lines with the same name (e.g. a code listed twice) are told apart by order
        var families = map.Select(m => m.family).Distinct().ToList();
        var lines = (await _db.StockItems.AsNoTracking()
                .Where(s => families.Contains(s.Family)).OrderBy(s => s.SortOrder).ToListAsync(ct))
            .GroupBy(s => (s.Family, s.Name, s.Color))
            .ToDictionary(g => g.Key, g => g.ToList());

        using var wb = new XLWorkbook(tpl);
        var ws = wb.Worksheet(1);
        foreach (var m in map)
        {
            if (!lines.TryGetValue((m.family, m.name, m.color), out var list) || m.occ >= list.Count) continue;
            var item = list[m.occ];
            var cell = ws.Cell(m.cell);
            switch (m.field)
            {
                case "warehouse": Set(cell, item.Warehouse); break;
                case "shops": Set(cell, item.Shops); break;
                case "ordered": Set(cell, item.Ordered); break;
                case "onWay": Set(cell, item.OnWay); break;
                case "total": Set(cell, item.Total); break;
                case "qtyText":
                    if (item.QtyText is not null) cell.Value = item.QtyText.Replace('×', '*');
                    break;
                default:
                    if (m.field.StartsWith("extra:") && Extra(item).TryGetValue(m.field[6..], out var v) && v.HasValue)
                    {
                        var e = v.Value;
                        if (e.ValueKind == JsonValueKind.Number) cell.Value = e.GetDouble();
                        else if (e.ValueKind == JsonValueKind.String) cell.Value = e.GetString();
                    }
                    break;
            }
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var date = Extensions.LocalTimeExtensions.ToLocal(DateTime.UtcNow).ToString("yyyy-MM-dd");
        return (ms.ToArray(), $"{f.FileName} {date}.xlsx");
    }

    private static void Set(IXLCell cell, int? v)
    {
        if (v.HasValue) cell.Value = v.Value; else cell.Clear(XLClearOptions.Contents);
    }

    private static Dictionary<string, JsonElement?> Extra(StockItem s)
    {
        if (string.IsNullOrEmpty(s.ExtraJson)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(s.ExtraJson)!
                .ToDictionary(kv => kv.Key.Trim(), kv => (JsonElement?)kv.Value);
        }
        catch (JsonException) { return new(); }
    }
}
