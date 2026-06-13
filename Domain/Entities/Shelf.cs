namespace ManolyWarehouse.Domain.Entities;

public enum WarehouseSide { ABC, DEF }

public class Shelf
{
    // Default capacity for A/B/C shelves. D/E/F racks override via MaxPositions.
    public const int MaxSlots = 6;

    public int Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string Label { get; private set; } = default!;
    public int Number { get; private set; }
    public WarehouseSide Side { get; private set; }

    // Per-shelf position capacity. Defaults to MaxSlots; racks (D/E/F) hold more.
    public int MaxPositions { get; private set; } = MaxSlots;

    private readonly List<ShelfInventory> _inventories = new();
    public IReadOnlyCollection<ShelfInventory> Inventories => _inventories.AsReadOnly();

    private Shelf() { }

    public static Shelf Create(string label, int number, int maxPositions = MaxSlots)
    {
        if (maxPositions < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPositions), "سعة الرف يجب أن تكون 1 أو أكثر.");

        var side = label is "A" or "B" or "C" ? WarehouseSide.ABC : WarehouseSide.DEF;
        return new Shelf
        {
            Label = label,
            Number = number,
            Code = $"{label}{number}",
            Side = side,
            MaxPositions = maxPositions
        };
    }

    /// <summary>Updates this shelf's position capacity. Guards against zero/negative values.</summary>
    public void SetCapacity(int maxPositions)
    {
        if (maxPositions < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPositions), "سعة الرف يجب أن تكون 1 أو أكثر.");
        MaxPositions = maxPositions;
    }

    public int OccupiedSlots => _inventories.Count;
    public bool IsFull => _inventories.Count >= MaxPositions;
    public bool HasAvailableSlot => _inventories.Count < MaxPositions;
}
