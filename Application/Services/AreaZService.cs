using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Domain.Exceptions;
using ManolyWarehouse.Infrastructure.Persistence;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Application.Services;

public class AreaZService : IAreaZService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<AreaZService> _logger;

    public AreaZService(
        AppDbContext db,
        ICurrentUserService currentUser,
        IActivityLogService activityLog,
        ILogger<AreaZService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<PagedResult<AreaZItemViewModel>> ListActiveAsync(
        int page, int pageSize, int? warehouseId = null, CancellationToken ct = default)
    {
        var query = _db.AreaZInventory
            .AsNoTracking()
            .Where(az => !az.IsDispatched && (warehouseId == null || az.WarehouseId == warehouseId))
            .OrderByDescending(az => az.CreatedAt);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(az => new AreaZItemViewModel
            {
                Id = az.Id,
                ProductId = az.ProductId,
                ProductName = az.Product.Name,
                CategoryName = az.Product.Category.Name,
                BundleCount = az.BundleCount,
                UnitsPerBundle = az.UnitsPerBundle,
                Notes = az.Notes,
                WarehouseId = az.WarehouseId,
                WarehouseName = az.Warehouse.Name,
                EnteredAt = az.CreatedAt,
                EnteredBy = _db.Users
                    .Where(u => u.Id == az.CreatedBy)
                    .Select(u => u.FullName)
                    .FirstOrDefault() ?? az.CreatedBy
            })
            .ToListAsync(ct);

        return new PagedResult<AreaZItemViewModel>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AreaZItemViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.AreaZInventory
            .AsNoTracking()
            .Where(az => az.Id == id)
            .Select(az => new AreaZItemViewModel
            {
                Id = az.Id,
                ProductId = az.ProductId,
                ProductName = az.Product.Name,
                CategoryName = az.Product.Category.Name,
                BundleCount = az.BundleCount,
                UnitsPerBundle = az.UnitsPerBundle,
                Notes = az.Notes,
                WarehouseId = az.WarehouseId,
                WarehouseName = az.Warehouse.Name,
                EnteredAt = az.CreatedAt,
                EnteredBy = _db.Users
                    .Where(u => u.Id == az.CreatedBy)
                    .Select(u => u.FullName)
                    .FirstOrDefault() ?? az.CreatedBy
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> AddAsync(
        int productId, int bundleCount, int unitsPerBundle,
        string? notes, int warehouseId, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        ValidateQuantities(bundleCount, unitsPerBundle);
        var warehouseName = await _db.AreaZWarehouses.Where(w => w.Id == warehouseId).Select(w => w.Name).FirstOrDefaultAsync(ct)
            ?? throw new DomainException("المستودع غير موجود.");

        var productExists = await _db.Products.AnyAsync(p => p.Id == productId, ct);
        if (!productExists)
            throw new DomainException("المنتج غير موجود.");

        // AREAZ-01: One active row per product in each warehouse
        var existing = await _db.AreaZInventory
            .FirstOrDefaultAsync(az => az.ProductId == productId && az.WarehouseId == warehouseId && !az.IsDispatched, ct);

        var productName = await ProductNameAsync(productId, ct);

        if (existing != null)
        {
            // Increment existing row instead of creating a duplicate
            existing.Update(
                existing.BundleCount + bundleCount,
                unitsPerBundle,
                existing.Notes,
                userId);

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Area Z entry {Id} incremented (+{Bundles}x{Units}) by {User}",
                existing.Id, bundleCount, unitsPerBundle, userId);

            await _activityLog.RecordAsync(ActivityArea.AreaZ,
                $"زاد كمية «{productName}» في Area Z · {warehouseName} (+{bundleCount} ربطة)", ct);

            return existing.Id;
        }

        var entry = AreaZInventory.Create(productId, bundleCount, unitsPerBundle, userId, warehouseId, notes?.Trim());
        _db.AreaZInventory.Add(entry);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Area Z entry {Id} created for product {ProductId} by {User}",
            entry.Id, productId, userId);

        await _activityLog.RecordAsync(ActivityArea.AreaZ,
            $"أضاف «{productName}» إلى Area Z · {warehouseName} ({bundleCount} ربطة)", ct);

        return entry.Id;
    }

    public async Task UpdateAsync(
        int id, int bundleCount, int unitsPerBundle,
        string? notes, int? warehouseId = null, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        ValidateQuantities(bundleCount, unitsPerBundle);

        var entry = await _db.AreaZInventory.FirstOrDefaultAsync(az => az.Id == id, ct)
            ?? throw new DomainException("صف منطقة Z غير موجود.");

        if (entry.IsDispatched)
            throw new DomainException("لا يمكن تعديل صف تم شحنه.");

        var previousBundles = entry.BundleCount;
        var previousUnits = entry.UnitsPerBundle;
        entry.Update(bundleCount, unitsPerBundle, notes?.Trim(), userId);

        string? movedTo = null;
        if (warehouseId is int wid && wid != entry.WarehouseId)
        {
            movedTo = await _db.AreaZWarehouses.Where(w => w.Id == wid).Select(w => w.Name).FirstOrDefaultAsync(ct)
                ?? throw new DomainException("المستودع غير موجود.");
            if (await _db.AreaZInventory.AnyAsync(az => az.Id != id && az.ProductId == entry.ProductId
                                                         && az.WarehouseId == wid && !az.IsDispatched, ct))
                throw new DomainException($"هذا المنتج موجود أصلاً في {movedTo}. عدّل الكمية هناك بدل النقل.");
            entry.MoveToWarehouse(wid, userId);
        }
        await _db.SaveChangesAsync(ct);
        if (movedTo is not null)
            await _activityLog.RecordAsync(ActivityArea.AreaZ,
                $"نقل «{await ProductNameAsync(entry.ProductId, ct)}» إلى Area Z · {movedTo}", ct);

        _logger.LogInformation(
            "Area Z entry {Id} updated to {Bundles}x{Units} by {User}",
            id, bundleCount, unitsPerBundle, userId);

        await _activityLog.RecordAsync(ActivityArea.AreaZ,
            $"عدّل «{await ProductNameAsync(entry.ProductId, ct)}» في منطقة Z من {previousBundles} إلى {bundleCount} ربطة"
            + (previousUnits != unitsPerBundle ? $" (الوحدات/ربطة من {previousUnits} إلى {unitsPerBundle})" : ""), ct);
    }

    public async Task<IReadOnlyList<AreaZWarehouseViewModel>> ListWarehousesAsync(CancellationToken ct = default)
        => await _db.AreaZWarehouses.AsNoTracking()
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Id)
            .Select(w => new AreaZWarehouseViewModel
            {
                Id = w.Id,
                Name = w.Name,
                ActiveCount = _db.AreaZInventory.Count(az => az.WarehouseId == w.Id && !az.IsDispatched)
            })
            .ToListAsync(ct);

    public async Task<int> CreateWarehouseAsync(string name, CancellationToken ct = default)
    {
        RequireUserId();
        var clean = (name ?? "").Trim();
        if (clean.Length == 0) throw new DomainException("اكتب اسم المستودع.");
        if (await _db.AreaZWarehouses.AnyAsync(w => w.Name.ToLower() == clean.ToLower(), ct))
            throw new DomainException($"يوجد مستودع باسم «{clean}».");
        var next = (await _db.AreaZWarehouses.MaxAsync(w => (int?)w.SortOrder, ct) ?? 0) + 1;
        var w = AreaZWarehouse.Create(clean, next);
        _db.AreaZWarehouses.Add(w);
        await _db.SaveChangesAsync(ct);
        await _activityLog.RecordAsync(ActivityArea.AreaZ, $"أنشأ مستودعاً جديداً في Area Z: «{w.Name}»", ct);
        return w.Id;
    }

    public async Task RenameWarehouseAsync(int id, string name, CancellationToken ct = default)
    {
        RequireUserId();
        var w = await _db.AreaZWarehouses.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("المستودع غير موجود.");
        var clean = (name ?? "").Trim();
        if (clean.Length == 0) throw new DomainException("اكتب اسم المستودع.");
        if (await _db.AreaZWarehouses.AnyAsync(x => x.Id != id && x.Name.ToLower() == clean.ToLower(), ct))
            throw new DomainException($"يوجد مستودع باسم «{clean}».");
        var old = w.Name;
        w.Rename(clean);
        await _db.SaveChangesAsync(ct);
        await _activityLog.RecordAsync(ActivityArea.AreaZ, $"غيّر اسم المستودع «{old}» إلى «{w.Name}»", ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var entry = await _db.AreaZInventory.FirstOrDefaultAsync(az => az.Id == id, ct)
            ?? throw new DomainException("صف منطقة Z غير موجود.");

        var productName = await ProductNameAsync(entry.ProductId, ct);
        _db.AreaZInventory.Remove(entry);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Area Z entry {Id} deleted by {User}", id, userId);

        await _activityLog.RecordAsync(ActivityArea.AreaZ,
            $"حذف «{productName}» من منطقة Z", ct);
    }

    public async Task DispatchAsync(int id, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var entry = await _db.AreaZInventory.FirstOrDefaultAsync(az => az.Id == id, ct)
            ?? throw new DomainException("صف منطقة Z غير موجود.");

        if (entry.IsDispatched)
            throw new DomainException("هذا الصف تم شحنه بالفعل.");

        var productName = await ProductNameAsync(entry.ProductId, ct);
        entry.Dispatch(userId);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Area Z entry {Id} dispatched by {User}", id, userId);

        await _activityLog.RecordAsync(ActivityArea.AreaZ,
            $"شحن «{productName}» من منطقة Z", ct);
    }

    /// <summary>
    /// AREAZ-05: Transactional move from Area Z to a shelf.
    /// Capacity check + ShelfInventory create + Area Z dispatch — all atomic.
    /// </summary>
    public async Task MoveToShelfAsync(
        int areaZId, int shelfId, int position, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var entry = await _db.AreaZInventory.FirstOrDefaultAsync(az => az.Id == areaZId, ct)
            ?? throw new DomainException("صف منطقة Z غير موجود.");

        if (entry.IsDispatched)
            throw new DomainException("هذا الصف تم شحنه بالفعل.");

        var shelf = await _db.Shelves
            .Include(s => s.Inventories)
            .FirstOrDefaultAsync(s => s.Id == shelfId, ct)
            ?? throw new DomainException("الرف غير موجود.");

        // Position must fall within this shelf's capacity (racks hold more than shelves)
        if (position < 1 || position > shelf.MaxPositions)
            throw new DomainException("رقم الموضع خارج نطاق سعة الرف.");

        // EnableRetryOnFailure requires user-initiated transactions to run
        // inside the execution strategy's retriable unit.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                if (shelf.IsFull)
                    throw new ShelfFullException(shelf.Code);

                if (shelf.Inventories.Any(i => i.Position == position))
                    throw new PositionOccupiedException(shelf.Code, position);

                // Create the ShelfInventory row using Area Z's quantities
                var inventory = ShelfInventory.Create(
                    shelfId, entry.ProductId, position,
                    entry.BundleCount, entry.UnitsPerBundle, userId);

                _db.ShelfInventory.Add(inventory);

                // Mark Area Z entry as dispatched (using the dispatch method —
                // semantically "left Area Z", which is true)
                entry.Dispatch(userId);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "Area Z entry {Id} moved to shelf {Code} pos {Position} by {User}",
                    areaZId, shelf.Code, position, userId);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        await _activityLog.RecordAsync(ActivityArea.Shelves,
            $"نقل «{await ProductNameAsync(entry.ProductId, ct)}» من منطقة Z إلى الرف {shelf.Code} — الموضع {position}", ct);
    }

    private async Task<string> ProductNameAsync(int productId, CancellationToken ct) =>
        await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct) ?? "منتج";

    private static void ValidateQuantities(int bundleCount, int unitsPerBundle)
    {
        if (bundleCount < 1)
            throw new DomainException("عدد الربطات يجب أن يكون 1 أو أكثر.");
        if (unitsPerBundle < 1)
            throw new DomainException("عدد الوحدات في الربطة يجب أن يكون 1 أو أكثر.");
    }

    private string RequireUserId() =>
        _currentUser.UserId
            ?? throw new InvalidOperationException("No authenticated user in context.");
}
