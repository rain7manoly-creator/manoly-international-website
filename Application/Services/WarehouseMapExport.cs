using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Application.Services;

/// <summary>
/// Builds the "Rain New WareHouse" Excel straight from the shelves, in the same layout
/// the team kept by hand:
///   • left half  (cols A–D): shelves A/B/C, odd number in A–B, even number in C–D,
///     header "1-A" then "مشتاح رقم 1…6"
///   • right half (cols G–J): shelves D/E/F (and G/H when used), D rows numbered by position
///   • each item written "name_____ 24 كرتونة*6"
/// </summary>
public interface IWarehouseMapExport
{
    Task<byte[]> BuildAsync(CancellationToken ct = default);
}

public class WarehouseMapExport : IWarehouseMapExport
{
    private readonly AppDbContext _db;
    public WarehouseMapExport(AppDbContext db) => _db = db;

    private sealed record Slot(int Position, string Text);

    public async Task<byte[]> BuildAsync(CancellationToken ct = default)
    {
        var shelves = await _db.Shelves.AsNoTracking()
            .Select(s => new
            {
                s.Label, s.Number, s.MaxPositions,
                Items = s.Inventories
                    .OrderBy(i => i.Position)
                    .Select(i => new { i.Position, i.Product.Name, i.BundleCount, i.UnitsPerBundle })
                    .ToList()
            })
            .ToListAsync(ct);

        var byCode = shelves.ToDictionary(
            s => (s.Label, s.Number),
            s => (s.MaxPositions, Slots: s.Items.Select(i => new Slot(i.Position, ItemText(i.Name, i.BundleCount, i.UnitsPerBundle))).ToList()));

        int MaxNumber(params string[] labels) =>
            shelves.Where(s => labels.Contains(s.Label)).Select(s => s.Number).DefaultIfEmpty(0).Max();
        var lastNumber = Math.Max(MaxNumber("A", "B", "C"), MaxNumber("D", "E", "F", "G", "H"));

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("ورقة1");
        ws.RightToLeft = true;
        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 11;
        ws.SheetView.ZoomScale = 83;

        foreach (var (col, w) in new[] { ("A", 10.0), ("B", 56.3), ("C", 12.3), ("D", 54.1), ("E", 10.0), ("F", 41.2), ("G", 10.0), ("H", 71.7), ("I", 11.4), ("J", 84.0) })
            ws.Column(col).Width = w;

        Title(ws.Range("A1:D2"));
        Title(ws.Range("G1:J2"));

        var row = 4;
        for (var n = 1; n <= lastNumber; n += 2)
        {
            // Three bands per pair of shelf numbers: A|D, B|E, C|F. G/H follow under F when they hold anything.
            var bands = new List<(string? Left, string? Right)> { ("A", "D"), ("B", "E"), ("C", "F") };
            if (Has("G", n) || Has("G", n + 1)) bands.Add((null, "G"));
            if (Has("H", n) || Has("H", n + 1)) bands.Add((null, "H"));

            foreach (var (left, right) in bands)
            {
                var leftRows = left is null ? 0 : WriteBlock(ws, row, 1, left, n, numbered: false, pallets: true);
                leftRows = Math.Max(leftRows, left is null ? 0 : WriteBlock(ws, row, 3, left, n + 1, numbered: false, pallets: true));
                var rightRows = right is null ? 0 : WriteBlock(ws, row, 7, right, n, numbered: right == "D", pallets: false);
                rightRows = Math.Max(rightRows, right is null ? 0 : WriteBlock(ws, row, 9, right, n + 1, numbered: right == "D", pallets: false));
                var height = Math.Max(leftRows, rightRows);
                if (height > 0) row += height;
            }
            row += 2; // blank rows between groups, like the original
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();

        bool Has(string label, int number) =>
            byCode.TryGetValue((label, number), out var s) && s.Slots.Count > 0;

        // Writes one shelf (header + rows) at (row, col); returns how many rows it used (0 if the shelf doesn't exist).
        int WriteBlock(IXLWorksheet sheet, int top, int col, string label, int number, bool numbered, bool pallets)
        {
            if (!byCode.TryGetValue((label, number), out var shelf)) return 0;
            if (shelf.Slots.Count == 0 && (label == "G" || label == "H")) return 0;

            var head = sheet.Cell(top, col);
            head.Value = $"{number}-{label}";
            head.Style.Font.Bold = true;
            head.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            head.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            Box(sheet.Range(top, col, top, col + 1));

            // A/B/C: always the 6 pallets. Racks: rows up to the last used position (at least 6).
            var rows = pallets
                ? 6
                : Math.Max(6, shelf.Slots.Select(s => s.Position).DefaultIfEmpty(0).Max());

            for (var p = 1; p <= rows; p++)
            {
                var r = top + p;
                var label1 = sheet.Cell(r, col);
                if (pallets) label1.Value = $"مشتاح رقم {p}";
                else if (numbered && shelf.Slots.Any(s => s.Position == p))
                {
                    label1.Value = p;
                    label1.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    label1.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }
                var slot = shelf.Slots.FirstOrDefault(s => s.Position == p);
                if (slot is not null) sheet.Cell(r, col + 1).Value = slot.Text;
                Box(sheet.Range(r, col, r, col + 1));
            }
            return rows + 1;
        }
    }

    /// <summary>"جرار ميترو زجاج ابيض 50/8.5_____ 24 كرتونة*6"; single pieces as "… 13 Pieces".</summary>
    public static string ItemText(string name, int bundles, int unitsPerBundle) =>
        unitsPerBundle > 1
            ? $"{name}_____ {bundles} كرتونة*{unitsPerBundle}"
            : $"{name}_____ {bundles} كرتونة";

    private static void Title(IXLRange range)
    {
        range.Merge();
        range.Value = "Rain New WareHouse";
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 20;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        Box(range);
    }

    private static void Box(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }
}
