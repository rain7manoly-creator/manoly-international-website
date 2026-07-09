namespace ManolyWarehouse.Domain.Entities;

/// <summary>Which part of the warehouse an activity-log entry belongs to.</summary>
public enum ActivityArea { Shelves, AreaZ, Orders }

/// <summary>
/// Append-only audit record. Captures a single change to the warehouse
/// (shelves, Area Z, or orders) together with who performed it. The user's
/// name and role are snapshotted at write time so the history stays faithful
/// even if the user is later renamed, has their role changed, or is removed.
/// </summary>
public class ActivityLog
{
    public int Id { get; private set; }
    public ActivityArea Area { get; private set; }
    public string Action { get; private set; } = default!;

    public string PerformedByUserId { get; private set; } = default!;
    public string PerformedByName { get; private set; } = default!;
    public string PerformedByRole { get; private set; } = default!;

    public DateTime CreatedAt { get; private set; }

    private ActivityLog() { }

    public static ActivityLog Create(
        ActivityArea area, string action,
        string userId, string userName, string role)
    {
        return new ActivityLog
        {
            Area = area,
            Action = action,
            PerformedByUserId = userId,
            PerformedByName = userName,
            PerformedByRole = role,
            CreatedAt = DateTime.UtcNow
        };
    }
}
