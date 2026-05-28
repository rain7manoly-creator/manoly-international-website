using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Domain.Exceptions;
using ManolyWarehouse.Infrastructure.Persistence;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Application.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<PurchaseOrderService> _logger;

    public PurchaseOrderService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ILogger<PurchaseOrderService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PagedResult<OrderListItemViewModel>> ListAsync(
        string? statusFilter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.PurchaseOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(statusFilter)
            && Enum.TryParse<OrderStatus>(statusFilter, true, out var status))
        {
            query = query.Where(po => po.Status == status);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(po => po.OrderedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(po => new OrderListItemViewModel
            {
                Id = po.Id,
                Supplier = po.Supplier,
                Status = po.Status,
                OrderedAt = po.OrderedAt,
                ExpectedArrivalDate = po.ExpectedArrivalDate,
                ItemCount = po.Items.Count,
                PendingShelvingCount = po.Status == OrderStatus.Received
                    ? po.Items.Count(i => i.ReceivedShelfId == null && !i.GoesToAreaZ)
                    : 0
            })
            .ToListAsync(ct);

        return new PagedResult<OrderListItemViewModel>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<OrderDetailViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await _db.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == id)
            .Select(po => new
            {
                po.Id,
                po.Supplier,
                po.Status,
                po.OrderedAt,
                po.ExpectedArrivalDate,
                po.Notes,
                po.CreatedBy,
                po.LastUpdatedAt,
                po.LastUpdatedBy,
                Items = po.Items.Select(item => new OrderItemViewModel
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    BundleCount = item.BundleCount,
                    UnitsPerBundle = item.UnitsPerBundle,
                    ReceivedShelfCode = item.ReceivedShelf != null ? item.ReceivedShelf.Code : null,
                    Position = item.Position,
                    GoesToAreaZ = item.GoesToAreaZ,
                    IsShelved = item.ReceivedShelfId != null
                                || (item.GoesToAreaZ && item.ShelvedAt != null)
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (order is null) return null;

        // Resolve the creator's display name from the user store
        var createdByName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == order.CreatedBy)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct)
            ?? order.CreatedBy;

        var isTerminal = order.Status is OrderStatus.Received or OrderStatus.Cancelled;
        var canAdvance = !isTerminal
            && (order.Status != OrderStatus.Ordered || order.Items.Count > 0);

        OrderStatus? nextStatus = order.Status switch
        {
            OrderStatus.Ordered      => OrderStatus.Shipped,
            OrderStatus.Shipped      => OrderStatus.ArrivingSoon,
            OrderStatus.ArrivingSoon => OrderStatus.Received,
            _ => null
        };

        return new OrderDetailViewModel
        {
            Id = order.Id,
            Supplier = order.Supplier,
            Status = order.Status,
            NextStatus = nextStatus,
            CanAdvance = canAdvance,
            IsTerminal = isTerminal,
            OrderedAt = order.OrderedAt,
            ExpectedArrivalDate = order.ExpectedArrivalDate,
            Notes = order.Notes,
            CreatedBy = createdByName,
            LastUpdatedAt = order.LastUpdatedAt,
            LastUpdatedBy = order.LastUpdatedBy,
            Items = order.Items
        };
    }

    public async Task<int> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        if (string.IsNullOrWhiteSpace(request.Supplier))
            throw new DomainException("اسم المورد مطلوب.");

        var order = PurchaseOrder.Create(
            request.Supplier.Trim(),
            request.ExpectedArrivalDate,
            request.Notes?.Trim(),
            userId);

        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Order {Id} created for supplier {Supplier} by {User}",
            order.Id, request.Supplier, userId);

        return order.Id;
    }

    public async Task AddItemAsync(
        int orderId, int productId, int bundleCount, int unitsPerBundle,
        CancellationToken ct = default)
    {
        var userId = RequireUserId();
        ValidateQuantities(bundleCount, unitsPerBundle);

        var order = await _db.PurchaseOrders.FirstOrDefaultAsync(po => po.Id == orderId, ct)
            ?? throw new DomainException("الطلبية غير موجودة.");

        if (order.IsTerminal) throw new TerminalStateException();

        var productExists = await _db.Products.AnyAsync(p => p.Id == productId, ct);
        if (!productExists) throw new DomainException("المنتج غير موجود.");

        var item = PurchaseOrderItem.Create(orderId, productId, bundleCount, unitsPerBundle, userId);
        order.AddItem(item);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Order {OrderId}: item added (product {ProductId}, {Bundles}x{Units}) by {User}",
            orderId, productId, bundleCount, unitsPerBundle, userId);
    }

    public async Task UpdateItemQuantityAsync(
        int itemId, int bundleCount, int unitsPerBundle, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        ValidateQuantities(bundleCount, unitsPerBundle);

        var item = await _db.PurchaseOrderItems
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new DomainException("صنف الطلبية غير موجود.");

        // ITEM-01: Cannot edit items once order is Received or Cancelled
        if (item.Order.IsTerminal) throw new TerminalStateException();

        item.UpdateQuantity(bundleCount, unitsPerBundle, userId);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Order item {Id} quantity updated to {Bundles}x{Units} by {User}",
            itemId, bundleCount, unitsPerBundle, userId);
    }

    public async Task DeleteItemAsync(int itemId, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var item = await _db.PurchaseOrderItems
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new DomainException("صنف الطلبية غير موجود.");

        if (item.Order.IsTerminal) throw new TerminalStateException();

        _db.PurchaseOrderItems.Remove(item);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Order item {Id} deleted by {User}", itemId, userId);
    }

    public async Task AdvanceStatusAsync(int orderId, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var order = await _db.PurchaseOrders
            .Include(po => po.Items)
            .FirstOrDefaultAsync(po => po.Id == orderId, ct)
            ?? throw new DomainException("الطلبية غير موجودة.");

        var previousStatus = order.Status;
        order.AdvanceStatus(userId); // throws via state machine if invalid
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Order {Id} advanced {From} -> {To} by {User}",
            orderId, previousStatus, order.Status, userId);
    }

    public async Task CancelAsync(int orderId, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var order = await _db.PurchaseOrders.FirstOrDefaultAsync(po => po.Id == orderId, ct)
            ?? throw new DomainException("الطلبية غير موجودة.");

        order.Cancel(userId);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Order {Id} cancelled by {User}", orderId, userId);
    }

    public async Task DeleteAsync(int orderId, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var order = await _db.PurchaseOrders.FirstOrDefaultAsync(po => po.Id == orderId, ct)
            ?? throw new DomainException("الطلبية غير موجودة.");

        // Items cascade by FK config; if any ShelfInventory references items via OrderItemId,
        // those refs are set to null per DeleteBehavior.SetNull
        _db.PurchaseOrders.Remove(order);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Order {Id} deleted by {User}", orderId, userId);
    }

    /// <summary>
    /// ITEM-03: Transactional receive — assigns item to a shelf AND creates the ShelfInventory row
    /// atomically. Capacity is checked under the same transaction to prevent races.
    /// </summary>
    public async Task ReceiveItemToShelfAsync(
        int itemId, int shelfId, int position, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        if (position is < 1 or > Shelf.MaxSlots)
            throw new DomainException($"الموضع يجب أن يكون بين 1 و {Shelf.MaxSlots}.");

        var item = await _db.PurchaseOrderItems
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new DomainException("صنف الطلبية غير موجود.");

        if (item.IsShelved)
            throw new DomainException("هذا الصنف تم استلامه بالفعل.");

        var shelf = await _db.Shelves
            .Include(s => s.Inventories)
            .FirstOrDefaultAsync(s => s.Id == shelfId, ct)
            ?? throw new DomainException("الرف غير موجود.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // SHELF-01: capacity check inside transaction
            if (shelf.IsFull)
                throw new ShelfFullException(shelf.Code);

            // SHELF-02: position uniqueness
            if (shelf.Inventories.Any(i => i.Position == position))
                throw new PositionOccupiedException(shelf.Code, position);

            // Update the item to mark it as assigned
            item.AssignToShelf(shelfId, position, userId);

            // ITEM-03: auto-create the ShelfInventory row with the item's quantities
            var inventory = ShelfInventory.Create(
                shelfId, item.ProductId, position,
                item.BundleCount, item.UnitsPerBundle,
                userId, orderItemId: item.Id);

            _db.ShelfInventory.Add(inventory);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Item {ItemId} (order {OrderId}) shelved at {Code} pos {Position} → inv {InvId} by {User}",
                itemId, item.OrderId, shelf.Code, position, inventory.Id, userId);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// ITEM-04: Receive item directly into Area Z instead of a shelf.
    /// Creates an AreaZInventory row or updates the existing active one for this product.
    /// </summary>
    public async Task ReceiveItemToAreaZAsync(int itemId, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var item = await _db.PurchaseOrderItems
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new DomainException("صنف الطلبية غير موجود.");

        if (item.IsShelved)
            throw new DomainException("هذا الصنف تم استلامه بالفعل.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            item.AssignToAreaZ(userId);

            // AREAZ-01: if an active row exists for this product, increase it;
            // otherwise create a new row. The partial unique index keeps us honest.
            var existing = await _db.AreaZInventory
                .FirstOrDefaultAsync(az => az.ProductId == item.ProductId && !az.IsDispatched, ct);

            if (existing != null)
            {
                existing.Update(
                    existing.BundleCount + item.BundleCount,
                    item.UnitsPerBundle,
                    existing.Notes,
                    userId);
            }
            else
            {
                var entry = AreaZInventory.Create(
                    item.ProductId, item.BundleCount, item.UnitsPerBundle, userId);
                _db.AreaZInventory.Add(entry);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Item {ItemId} (order {OrderId}) received to Area Z by {User}",
                itemId, item.OrderId, userId);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

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
