using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Domain.Exceptions;
using ManolyWarehouse.Infrastructure.Persistence;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ILogger<CategoryService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CategoryViewModel>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryViewModel
            {
                Id = c.Id,
                Name = c.Name,
                ProductCount = c.Products.Count
            })
            .ToListAsync(ct);
    }

    public async Task<int> CreateAsync(string name, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var trimmed = ValidateName(name);

        // CAT-01: Unique name (also enforced by DB unique index — this gives a friendly error)
        var exists = await _db.Categories.AnyAsync(c => c.Name == trimmed, ct);
        if (exists)
            throw new DomainException("تصنيف بهذا الاسم موجود بالفعل.");

        var category = Category.Create(trimmed, userId);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Category {Id} created: {Name} by {User}", category.Id, trimmed, userId);
        return category.Id;
    }

    public async Task UpdateAsync(int id, string name, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var trimmed = ValidateName(name);

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new DomainException("التصنيف غير موجود.");

        var conflict = await _db.Categories
            .AnyAsync(c => c.Id != id && c.Name == trimmed, ct);
        if (conflict)
            throw new DomainException("تصنيف آخر بهذا الاسم موجود بالفعل.");

        category.Update(trimmed, userId);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Category {Id} updated to {Name} by {User}", id, trimmed, userId);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new DomainException("التصنيف غير موجود.");

        // CAT-02: Cannot delete a category with linked products
        var productCount = await _db.Products.CountAsync(p => p.CategoryId == id, ct);
        if (productCount > 0)
            throw new CategoryInUseException(productCount);

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Category {Id} deleted by {User}", id, userId);
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("اسم التصنيف مطلوب.");
        var trimmed = name.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 100)
            throw new DomainException("اسم التصنيف يجب أن يكون بين 2 و 100 حرف.");
        return trimmed;
    }

    private string RequireUserId() =>
        _currentUser.UserId
            ?? throw new InvalidOperationException("No authenticated user in context.");
}
