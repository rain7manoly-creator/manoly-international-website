namespace ManolyWarehouse.Domain.Entities;

public class InventoryAdjustmentLog
{
    public int Id { get; private set; }
    public int ShelfInventoryId { get; private set; }
    public ShelfInventory ShelfInventory { get; private set; } = default!;
    public int PreviousBundleCount { get; private set; }
    public int NewBundleCount { get; private set; }
    public int PreviousUnitsPerBundle { get; private set; }
    public int NewUnitsPerBundle { get; private set; }
    public string AdjustedBy { get; private set; } = default!;
    public DateTime AdjustedAt { get; private set; }
    public string? Notes { get; private set; }

    private InventoryAdjustmentLog() { }

    /// <summary>
    /// Created via the parent ShelfInventory.Adjust() — ShelfInventoryId
    /// is set by EF Core via the navigation property on SaveChanges.
    /// </summary>
    public static InventoryAdjustmentLog Create(
        int prevBundleCount, int newBundleCount,
        int prevUnitsPerBundle, int newUnitsPerBundle,
        string adjustedBy, string? notes = null)
    {
        return new InventoryAdjustmentLog
        {
            PreviousBundleCount = prevBundleCount,
            NewBundleCount = newBundleCount,
            PreviousUnitsPerBundle = prevUnitsPerBundle,
            NewUnitsPerBundle = newUnitsPerBundle,
            AdjustedBy = adjustedBy,
            AdjustedAt = DateTime.UtcNow,
            Notes = notes
        };
    }
}
