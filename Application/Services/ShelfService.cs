using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Domain.Exceptions;
using ManolyWarehouse.Infrastructure.Persistence;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Application.Services;

public class ShelfService : IShelfService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<ShelfService> _logger;

    public ShelfService(
        AppDbContext db,
        ICurrentUserService currentUser,
        IActivityLogService activityLog,
        ILogger<ShelfService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<ShelfDetailViewModel?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var shelf = await _db.Shelves
            .AsNoTracking()
            .Where(s => s.Code == code)
            .Select(s => new
            {
                s.Id,
                s.Code,
                s.Label,
                s.Number,
                s.MaxPositions
            })
            .FirstOrDefaultAsync(ct);

        if (shelf is null)
        {
            _logger.LogWarning("Shelf {Code} not found", code);
            return null;
        }

        var inventories = await _db.ShelfInventory
            .AsNoTracking()
            .Where(si => si.ShelfId == shelf.Id)
            .Select(si => new
            {
                si.Id,
                si.Position,
                si.ProductId,
                ProductName = si.Product.Name,
                CategoryName = si.Product.Category.Name,
                si.BundleCount,
                si.UnitsPerBundle,
                si.Product.StockItemId,
                StockName = si.Product.StockItem == null ? null
                    : si.Product.StockItem.Name + (si.Product.StockItem.Color == "white" ? " — أبيض" : si.Product.StockItem.Color == "grey" ? " — سكني" : "")
            })
            .ToListAsync(ct);

        // Build all positions for this shelf — empty slots are filled in for visual rendering
        var slots = Enumerable.Range(1, shelf.MaxPositions).Select(pos =>
        {
            var inv = inventories.FirstOrDefault(i => i.Position == pos);
            return new ShelfSlotViewModel
            {
                Position = pos,
                InventoryId = inv?.Id,
                ProductId = inv?.ProductId,
                ProductName = inv?.ProductName,
                CategoryName = inv?.CategoryName,
                BundleCount = inv?.BundleCount ?? 0,
                UnitsPerBundle = inv?.UnitsPerBundle ?? 0,
                StockItemId = inv?.StockItemId,
                StockName = inv?.StockName
            };
        }).ToList();

        return new ShelfDetailViewModel
        {
            ShelfId = shelf.Id,
            Code = shelf.Code,
            Label = shelf.Label,
            Number = shelf.Number,
            Slots = slots,
            OccupiedCount = inventories.Count,
            MaxPositions = shelf.MaxPositions
        };
    }

    public async Task<int> AddInventoryAsync(AddShelfInventoryRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var shelf = await _db.Shelves
            .Include(s => s.Inventories)
            .FirstOrDefaultAsync(s => s.Code == request.ShelfCode, ct)
            ?? throw new DomainException($"الرف {request.ShelfCode} غير موجود.");

        // Position must be within this shelf's capacity (racks hold more than shelves)
        if (request.Position < 1 || request.Position > shelf.MaxPositions)
            throw new DomainException("رقم الموضع خارج نطاق سعة الرف.");

        // SHELF-01: Capacity check (also enforced by the DB unique index on Position)
        if (shelf.IsFull)
        {
            _logger.LogWarning("Attempt to add to full shelf {Code}", shelf.Code);
            throw new ShelfFullException(shelf.Code);
        }

        // SHELF-02: Position uniqueness check
        if (shelf.Inventories.Any(i => i.Position == request.Position))
        {
            _logger.LogWarning(
                "Position {Position} on shelf {Code} is occupied", request.Position, shelf.Code);
            throw new PositionOccupiedException(shelf.Code, request.Position);
        }

        // Confirm product exists
        var productName = await _db.Products
            .Where(p => p.Id == request.ProductId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);
        if (productName is null)
            throw new DomainException("المنتج غير موجود.");

        var inventory = ShelfInventory.Create(
            shelf.Id, request.ProductId, request.Position,
            request.BundleCount, request.UnitsPerBundle, userId);

        _db.ShelfInventory.Add(inventory);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Inventory {Id} added to shelf {Code} pos {Position} by {User}",
            inventory.Id, shelf.Code, request.Position, userId);

        await _activityLog.RecordAsync(ActivityArea.Shelves,
            $"أضاف «{productName}» إلى الرف {shelf.Code} — الموضع {request.Position} ({request.BundleCount} ربطة)", ct);

        return inventory.Id;
    }

    public async Task AdjustInventoryAsync(
        int inventoryId, int bundleCount, int unitsPerBundle,
        string? notes, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        if (bundleCount < 0)
            throw new DomainException("عدد الربطات لا يمكن أن يكون سالباً.");
        if (unitsPerBundle < 1)
            throw new DomainException("عدد الوحدات في الربطة يجب أن يكون 1 أو أكثر.");

        var inventory = await _db.ShelfInventory
            .Include(si => si.Shelf)
            .Include(si => si.Product)
            .FirstOrDefaultAsync(si => si.Id == inventoryId, ct)
            ?? throw new DomainException("صف المخزون غير موجود.");

        var previousBundles = inventory.BundleCount;
        var previousUnits = inventory.UnitsPerBundle;
        if (previousBundles == bundleCount && previousUnits == unitsPerBundle)
            return; // nothing changed — don't clutter the history

        // SHELF-04: Adjust auto-creates the log. BundleCount=0 means out-of-stock — keep the
        // row so the out-of-stock page can surface it; do NOT delete it here.
        inventory.Adjust(bundleCount, unitsPerBundle, userId, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());

        _logger.LogInformation(
            "Inventory {Id} on shelf {Code} adjusted to {Bundles}x{Units} by {User}",
            inventoryId, inventory.Shelf.Code, bundleCount, unitsPerBundle, userId);

        await _db.SaveChangesAsync(ct);

        var action = bundleCount == 0
            ? $"حدّد «{inventory.Product.Name}» كنافد من المخزون في الرف {inventory.Shelf.Code}"
            : $"عدّل كمية «{inventory.Product.Name}» في الرف {inventory.Shelf.Code} من {previousBundles} إلى {bundleCount} ربطة"
              + (previousUnits != unitsPerBundle ? $" (الوحدات/ربطة من {previousUnits} إلى {unitsPerBundle})" : "");
        await _activityLog.RecordAsync(ActivityArea.Shelves, action, ct);
    }

    public async Task DeleteInventoryAsync(int inventoryId, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var inventory = await _db.ShelfInventory
            .Include(si => si.Shelf)
            .Include(si => si.Product)
            .FirstOrDefaultAsync(si => si.Id == inventoryId, ct)
            ?? throw new DomainException("صف المخزون غير موجود.");

        var shelfCode = inventory.Shelf.Code;
        var productName = inventory.Product.Name;
        _db.ShelfInventory.Remove(inventory);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Inventory {Id} on shelf {Code} deleted by {User}",
            inventoryId, shelfCode, userId);

        await _activityLog.RecordAsync(ActivityArea.Shelves,
            $"حذف «{productName}» من الرف {shelfCode}", ct);
    }

    public async Task<InventoryHistoryViewModel?> GetInventoryHistoryAsync(
        int inventoryId, CancellationToken ct = default)
    {
        var inv = await _db.ShelfInventory
            .AsNoTracking()
            .Where(si => si.Id == inventoryId)
            .Select(si => new
            {
                si.Id,
                ShelfCode = si.Shelf.Code,
                si.Position,
                ProductName = si.Product.Name,
                si.BundleCount,
                si.UnitsPerBundle,
                si.CreatedAt,
                si.CreatedBy
            })
            .FirstOrDefaultAsync(ct);
        if (inv is null) return null;

        var logs = await _db.InventoryAdjustmentLogs
            .AsNoTracking()
            .Where(l => l.ShelfInventoryId == inventoryId)
            .OrderByDescending(l => l.AdjustedAt)
            .ThenByDescending(l => l.Id)
            .ToListAsync(ct);

        // Stored values are user ids — resolve them to display names in one query.
        var userIds = logs.Select(l => l.AdjustedBy).Append(inv.CreatedBy).Distinct().ToList();
        var names = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string NameOf(string id) => names.TryGetValue(id, out var n) ? n : "مستخدم محذوف";

        // The oldest change's "previous" value is what was originally put on the shelf.
        var first = logs.LastOrDefault();

        return new InventoryHistoryViewModel
        {
            InventoryId = inv.Id,
            ShelfCode = inv.ShelfCode,
            Position = inv.Position,
            ProductName = inv.ProductName,
            BundleCount = inv.BundleCount,
            UnitsPerBundle = inv.UnitsPerBundle,
            OriginalBundleCount = first?.PreviousBundleCount ?? inv.BundleCount,
            OriginalUnitsPerBundle = first?.PreviousUnitsPerBundle ?? inv.UnitsPerBundle,
            AddedByName = NameOf(inv.CreatedBy),
            AddedAt = inv.CreatedAt,
            Changes = logs.Select(l => new InventoryHistoryEntry
            {
                PreviousBundleCount = l.PreviousBundleCount,
                NewBundleCount = l.NewBundleCount,
                PreviousUnitsPerBundle = l.PreviousUnitsPerBundle,
                NewUnitsPerBundle = l.NewUnitsPerBundle,
                AdjustedByName = NameOf(l.AdjustedBy),
                AdjustedAt = l.AdjustedAt,
                Notes = l.Notes
            }).ToList()
        };
    }

    private string RequireUserId() =>
        _currentUser.UserId
            ?? throw new InvalidOperationException("No authenticated user in context.");
}
