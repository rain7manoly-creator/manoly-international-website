using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Domain.Exceptions;
using ManolyWarehouse.Infrastructure.Persistence;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Application.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ILogger<ProductService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductListItemViewModel>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Category.Name)
            .ThenBy(p => p.Name)
            .Select(p => new ProductListItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                CategoryName = p.Category.Name,
                ShelfLocationCount = p.ShelfInventories.Count,
                IsInAreaZ = p.AreaZInventories.Any(az => !az.IsDispatched)
            })
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ProductListItemViewModel>> ListPagedAsync(
        int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking();

        // Tokenised search: every token must appear in name OR category name
        if (!string.IsNullOrWhiteSpace(search))
        {
            var tokens = search.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct();
            foreach (var token in tokens)
            {
                var pat = $"%{token}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, pat) ||
                    EF.Functions.ILike(p.Category.Name, pat));
            }
        }

        query = query.OrderBy(p => p.Category.Name).ThenBy(p => p.Name);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                CategoryName = p.Category.Name,
                ShelfLocationCount = p.ShelfInventories.Count,
                IsInAreaZ = p.AreaZInventories.Any(az => !az.IsDispatched)
            })
            .ToListAsync(ct);

        return new PagedResult<ProductListItemViewModel>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProductDetailViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.CategoryId,
                CategoryName = p.Category.Name
            })
            .FirstOrDefaultAsync(ct);

        if (product is null) return null;

        var shelfLocations = await _db.ShelfInventory
            .AsNoTracking()
            .Where(si => si.ProductId == id)
            .OrderBy(si => si.Shelf.Code)
            .Select(si => new ProductShelfLocationViewModel
            {
                InventoryId = si.Id,
                ShelfCode = si.Shelf.Code,
                Position = si.Position,
                BundleCount = si.BundleCount,
                UnitsPerBundle = si.UnitsPerBundle
            })
            .ToListAsync(ct);

        var areaZ = await _db.AreaZInventory
            .AsNoTracking()
            .Where(az => az.ProductId == id && !az.IsDispatched)
            .Select(az => new AreaZInventoryViewModel
            {
                Id = az.Id,
                BundleCount = az.BundleCount,
                UnitsPerBundle = az.UnitsPerBundle,
                Notes = az.Notes,
                EnteredAt = az.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        return new ProductDetailViewModel
        {
            Id = product.Id,
            Name = product.Name,
            CategoryId = product.CategoryId,
            CategoryName = product.CategoryName,
            ShelfLocations = shelfLocations,
            AreaZ = areaZ
        };
    }

    public async Task<int> CreateAsync(string name, int categoryId, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var trimmed = ValidateName(name);

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == categoryId, ct);
        if (!categoryExists)
            throw new DomainException("التصنيف المحدد غير موجود.");

        // PROD-01: Name unique within the same category
        var conflict = await _db.Products
            .AnyAsync(p => p.CategoryId == categoryId && p.Name == trimmed, ct);
        if (conflict)
            throw new DomainException("منتج بهذا الاسم موجود بالفعل في نفس التصنيف.");

        var product = Product.Create(trimmed, categoryId, userId);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Product {Id} created: {Name} (cat {CategoryId}) by {User}",
            product.Id, trimmed, categoryId, userId);

        return product.Id;
    }

    public async Task UpdateAsync(int id, string name, int categoryId, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var trimmed = ValidateName(name);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainException("المنتج غير موجود.");

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == categoryId, ct);
        if (!categoryExists)
            throw new DomainException("التصنيف المحدد غير موجود.");

        var conflict = await _db.Products
            .AnyAsync(p => p.Id != id && p.CategoryId == categoryId && p.Name == trimmed, ct);
        if (conflict)
            throw new DomainException("منتج بهذا الاسم موجود بالفعل في نفس التصنيف.");

        product.Update(trimmed, categoryId, userId);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Product {Id} updated by {User}", id, userId);
    }

    public async Task<ProductDeletionImpact> GetDeletionImpactAsync(int id, CancellationToken ct = default)
    {
        var shelfCount = await _db.ShelfInventory.CountAsync(si => si.ProductId == id, ct);
        var areaZCount = await _db.AreaZInventory
            .CountAsync(az => az.ProductId == id && !az.IsDispatched, ct);
        var orderItemsCount = await _db.PurchaseOrderItems.CountAsync(poi => poi.ProductId == id, ct);

        return new ProductDeletionImpact
        {
            ShelfInventoryCount = shelfCount,
            AreaZCount = areaZCount,
            OrderItemsCount = orderItemsCount
        };
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainException("المنتج غير موجود.");

        // PROD-03: Block delete if product is referenced by completed purchase order items
        // ShelfInventory and AreaZ rows must be removed first (the UI shows a warning)
        var hasOrderHistory = await _db.PurchaseOrderItems.AnyAsync(poi => poi.ProductId == id, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Remove all linked shelf inventory rows (their logs cascade automatically)
            var shelfRows = await _db.ShelfInventory.Where(si => si.ProductId == id).ToListAsync(ct);
            _db.ShelfInventory.RemoveRange(shelfRows);

            // Remove all Area Z rows (both active and dispatched)
            var areaZRows = await _db.AreaZInventory.Where(az => az.ProductId == id).ToListAsync(ct);
            _db.AreaZInventory.RemoveRange(areaZRows);

            // If there's order history, refuse — the product has a paper trail we can't break
            if (hasOrderHistory)
            {
                _logger.LogWarning(
                    "Delete of product {Id} blocked: linked to existing purchase order items", id);
                throw new DomainException(
                    "لا يمكن حذف هذا المنتج لأنه مرتبط بطلبيات سابقة. تواصل مع المسؤول.");
            }

            _db.Products.Remove(product);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Product {Id} deleted by {User} ({ShelfRows} shelf, {AreaZRows} area-z rows removed)",
                id, userId, shelfRows.Count, areaZRows.Count);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("اسم المنتج مطلوب.");
        var trimmed = name.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 200)
            throw new DomainException("اسم المنتج يجب أن يكون بين 2 و 200 حرف.");
        return trimmed;
    }

    private string RequireUserId() =>
        _currentUser.UserId
            ?? throw new InvalidOperationException("No authenticated user in context.");
}
