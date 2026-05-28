using ManolyWarehouse.Domain.Common;

namespace ManolyWarehouse.Domain.Entities;

public class Category : AuditableEntity
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;

    private readonly List<Product> _products = new();
    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();

    private Category() { }

    public static Category Create(string name, string createdBy)
    {
        var category = new Category { Name = name };
        category.SetCreated(createdBy);
        return category;
    }

    public void Update(string name, string updatedBy)
    {
        Name = name;
        SetUpdated(updatedBy);
    }
}
