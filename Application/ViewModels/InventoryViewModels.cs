namespace ManolyWarehouse.Application.ViewModels;

public class InventorySummaryViewModel
{
    public IReadOnlyList<InventoryCategoryGroup> Categories { get; init; } = Array.Empty<InventoryCategoryGroup>();
    /// <summary>All categories that have any stock — used for filter pills, independent of pagination.</summary>
    public IReadOnlyList<InventoryCategoryMeta> AllCategories { get; init; } = Array.Empty<InventoryCategoryMeta>();
    public string? ActiveCategory { get; init; }
    public int Page       { get; init; }
    public int TotalPages { get; init; }
}

public class InventoryCategoryMeta
{
    public string CategoryName { get; init; } = default!;
    public int    ProductCount { get; init; }
}

public class InventoryCategoryGroup
{
    public string CategoryName { get; init; } = default!;
    public int    ProductCount { get; init; }
    public int    TotalBundles { get; init; }
    public IReadOnlyList<InventoryProductRow> Products { get; init; } = Array.Empty<InventoryProductRow>();
}

public class InventoryProductRow
{
    public int    ProductId          { get; init; }
    public string ProductName        { get; init; } = default!;
    public string CategoryName       { get; init; } = default!;
    public int    ShelfBundles       { get; init; }
    public int    ShelfUnits         { get; init; }
    public int    ShelfLocationCount { get; init; }
    public int    AreaZBundles       { get; init; }
    public int    AreaZUnits         { get; init; }
    public int    TotalBundles       => ShelfBundles + AreaZBundles;
    public int    TotalUnits         => ShelfUnits   + AreaZUnits;
    public IReadOnlyList<InventoryShelfLocation> Locations { get; init; } = Array.Empty<InventoryShelfLocation>();
}

public class InventoryShelfLocation
{
    public string ShelfCode      { get; init; } = default!;
    public int    Position       { get; init; }
    public int    BundleCount    { get; init; }
    public int    UnitsPerBundle { get; init; }
    public int    TotalQuantity  => BundleCount * UnitsPerBundle;
}
