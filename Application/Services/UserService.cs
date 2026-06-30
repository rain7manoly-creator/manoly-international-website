using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Domain.Exceptions;
using ManolyWarehouse.Infrastructure.Seeding;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Application.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserViewModel>> ListAsync(CancellationToken ct = default)
    {
        return await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new UserViewModel
            {
                Id = u.Id,
                UserName = u.UserName!,
                FullName = u.FullName,
                IsAdmin = u.IsAdmin,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<UserViewModel?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return null;

        return new UserViewModel
        {
            Id = user.Id,
            UserName = user.UserName!,
            FullName = user.FullName,
            IsAdmin = user.IsAdmin,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<string> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        var existing = await _userManager.FindByNameAsync(request.UserName);
        if (existing != null)
            throw new DomainException("اسم المستخدم مستخدم بالفعل.");

        var user = ApplicationUser.Create(
            request.UserName.Trim(),
            request.FullName.Trim(),
            request.IsAdmin);

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            _logger.LogWarning("User creation failed: {Errors}", errors);
            throw new DomainException($"فشل إنشاء الحساب: {errors}");
        }

        var role = request.IsAdmin ? DbInitializer.AdminRole : DbInitializer.WorkerRole;
        await _userManager.AddToRoleAsync(user, role);

        _logger.LogInformation(
            "User {NewUserId} ({UserName}) created with role {Role} by {Admin}",
            user.Id, user.UserName, role, userId);

        return user.Id;
    }

    public async Task UpdateProfileAsync(
        string id, string fullName, bool isAdmin, CancellationToken ct = default)
    {
        var currentUserId = RequireUserId();

        var user = await _userManager.FindByIdAsync(id)
            ?? throw new DomainException("المستخدم غير موجود.");

        // AUTH-05: Admin cannot change their own role, but can rename themselves
        if (id == currentUserId && isAdmin != user.IsAdmin)
            throw new DomainException("لا يمكنك تعديل صلاحيات حسابك الخاص.");

        var wasAdmin = user.IsAdmin;
        user.UpdateProfile(fullName.Trim(), isAdmin);

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new DomainException("فشل تحديث الحساب.");

        // Sync the role assignment
        if (wasAdmin && !isAdmin)
        {
            await _userManager.RemoveFromRoleAsync(user, DbInitializer.AdminRole);
            await _userManager.AddToRoleAsync(user, DbInitializer.WorkerRole);
        }
        else if (!wasAdmin && isAdmin)
        {
            await _userManager.RemoveFromRoleAsync(user, DbInitializer.WorkerRole);
            await _userManager.AddToRoleAsync(user, DbInitializer.AdminRole);
        }

        _logger.LogInformation(
            "User {Id} profile updated (admin={IsAdmin}) by {Admin}",
            id, isAdmin, currentUserId);
    }

    public async Task ResetPasswordAsync(string id, string newPassword, CancellationToken ct = default)
    {
        var currentUserId = RequireUserId();

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new DomainException("كلمة المرور يجب أن تكون 8 أحرف على الأقل.");

        var user = await _userManager.FindByIdAsync(id)
            ?? throw new DomainException("المستخدم غير موجود.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (!resetResult.Succeeded)
        {
            var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
            throw new DomainException($"فشل تعيين كلمة المرور: {errors}");
        }

        _logger.LogInformation("Password reset for user {Id} by {Admin}", id, currentUserId);
    }

    public async Task ToggleActiveAsync(string id, CancellationToken ct = default)
    {
        var currentUserId = RequireUserId();

        // AUTH-05: Admin cannot disable their own account
        if (id == currentUserId)
            throw new DomainException("لا يمكنك تعطيل حسابك الخاص.");

        var user = await _userManager.FindByIdAsync(id)
            ?? throw new DomainException("المستخدم غير موجود.");

        if (user.IsActive) user.Deactivate();
        else user.Activate();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new DomainException("فشل تحديث حالة الحساب.");

        _logger.LogInformation(
            "User {Id} {Action} by {Admin}",
            id, user.IsActive ? "activated" : "deactivated", currentUserId);
    }

    private string RequireUserId() =>
        _currentUser.UserId
            ?? throw new InvalidOperationException("No authenticated user in context.");
}
