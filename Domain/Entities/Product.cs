using ManolyWarehouse.Domain.Common;

namespace ManolyWarehouse.Domain.Entities;

public class Product : AuditableEntity
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = default!;

    private readonly List<ShelfInventory> _shelfInventories = new();
    public IReadOnlyCollection<ShelfInventory> ShelfInventories => _shelfInventories.AsReadOnly();

    private readonly List<AreaZInventory> _areaZInventories = new();
    public IReadOnlyCollection<AreaZInventory> AreaZInventories => _areaZInventories.AsReadOnly();

    private readonly List<PurchaseOrderItem> _purchaseOrderItems = new();
    public IReadOnlyCollection<PurchaseOrderItem> PurchaseOrderItems => _purchaseOrderItems.AsReadOnly();

    private Product() { }

    public static Product Create(string name, int categoryId, string createdBy)
    {
        var product = new Product
        {
            Name = name,
            CategoryId = categoryId
        };
        product.SetCreated(createdBy);
        return product;
    }

    public void Update(string name, int categoryId, string updatedBy)
    {
        Name = name;
        CategoryId = categoryId;
        SetUpdated(updatedBy);
    }
}
