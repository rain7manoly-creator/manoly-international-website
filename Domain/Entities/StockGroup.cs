namespace ManolyWarehouse.Domain.Entities;

/// <summary>
/// A stock group the team created in the app (e.g. "مفصلات SH"), next to the built-in
/// Metro / SH / screws / Kent / Smirdex groups. Its lines use the family key "g{Id}".
/// </summary>
public class StockGroup
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    /// <summary>true = counted in cartons (one bundle on a shelf = one carton); false = single pieces.</summary>
    public bool InCartons { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private StockGroup() { }

    public static StockGroup Create(string name, bool inCartons, int sortOrder) => new()
    {
        Name = Clean(name), InCartons = inCartons, SortOrder = sortOrder, CreatedAt = DateTime.UtcNow
    };

    public void Update(string name, bool inCartons) { Name = Clean(name); InCartons = inCartons; }

    public string Key => KeyFor(Id);
    public static string KeyFor(int id) => "g" + id;

    private static string Clean(string name)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) throw new ArgumentException("اسم المخزون مطلوب.");
        return n.Length > 100 ? n[..100] : n;
    }
}

/// <summary>
/// In-memory copy of the created groups (name + unit), so family names and units
/// can be looked up anywhere without a query. Reloaded at startup and after every change.
/// </summary>
public static class StockGroupRegistry
{
    private static volatile IReadOnlyDictionary<string, (string Name, bool InCartons)> _groups
        = new Dictionary<string, (string, bool)>();

    public static IReadOnlyDictionary<string, (string Name, bool InCartons)> Groups => _groups;

    public static void Load(IEnumerable<StockGroup> groups) =>
        _groups = groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Id)
            .ToDictionary(g => g.Key, g => (g.Name, g.InCartons));

    public static bool IsCustom(string family) => family.StartsWith('g') && _groups.ContainsKey(family);
}
