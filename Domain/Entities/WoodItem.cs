namespace ManolyWarehouse.Domain.Entities;

/// <summary>
/// A wood item in the wood warehouse (مستودع الخشب) — kept separate from the shelves,
/// Area Z and the drawer stock. Counted in sheets / pieces.
/// </summary>
public class WoodItem
{
    public int Id { get; private set; }
    /// <summary>Item code from the company system, e.g. WPLY11110.</summary>
    public string? Code { get; private set; }
    /// <summary>Full name as written in the stock file, e.g. "ساندويش 17 ملم توين II فرمايكا". Null = built from the details.</summary>
    public string? Name { get; private set; }
    /// <summary>لوح (sheets), م³ (timber) or قطعة.</summary>
    public string Unit { get; private set; } = "لوح";
    /// <summary>Sheets in one bundle, e.g. "55" or "185-195".</summary>
    public string? PerBundle { get; private set; }
    public int SortOrder { get; private set; }
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
    public decimal Quantity { get; private set; }
    /// <summary>Warning: null = when it runs out (0), -1 = never, N = at N or fewer.</summary>
    public int? AlertAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private WoodItem() { }

    public static WoodItem Create(string type, decimal? thickness, decimal? length, decimal? width, string? color, string? notes, decimal quantity,
        string? code = null, string? name = null, string? unit = null, string? perBundle = null, int sortOrder = 0)
    {
        var w = new WoodItem { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, SortOrder = sortOrder };
        w.SetDetails(type, thickness, length, width, color, notes);
        w.SetIdentity(code, name, unit, perBundle);
        w.Quantity = Math.Max(0, quantity);
        return w;
    }

    public void SetIdentity(string? code, string? name, string? unit, string? perBundle)
    {
        static string? T(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, max)];
        Code = T(code, 30);
        Name = T(name, 200);
        Unit = T(unit, 10) ?? "لوح";
        PerBundle = T(perBundle, 20);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Bundles, when the sheets-per-bundle is a single number.</summary>
    public decimal? Bundles => Unit == "لوح" && decimal.TryParse(PerBundle, out var per) && per > 0 ? Math.Round(Quantity / per, 1) : null;

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

    public void SetQuantity(decimal quantity) { Quantity = Math.Max(0, quantity); UpdatedAt = DateTime.UtcNow; }
    public void SetAlertAt(int? at) => AlertAt = at;

    /// <summary>"MDF 18mm 244×122 أبيض"</summary>
    public string DisplayName
    {
        get
        {
            if (Name != null) return Name;
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
    public decimal Before { get; private set; }
    public decimal After { get; private set; }
    public string Reason { get; private set; } = default!;
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private WoodMovement() { }

    public static WoodMovement Create(int itemId, decimal before, decimal after, string reason, string? userId, string? userName) => new()
    {
        WoodItemId = itemId, Before = before, After = after, Reason = reason.Length > 200 ? reason[..200] : reason,
        UserId = userId, UserName = userName, CreatedAt = DateTime.UtcNow
    };
}
