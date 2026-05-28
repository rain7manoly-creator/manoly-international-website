using ManolyWarehouse.Domain.Common;

namespace ManolyWarehouse.Domain.Entities;

public class AreaZInventory : AuditableEntity
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public int BundleCount { get; private set; }
    public int UnitsPerBundle { get; private set; }
    public string? Notes { get; private set; }
    public bool IsDispatched { get; private set; }
    public DateTime? DispatchedAt { get; private set; }
    public string? DispatchedBy { get; private set; }

    public int TotalQuantity => BundleCount * UnitsPerBundle;

    private AreaZInventory() { }

    public static AreaZInventory Create(
        int productId, int bundleCount, int unitsPerBundle,
        string enteredBy, string? notes = null)
    {
        var entry = new AreaZInventory
        {
            ProductId = productId,
            BundleCount = bundleCount,
            UnitsPerBundle = unitsPerBundle,
            Notes = notes,
            IsDispatched = false
        };
        entry.SetCreated(enteredBy);
        return entry;
    }

    public void Update(int bundleCount, int unitsPerBundle, string? notes, string updatedBy)
    {
        BundleCount = bundleCount;
        UnitsPerBundle = unitsPerBundle;
        Notes = notes;
        SetUpdated(updatedBy);
    }

    public void Dispatch(string dispatchedBy)
    {
        IsDispatched = true;
        DispatchedAt = DateTime.UtcNow;
        DispatchedBy = dispatchedBy;
        SetUpdated(dispatchedBy);
    }
}
