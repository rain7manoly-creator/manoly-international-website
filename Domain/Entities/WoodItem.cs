namespace ManolyWarehouse.Domain.Entities;

/// <summary>
/// A wood item in the wood warehouse (مستودع الخشب) — kept separate from the shelves,
/// Area Z and the drawer stock. Counted in sheets / pieces.
/// </summary>
public class WoodItem
{
    public int Id { get; private set; }
    /// <summary>Material, e.g. MDF, Plywood, Chipboard, Beech…</summary>
    public string Type { get; private set; } = default!;
    public decimal? ThicknessMm { get; private set; }
    /// <summary>Size in cm, e.g. 244 × 122.</summary>
    public decimal? LengthCm { get; private set; }
    public decimal? WidthCm { get; private set; }
    /// <summary>Colour, finish or brand, e.g. "أبيض", "Egger H1145".</summary>
    public string? Color { get; private set; }
    public string? Notes { get; private set; }
    /// <summary>Kept for later: the area of the wood warehouse it sits in.</summary>
    public string? Area { get; private set; }
    public int Quantity { get; private set; }
    /// <summary>Warning: null = when it runs out (0), -1 = never, N = at N or fewer.</summary>
    public int? AlertAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private WoodItem() { }

    public static WoodItem Create(string type, decimal? thickness, decimal? length, decimal? width, string? color, string? notes, int quantity)
    {
        var w = new WoodItem { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        w.SetDetails(type, thickness, length, width, color, notes);
        w.Quantity = Math.Max(0, quantity);
        return w;
    }

    public void SetDetails(string type, decimal? thickness, decimal? length, decimal? width, string? color, string? notes)
    {
        type = (type ?? "").Trim();
        if (type.Length == 0) throw new ArgumentException("نوع الخشب مطلوب.");
        Type = type.Length > 60 ? type[..60] : type;
        ThicknessMm = thickness is > 0 ? thickness : null;
        LengthCm = length is > 0 ? length : null;
        WidthCm = width is > 0 ? width : null;
        Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim()[..Math.Min(color.Trim().Length, 80)];
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 300)];
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetQuantity(int quantity) { Quantity = Math.Max(0, quantity); UpdatedAt = DateTime.UtcNow; }
    public void SetAlertAt(int? at) => AlertAt = at;

    /// <summary>"MDF 18mm 244×122 أبيض"</summary>
    public string DisplayName
    {
        get
        {
            static string N(decimal d) => d.ToString("0.##");
            var parts = new List<string> { Type };
            if (ThicknessMm is decimal t) parts.Add(N(t) + "mm");
            if (LengthCm is decimal l && WidthCm is decimal w) parts.Add(N(l) + "×" + N(w));
            else if (LengthCm is decimal l2) parts.Add(N(l2));
            if (Color != null) parts.Add(Color);
            return string.Join(" ", parts);
        }
    }

    public bool IsLow => Quantity <= (AlertAt ?? 0) && AlertAt != -1;
}

/// <summary>Every change to a wood item's quantity, so a wrong edit can be seen and undone.</summary>
public class WoodMovement
{
    public long Id { get; private set; }
    public int WoodItemId { get; private set; }
    public int Before { get; private set; }
    public int After { get; private set; }
    public string Reason { get; private set; } = default!;
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private WoodMovement() { }

    public static WoodMovement Create(int itemId, int before, int after, string reason, string? userId, string? userName) => new()
    {
        WoodItemId = itemId, Before = before, After = after, Reason = reason.Length > 200 ? reason[..200] : reason,
        UserId = userId, UserName = userName, CreatedAt = DateTime.UtcNow
    };
}
