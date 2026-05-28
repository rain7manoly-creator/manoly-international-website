using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Application.Services;

public class InventoryPdfExporter : IInventoryPdfExporter
{
    private readonly AppDbContext _db;
    private readonly ILogger<InventoryPdfExporter> _logger;

    public InventoryPdfExporter(AppDbContext db, ILogger<InventoryPdfExporter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<byte[]> GenerateInventorySnapshotAsync(CancellationToken ct = default)
    {
        var rows = await _db.ShelfInventory
            .AsNoTracking()
            .OrderBy(si => si.Shelf.Side)
            .ThenBy(si => si.Shelf.Number)
            .ThenBy(si => si.Shelf.Label)
            .ThenBy(si => si.Position)
            .Select(si => new InventoryRow
            {
                ShelfCode = si.Shelf.Code,
                Position = si.Position,
                ProductName = si.Product.Name,
                CategoryName = si.Product.Category.Name,
                BundleCount = si.BundleCount,
                UnitsPerBundle = si.UnitsPerBundle,
                TotalQuantity = si.BundleCount * si.UnitsPerBundle
            })
            .ToListAsync(ct);

        var areaZ = await _db.AreaZInventory
            .AsNoTracking()
            .Where(az => !az.IsDispatched)
            .OrderBy(az => az.Product.Name)
            .Select(az => new InventoryRow
            {
                ShelfCode = "منطقة Z",
                Position = 0,
                ProductName = az.Product.Name,
                CategoryName = az.Product.Category.Name,
                BundleCount = az.BundleCount,
                UnitsPerBundle = az.UnitsPerBundle,
                TotalQuantity = az.BundleCount * az.UnitsPerBundle
            })
            .ToListAsync(ct);

        var snapshot = new InventorySnapshot
        {
            GeneratedAt = DateTime.UtcNow,
            ShelfRows = rows,
            AreaZRows = areaZ
        };

        _logger.LogInformation(
            "Generating inventory PDF: {ShelfRows} shelf rows, {AreaZRows} Area Z rows",
            rows.Count, areaZ.Count);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.PageColor(Colors.White);

                page.Header().Element(c => ComposeHeader(c, snapshot));
                page.Content().Element(c => ComposeContent(c, snapshot));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();

        return pdf;
    }

    private static void ComposeHeader(IContainer container, InventorySnapshot snapshot)
    {
        container.Column(col =>
        {
            col.Item().Text("جرد المستودع — Manoly International").FontSize(16).Bold();
            col.Item().Text($"تاريخ التقرير: {snapshot.GeneratedAt:yyyy-MM-dd HH:mm} UTC")
                .FontSize(9).FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(8).LineHorizontal(0.5f);
        });
    }

    private static void ComposeContent(IContainer container, InventorySnapshot snapshot)
    {
        container.PaddingVertical(12).Column(col =>
        {
            col.Item().Text("الرفوف").FontSize(13).Bold();
            col.Item().PaddingTop(4).Element(c => ComposeTable(c, snapshot.ShelfRows));

            if (snapshot.AreaZRows.Count > 0)
            {
                col.Item().PaddingTop(16).Text("منطقة Z").FontSize(13).Bold();
                col.Item().PaddingTop(4).Element(c => ComposeTable(c, snapshot.AreaZRows));
            }

            col.Item().PaddingTop(16).Text(
                $"الإجمالي: {snapshot.ShelfRows.Count + snapshot.AreaZRows.Count} صف")
                .FontSize(10).FontColor(Colors.Grey.Darken2);
        });
    }

    private static void ComposeTable(IContainer container, IReadOnlyList<InventoryRow> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);   // Shelf
                c.RelativeColumn(1);   // Position
                c.RelativeColumn(4);   // Product
                c.RelativeColumn(3);   // Category
                c.RelativeColumn(2);   // Bundles
                c.RelativeColumn(2);   // Units/bundle
                c.RelativeColumn(2);   // Total
            });

            table.Header(h =>
            {
                static IContainer HeaderCell(IContainer c) =>
                    c.Background(Colors.Grey.Lighten3).Padding(5).DefaultTextStyle(x => x.SemiBold());

                h.Cell().Element(HeaderCell).Text("الرف");
                h.Cell().Element(HeaderCell).Text("الموضع");
                h.Cell().Element(HeaderCell).Text("المنتج");
                h.Cell().Element(HeaderCell).Text("التصنيف");
                h.Cell().Element(HeaderCell).Text("الربطات");
                h.Cell().Element(HeaderCell).Text("وحدات/ربطة");
                h.Cell().Element(HeaderCell).Text("الإجمالي");
            });

            foreach (var row in rows)
            {
                static IContainer Cell(IContainer c) =>
                    c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);

                table.Cell().Element(Cell).Text(row.ShelfCode);
                table.Cell().Element(Cell).Text(row.Position == 0 ? "—" : row.Position.ToString());
                table.Cell().Element(Cell).Text(row.ProductName);
                table.Cell().Element(Cell).Text(row.CategoryName);
                table.Cell().Element(Cell).Text(row.BundleCount.ToString());
                table.Cell().Element(Cell).Text(row.UnitsPerBundle.ToString());
                table.Cell().Element(Cell).Text(row.TotalQuantity.ToString());
            }
        });
    }

    private sealed class InventorySnapshot
    {
        public DateTime GeneratedAt { get; init; }
        public IReadOnlyList<InventoryRow> ShelfRows { get; init; } = Array.Empty<InventoryRow>();
        public IReadOnlyList<InventoryRow> AreaZRows { get; init; } = Array.Empty<InventoryRow>();
    }

    private sealed class InventoryRow
    {
        public string ShelfCode { get; init; } = default!;
        public int Position { get; init; }
        public string ProductName { get; init; } = default!;
        public string CategoryName { get; init; } = default!;
        public int BundleCount { get; init; }
        public int UnitsPerBundle { get; init; }
        public int TotalQuantity { get; init; }
    }
}
