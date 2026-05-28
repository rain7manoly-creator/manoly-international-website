using ManolyWarehouse.Domain.Common;

namespace ManolyWarehouse.Domain.Entities;

public class ShelfInventory : AuditableEntity
{
    public int Id { get; private set; }
    public int ShelfId { get; private set; }
    public Shelf Shelf { get; private set; } = default!;
    public int ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public int Position { get; private set; }
    public int BundleCount { get; private set; }
    public int UnitsPerBundle { get; private set; }
    public int? OrderItemId { get; private set; }
    public PurchaseOrderItem? OrderItem { get; private set; }

    public int TotalQuantity => BundleCount * UnitsPerBundle;

    private readonly List<InventoryAdjustmentLog> _adjustmentLogs = new();
    public IReadOnlyCollection<InventoryAdjustmentLog> AdjustmentLogs => _adjustmentLogs.AsReadOnly();

    private ShelfInventory() { }

    public static ShelfInventory Create(
        int shelfId, int productId, int position,
        int bundleCount, int unitsPerBundle,
        string addedBy, int? orderItemId = null)
    {
        var inventory = new ShelfInventory
        {
            ShelfId = shelfId,
            ProductId = productId,
            Position = position,
            BundleCount = bundleCount,
            UnitsPerBundle = unitsPerBundle,
            OrderItemId = orderItemId
        };
        inventory.SetCreated(addedBy);
        return inventory;
    }

    /// <summary>
    /// Returns true when the row should be deleted (quantity hit zero).
    /// Adjustment log is auto-created.
    /// </summary>
    public bool Adjust(int newBundleCount, int newUnitsPerBundle, string updatedBy, string? notes = null)
    {
        var log = InventoryAdjustmentLog.Create(
            BundleCount, newBundleCount,
            UnitsPerBundle, newUnitsPerBundle, updatedBy, notes);

        _adjustmentLogs.Add(log);

        BundleCount = newBundleCount;
        UnitsPerBundle = newUnitsPerBundle;
        SetUpdated(updatedBy);

        return BundleCount == 0;
    }
}
