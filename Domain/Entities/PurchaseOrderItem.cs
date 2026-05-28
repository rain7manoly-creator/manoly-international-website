using ManolyWarehouse.Domain.Common;
using ManolyWarehouse.Domain.Exceptions;

namespace ManolyWarehouse.Domain.Entities;

public class PurchaseOrderItem : AuditableEntity
{
    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public PurchaseOrder Order { get; private set; } = default!;
    public int ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public int BundleCount { get; private set; }
    public int UnitsPerBundle { get; private set; }

    public int? ReceivedShelfId { get; private set; }
    public Shelf? ReceivedShelf { get; private set; }
    public int? Position { get; private set; }
    public bool GoesToAreaZ { get; private set; }
    public DateTime? ShelvedAt { get; private set; }
    public string? ShelvedBy { get; private set; }

    public int TotalQuantity => BundleCount * UnitsPerBundle;
    public bool IsShelved => ReceivedShelfId.HasValue || (GoesToAreaZ && ShelvedAt.HasValue);
    public bool IsPendingShelving => !IsShelved;

    private PurchaseOrderItem() { }

    public static PurchaseOrderItem Create(
        int orderId, int productId,
        int bundleCount, int unitsPerBundle,
        string createdBy)
    {
        var item = new PurchaseOrderItem
        {
            OrderId = orderId,
            ProductId = productId,
            BundleCount = bundleCount,
            UnitsPerBundle = unitsPerBundle,
            GoesToAreaZ = false
        };
        item.SetCreated(createdBy);
        return item;
    }

    public void UpdateQuantity(int bundleCount, int unitsPerBundle, string updatedBy)
    {
        BundleCount = bundleCount;
        UnitsPerBundle = unitsPerBundle;
        SetUpdated(updatedBy);
    }

    public void AssignToShelf(int shelfId, int position, string shelvedBy)
    {
        if (GoesToAreaZ)
            throw new DomainException("هذا الصنف محدد لمنطقة Z.");

        ReceivedShelfId = shelfId;
        Position = position;
        ShelvedAt = DateTime.UtcNow;
        ShelvedBy = shelvedBy;
        SetUpdated(shelvedBy);
    }

    public void AssignToAreaZ(string shelvedBy)
    {
        if (ReceivedShelfId.HasValue)
            throw new DomainException("هذا الصنف محدد لرف.");

        GoesToAreaZ = true;
        ShelvedAt = DateTime.UtcNow;
        ShelvedBy = shelvedBy;
        SetUpdated(shelvedBy);
    }
}
