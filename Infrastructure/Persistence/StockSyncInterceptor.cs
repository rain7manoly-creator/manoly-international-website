using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Infrastructure.Persistence;

/// <summary>
/// Keeps the company stock list in step with the shelves.
///
/// Every time shelf stock is saved — put on a shelf, quantity edited (including
/// restores from the history), removed, moved from Area Z or received from an order —
/// the change in single units (bundles × units per bundle) is applied to the stock
/// item the product is linked to, in the same database transaction, and recorded
/// as a StockMovement. Doing it here instead of in each service means no code path
/// that touches a shelf can forget to update the stock.
/// </summary>
public class StockSyncInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _http;

    public StockSyncInterceptor(IHttpContextAccessor http) => _http = http;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is AppDbContext db) await SyncAsync(db, ct);
        return result;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is AppDbContext db) SyncAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    private async Task SyncAsync(AppDbContext db, CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();

        // Products being deleted take their shelf rows with them; that is catalog cleanup, not stock leaving.
        var deletedProducts = db.ChangeTracker.Entries<Product>()
            .Where(e => e.State == EntityState.Deleted).Select(e => e.Entity.Id).ToHashSet();

        var changes = new List<(int ProductId, int Delta, int BundleDelta, string ShelfCode, string Kind)>();
        foreach (var e in db.ChangeTracker.Entries<ShelfInventory>())
        {
            var productId = e.Entity.ProductId;
            if (deletedProducts.Contains(productId)) continue;

            int Bundles(PropertyValues v) => v.GetValue<int>(nameof(ShelfInventory.BundleCount));
            int Units(PropertyValues v) => Bundles(v) * v.GetValue<int>(nameof(ShelfInventory.UnitsPerBundle));
            int delta, bundleDelta; string kind;
            switch (e.State)
            {
                case EntityState.Added:
                    delta = Units(e.CurrentValues); bundleDelta = Bundles(e.CurrentValues); kind = "add"; break;
                case EntityState.Deleted:
                    delta = -Units(e.OriginalValues); bundleDelta = -Bundles(e.OriginalValues); kind = "remove"; break;
                case EntityState.Modified:
                    delta = Units(e.CurrentValues) - Units(e.OriginalValues);
                    bundleDelta = Bundles(e.CurrentValues) - Bundles(e.OriginalValues); kind = "edit"; break;
                default: continue;
            }
            if (delta == 0 && bundleDelta == 0) continue;
            changes.Add((productId, delta, bundleDelta, await ShelfCodeAsync(db, e.Entity, ct), kind));
        }
        // Area Z is part of the warehouse too. Dispatching or moving to a shelf takes it out of
        // Area Z (−); the shelf side adds it back (+), so a move nets to zero.
        foreach (var e in db.ChangeTracker.Entries<AreaZInventory>())
        {
            var productId = e.Entity.ProductId;
            if (deletedProducts.Contains(productId)) continue;
            int B(PropertyValues v) => v.GetValue<bool>(nameof(AreaZInventory.IsDispatched)) ? 0 : v.GetValue<int>(nameof(AreaZInventory.BundleCount));
            int U(PropertyValues v) => B(v) * v.GetValue<int>(nameof(AreaZInventory.UnitsPerBundle));
            int delta, bundleDelta; string kind;
            switch (e.State)
            {
                case EntityState.Added: delta = U(e.CurrentValues); bundleDelta = B(e.CurrentValues); kind = "z-add"; break;
                case EntityState.Deleted: delta = -U(e.OriginalValues); bundleDelta = -B(e.OriginalValues); kind = "z-remove"; break;
                case EntityState.Modified:
                    delta = U(e.CurrentValues) - U(e.OriginalValues);
                    bundleDelta = B(e.CurrentValues) - B(e.OriginalValues); kind = "z-edit"; break;
                default: continue;
            }
            if (delta == 0 && bundleDelta == 0) continue;
            changes.Add((productId, delta, bundleDelta, "Area Z", kind));
        }

        if (changes.Count == 0) return;

        var productIds = changes.Select(c => c.ProductId).Distinct().ToList();
        var links = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.StockItemId != null)
            .Select(p => new { p.Id, p.Name, StockItemId = p.StockItemId!.Value })
            .ToListAsync(ct);
        if (links.Count == 0) return;

        var itemIds = links.Select(l => l.StockItemId).Distinct().ToList();
        var items = await db.StockItems.Where(s => itemIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        var user = _http.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user?.Identity?.Name;

        foreach (var c in changes)
        {
            var link = links.FirstOrDefault(l => l.Id == c.ProductId);
            if (link is null || !items.TryGetValue(link.StockItemId, out var item)) continue;

            // Drawers are counted in pieces (bundles × units); screws and Kent in cartons (bundles)
            var amount = StockItem.CountsInCartons(item.Family) ? c.BundleDelta : c.Delta;
            if (amount == 0) continue;
            var before = item.Total;
            item.ApplyShelfDelta(amount, userId);
            var verb = c.Kind switch
            {
                "add" => "وُضع على الرف",
                "remove" => "حُذف من الرف",
                "z-add" => "أُضيف إلى",
                "z-remove" => "حُذف من",
                "z-edit" => amount < 0 ? "خرج من" : "زيد في",
                _ => amount < 0 ? "أُخذ من الرف" : "زيد على الرف"
            };
            db.StockMovements.Add(StockMovement.Create(item, amount, before, item.Total,
                $"{verb} {c.ShelfCode} — «{link.Name}»", userId, userName));
        }

        db.ChangeTracker.DetectChanges();
    }

    private static async Task<string> ShelfCodeAsync(AppDbContext db, ShelfInventory si, CancellationToken ct)
    {
        if (si.Shelf is not null) return si.Shelf.Code;
        var shelf = db.Shelves.Local.FirstOrDefault(s => s.Id == si.ShelfId);
        if (shelf is not null) return shelf.Code;
        return await db.Shelves.AsNoTracking().Where(s => s.Id == si.ShelfId).Select(s => s.Code).FirstOrDefaultAsync(ct) ?? "?";
    }
}
