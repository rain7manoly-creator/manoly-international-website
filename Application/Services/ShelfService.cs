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
    private readonly ILogger<ShelfService> _logger;

    public ShelfService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ILogger<ShelfService> logger)
    {
        _db = db;
        _currentUser = currentUser;
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
                si.UnitsPerBundle
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
                UnitsPerBundle = inv?.UnitsPerBundle ?? 0
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
        var productExists = await _db.Products.AnyAsync(p => p.Id == request.ProductId, ct);
        if (!productExists)
            throw new DomainException("المنتج غير موجود.");

        var inventory = ShelfInventory.Create(
            shelf.Id, request.ProductId, request.Position,
            request.BundleCount, request.UnitsPerBundle, userId);

        _db.ShelfInventory.Add(inventory);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Inventory {Id} added to shelf {Code} pos {Position} by {User}",
            inventory.Id, shelf.Code, request.Position, userId);

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
            .FirstOrDefaultAsync(si => si.Id == inventoryId, ct)
            ?? throw new DomainException("صف المخزون غير موجود.");

        // SHELF-04 + SHELF-05: Adjust auto-creates the log; returns true if quantity hit zero
        var shouldDelete = inventory.Adjust(bundleCount, unitsPerBundle, userId, notes);

        if (shouldDelete)
        {
            _db.ShelfInventory.Remove(inventory);
            _logger.LogInformation(
                "Inventory {Id} on shelf {Code} auto-deleted (qty=0) by {User}",
                inventoryId, inventory.Shelf.Code, userId);
        }
        else
        {
            _logger.LogInformation(
                "Inventory {Id} on shelf {Code} adjusted to {Bundles}x{Units} by {User}",
                inventoryId, inventory.Shelf.Code, bundleCount, unitsPerBundle, userId);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteInventoryAsync(int inventoryId, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var inventory = await _db.ShelfInventory
            .Include(si => si.Shelf)
            .FirstOrDefaultAsync(si => si.Id == inventoryId, ct)
            ?? throw new DomainException("صف المخزون غير موجود.");

        var shelfCode = inventory.Shelf.Code;
        _db.ShelfInventory.Remove(inventory);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Inventory {Id} on shelf {Code} deleted by {User}",
            inventoryId, shelfCode, userId);
    }

    private string RequireUserId() =>
        _currentUser.UserId
            ?? throw new InvalidOperationException("No authenticated user in context.");
}
