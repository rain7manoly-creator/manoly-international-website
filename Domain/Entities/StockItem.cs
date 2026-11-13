namespace ManolyWarehouse.Domain.Entities;

/// <summary>
/// One line of the company stock list (originally the Metro / SH / screws / Kent
/// Excel files). Each colour of a drawer size is its own item.
/// Quantities are single units. Shelf changes on a linked product move
/// <see cref="Warehouse"/> and <see cref="Total"/> automatically (see StockSyncInterceptor).
/// </summary>
public class StockItem
{
    public int Id { get; private set; }

    /// <summary>metro · sh · screws · kent · metro-code · metro-tandem</summary>
    public string Family { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    /// <summary>white · grey · null</summary>
    public string? Color { get; private set; }
    public string? DrawerType { get; private set; }
    public int? Height { get; private set; }
    public int? Length { get; private set; }
    public int SortOrder { get; private set; }

    public string? WarehouseLabel { get; private set; }
    public int? Warehouse { get; private set; }
    public int? Shops { get; private set; }
    public int? Ordered { get; private set; }
    public int? OnWay { get; private set; }
    public int? Total { get; private set; }

    /// <summary>Non-numeric quantity kept exactly as written in the file (e.g. "2×20").</summary>
    public string? QtyText { get; private set; }
    /// <summary>Extra columns from the original file (carton, box, order dates…) as JSON, kept for export.</summary>
    public string? ExtraJson { get; private set; }

    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    private readonly List<StockMovement> _movements = new();
    public IReadOnlyCollection<StockMovement> Movements => _movements.AsReadOnly();

    private StockItem() { }

    public static StockItem Create(
        string family, string name, string? color, string? drawerType, int? height, int? length, int sortOrder,
        string? warehouseLabel, int? warehouse, int? shops, int? ordered, int? onWay, int? total,
        string? qtyText, string? extraJson)
    {
        return new StockItem
        {
            Family = family, Name = name, Color = color, DrawerType = drawerType, Height = height, Length = length,
            SortOrder = sortOrder, WarehouseLabel = warehouseLabel, Warehouse = warehouse, Shops = shops,
            Ordered = ordered, OnWay = onWay, Total = total, QtyText = qtyText, ExtraJson = extraJson,
            UpdatedAt = DateTime.UtcNow, UpdatedBy = "import"
        };
    }

    /// <summary>
    /// A shelf change of <paramref name="deltaUnits"/> (negative = taken). The shelves are the
    /// warehouse, so the warehouse column moves; the total moves by the same amount.
    /// Families without a warehouse column (screws, Kent) only move the total.
    /// </summary>
    public void ApplyShelfDelta(int deltaUnits, string? by)
    {
        if (Warehouse.HasValue) Warehouse += deltaUnits;
        Total = (Total ?? 0) + deltaUnits;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = by;
    }

    /// <summary>Manual correction by an admin (e.g. an order arrived, a count was done).</summary>
    public void SetValues(int? warehouse, int? shops, int? ordered, int? onWay, int? total, string by)
    {
        Warehouse = warehouse; Shops = shops; Ordered = ordered; OnWay = onWay; Total = total;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = by;
    }

    public void SetColor(string? color) => Color = color;

    /// <summary>Warn when the total is at or below this. null = only when finished (≤ 0); -1 = never warn.</summary>
    public int? AlertAt { get; private set; }
    public void SetAlertAt(int? alertAt) => AlertAt = alertAt;
    public void Rename(string name) => Name = name;

    /// <summary>Non-numeric quantity and extra file columns, replaced when a newer Excel file is imported.</summary>
    public void SetFileData(string? qtyText, string? extraJson)
    {
        QtyText = qtyText;
        ExtraJson = extraJson;
    }

    /// <summary>Screws and Kent are counted in cartons in the stock files; drawers in single pieces.</summary>
    public static bool CountsInCartons(string family) => family is "screws" or "kent" or "smirdex"
        || (StockGroupRegistry.Groups.TryGetValue(family, out var g) && g.InCartons);
    public string UnitLabel => CountsInCartons(Family) ? "كرتونة" : "حبة";

    public string ColorLabel => Color switch { "white" => "أبيض", "grey" => "سكني", _ => "" };
    public string DisplayName => string.IsNullOrEmpty(ColorLabel) ? Name : $"{Name} — {ColorLabel}";
}

/// <summary>Append-only history of every change to a stock item, automatic or manual.</summary>
public class StockMovement
{
    public int Id { get; private set; }
    public int StockItemId { get; private set; }
    public StockItem StockItem { get; private set; } = default!;
    public int? Delta { get; private set; }
    public int? TotalBefore { get; private set; }
    public int? TotalAfter { get; private set; }
    /// <summary>Arabic description: «أُخذ من الرف A1 — MDF أبيض…», «تعديل يدوي»…</summary>
    public string Reason { get; private set; } = default!;
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private StockMovement() { }

    public static StockMovement Create(StockItem item, int? delta, int? totalBefore, int? totalAfter,
        string reason, string? userId, string? userName)
    {
        return new StockMovement
        {
            StockItem = item, Delta = delta, TotalBefore = totalBefore, TotalAfter = totalAfter,
            Reason = reason, UserId = userId, UserName = userName, CreatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>Marks a stock update file as applied, so each Excel update is loaded exactly once.</summary>
public class StockImport
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public DateTime AppliedAt { get; private set; }

    private StockImport() { }
    public static StockImport Create(string name) => new() { Name = name, AppliedAt = DateTime.UtcNow };
}
