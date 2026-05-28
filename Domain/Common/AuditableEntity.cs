namespace ManolyWarehouse.Domain.Common;

public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = default!;
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    protected AuditableEntity() { }

    public void SetCreated(string userId)
    {
        CreatedAt = DateTime.UtcNow;
        CreatedBy = userId;
    }

    public void SetUpdated(string userId)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = userId;
    }
}
