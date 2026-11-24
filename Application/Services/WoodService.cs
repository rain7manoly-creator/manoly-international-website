using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Domain.Exceptions;
using ManolyWarehouse.Infrastructure.Persistence;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Application.Services;

public record WoodRow(int Id, string Type, decimal? Thickness, decimal? Length, decimal? Width, string? Color, string? Notes,
    decimal Quantity, int? AlertAt, DateTime UpdatedAt, string Name, string? Code, string Unit, string? PerBundle, decimal? Bundles, bool HasName)
{
    public bool IsLow => AlertAt != -1 && Quantity <= (AlertAt ?? 0);
    public string Qty => Quantity.ToString("#,0.###");
}

public record WoodDetail(WoodRow Item, IReadOnlyList<WoodMovement> Movements);

public interface IWoodService
{
    Task<IReadOnlyList<WoodRow>> ListAsync(CancellationToken ct = default);
    Task<WoodDetail?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> TypesAsync(CancellationToken ct = default);
    Task<int> CreateAsync(string type, decimal? thickness, decimal? length, decimal? width, string? color, string? notes, decimal quantity,
        string? code = null, string? name = null, string? unit = null, string? perBundle = null, CancellationToken ct = default);
    Task UpdateDetailsAsync(int id, string type, decimal? thickness, decimal? length, decimal? width, string? color, string? notes,
        string? code = null, string? name = null, string? unit = null, string? perBundle = null, CancellationToken ct = default);
    Task SetQuantityAsync(int id, decimal quantity, string? note, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task SetAlertAsync(int id, int? at, CancellationToken ct = default);
    Task<IReadOnlyList<WoodRow>> LowAsync(CancellationToken ct = default);
}

public class WoodService : IWoodService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IActivityLogService _log;

    public WoodService(AppDbContext db, ICurrentUserService user, IActivityLogService log) { _db = db; _user = user; _log = log; }

    private static WoodRow Row(WoodItem w) => new(w.Id, w.Type, w.ThicknessMm, w.LengthCm, w.WidthCm, w.Color, w.Notes,
        w.Quantity, w.AlertAt, w.UpdatedAt, w.DisplayName, w.Code, w.Unit, w.PerBundle, w.Bundles, w.Name != null);

    /// <summary>In the stock file's order (groups as in the file); items added in the app follow, by type and size.</summary>
    public async Task<IReadOnlyList<WoodRow>> ListAsync(CancellationToken ct = default)
    {
        var items = await _db.WoodItems.AsNoTracking().ToListAsync(ct);
        var typeOrder = items.GroupBy(w => w.Type).ToDictionary(g => g.Key, g => g.Min(w => w.SortOrder == 0 ? int.MaxValue : w.SortOrder));
        return items.OrderBy(w => typeOrder[w.Type]).ThenBy(w => w.Type)
            .ThenBy(w => w.SortOrder == 0 ? int.MaxValue : w.SortOrder)
            .ThenBy(w => w.ThicknessMm).ThenBy(w => w.LengthCm).ThenBy(w => w.WidthCm).ThenBy(w => w.Color)
            .Select(Row).ToList();
    }

    public async Task<WoodDetail?> GetAsync(int id, CancellationToken ct = default)
    {
        var w = await _db.WoodItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w == null) return null;
        var moves = await _db.WoodMovements.AsNoTracking().Where(m => m.WoodItemId == id)
            .OrderByDescending(m => m.CreatedAt).Take(100).ToListAsync(ct);
        return new WoodDetail(Row(w), moves);
    }

    public async Task<IReadOnlyList<string>> TypesAsync(CancellationToken ct = default) =>
        await _db.WoodItems.AsNoTracking().Select(w => w.Type).Distinct().OrderBy(t => t).ToListAsync(ct);

    public async Task<int> CreateAsync(string type, decimal? thickness, decimal? length, decimal? width, string? color, string? notes, decimal quantity,
        string? code = null, string? name = null, string? unit = null, string? perBundle = null, CancellationToken ct = default)
    {
        WoodItem w;
        try { w = WoodItem.Create(type, thickness, length, width, color, notes, quantity, code, name, unit, perBundle); }
        catch (ArgumentException e) { throw new DomainException(e.Message); }
        if (await DuplicateAsync(w, null, ct)) throw new DomainException($"«{w.DisplayName}» موجود من قبل. عدّل كميته بدل إضافته مرة ثانية.");
        _db.WoodItems.Add(w);
        await _db.SaveChangesAsync(ct);
        _db.WoodMovements.Add(WoodMovement.Create(w.Id, 0, w.Quantity, "إضافة صنف خشب جديد", _user.UserId, _user.UserName));
        await _db.SaveChangesAsync(ct);
        await _log.RecordAsync(ActivityArea.Wood, $"أضاف «{w.DisplayName}» — {w.Quantity:0.###} {w.Unit}", ct);
        return w.Id;
    }

    public async Task UpdateDetailsAsync(int id, string type, decimal? thickness, decimal? length, decimal? width, string? color, string? notes,
        string? code = null, string? name = null, string? unit = null, string? perBundle = null, CancellationToken ct = default)
    {
        var w = await Find(id, ct);
        var old = w.DisplayName;
        try { w.SetDetails(type, thickness, length, width, color, notes); w.SetIdentity(code, name, unit, perBundle); }
        catch (ArgumentException e) { throw new DomainException(e.Message); }
        if (await DuplicateAsync(w, id, ct)) throw new DomainException($"«{w.DisplayName}» موجود من قبل.");
        await _db.SaveChangesAsync(ct);
        if (old != w.DisplayName) await _log.RecordAsync(ActivityArea.Wood, $"عدّل «{old}» ← «{w.DisplayName}»", ct);
    }

    public async Task SetQuantityAsync(int id, decimal quantity, string? note, CancellationToken ct = default)
    {
        if (quantity < 0) throw new DomainException("الكمية لا يمكن أن تكون أقل من 0.");
        var w = await Find(id, ct);
        var before = w.Quantity;
        if (before == quantity) return;
        w.SetQuantity(quantity);
        var reason = string.IsNullOrWhiteSpace(note) ? (quantity > before ? "إضافة" : "سحب") : note.Trim();
        _db.WoodMovements.Add(WoodMovement.Create(w.Id, before, quantity, reason, _user.UserId, _user.UserName));
        await _db.SaveChangesAsync(ct);
        await _log.RecordAsync(ActivityArea.Wood, $"«{w.DisplayName}»: {before:0.###} ← {quantity:0.###} {w.Unit}", ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var w = await Find(id, ct);
        _db.WoodItems.Remove(w);
        await _db.SaveChangesAsync(ct);
        await _log.RecordAsync(ActivityArea.Wood, $"حذف «{w.DisplayName}» ({w.Quantity:0.###} {w.Unit})", ct);
    }

    public async Task SetAlertAsync(int id, int? at, CancellationToken ct = default)
    {
        var w = await Find(id, ct);
        w.SetAlertAt(at);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WoodRow>> LowAsync(CancellationToken ct = default) =>
        (await _db.WoodItems.AsNoTracking().Where(w => w.AlertAt != -1 && w.Quantity <= (w.AlertAt ?? 0))
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Type).ToListAsync(ct)).Select(Row).ToList();

    private async Task<WoodItem> Find(int id, CancellationToken ct) =>
        await _db.WoodItems.FirstOrDefaultAsync(w => w.Id == id, ct) ?? throw new DomainException("صنف الخشب غير موجود.");

    /// <summary>Same code, or (with no codes) the same name / same details.</summary>
    private Task<bool> DuplicateAsync(WoodItem w, int? exceptId, CancellationToken ct) =>
        _db.WoodItems.AnyAsync(x => x.Id != (exceptId ?? 0) && (
            (w.Code != null && x.Code == w.Code) ||
            (w.Name != null && x.Name == w.Name) ||
            (w.Code == null && w.Name == null && x.Code == null && x.Name == null && x.Type == w.Type && x.ThicknessMm == w.ThicknessMm
                && x.LengthCm == w.LengthCm && x.WidthCm == w.WidthCm && x.Color == w.Color)), ct);
}
