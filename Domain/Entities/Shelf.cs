namespace ManolyWarehouse.Domain.Entities;

public enum WarehouseSide { ABC, DEF }

public class Shelf
{
    public const int MaxSlots = 6;

    public int Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string Label { get; private set; } = default!;
    public int Number { get; private set; }
    public WarehouseSide Side { get; private set; }

    private readonly List<ShelfInventory> _inventories = new();
    public IReadOnlyCollection<ShelfInventory> Inventories => _inventories.AsReadOnly();

    private Shelf() { }

    public static Shelf Create(string label, int number)
    {
        var side = label is "A" or "B" or "C" ? WarehouseSide.ABC : WarehouseSide.DEF;
        return new Shelf
        {
            Label = label,
            Number = number,
            Code = $"{label}{number}",
            Side = side
        };
    }

    public int OccupiedSlots => _inventories.Count;
    public bool IsFull => _inventories.Count >= MaxSlots;
    public bool HasAvailableSlot => _inventories.Count < MaxSlots;
}
