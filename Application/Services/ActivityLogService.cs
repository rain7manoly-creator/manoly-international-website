using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Infrastructure.Persistence;
using ManolyWarehouse.Infrastructure.Seeding;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Application.Services;

public class ActivityLogService : IActivityLogService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ILogger<ActivityLogService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task RecordAsync(ActivityArea area, string action, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            // No authenticated user — nothing to attribute the action to. Skip rather
            // than throw, so audit logging never breaks the underlying operation.
            _logger.LogWarning("Activity log skipped (no user in context): {Action}", action);
            return;
        }

        // Snapshot the display name + role at write time so history stays faithful.
        var name = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? userId;

        var role = _currentUser.IsAdmin ? DbInitializer.AdminRole : DbInitializer.WorkerRole;

        _db.ActivityLogs.Add(ActivityLog.Create(area, action, userId, name, role));
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<ActivityLogViewModel>> ListAsync(
        string? areaFilter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.ActivityLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(areaFilter)
            && Enum.TryParse<ActivityArea>(areaFilter, true, out var area))
        {
            query = query.Where(a => a.Area == area);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityLogViewModel
            {
                Id = a.Id,
                Area = a.Area,
                Action = a.Action,
                PerformedByName = a.PerformedByName,
                PerformedByRole = a.PerformedByRole,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<ActivityLogViewModel>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
