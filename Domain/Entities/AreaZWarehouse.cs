namespace ManolyWarehouse.Domain.Entities;

/// <summary>
/// A warehouse inside the company, grouped under Area Z (e.g. "Smirdex Warehouse").
/// Every Area Z entry belongs to exactly one of these.
/// </summary>
public class AreaZWarehouse
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AreaZWarehouse() { }

    public static AreaZWarehouse Create(string name, int sortOrder) => new()
    {
        Name = Clean(name), SortOrder = sortOrder, CreatedAt = DateTime.UtcNow
    };

    public void Rename(string name) => Name = Clean(name);

    private static string Clean(string name)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) throw new ArgumentException("اسم المستودع مطلوب.");
        return n.Length > 100 ? n[..100] : n;
    }
}
