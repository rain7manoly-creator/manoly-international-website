using Microsoft.AspNetCore.Identity;

namespace ManolyWarehouse.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; private set; } = default!;
    public bool IsAdmin { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ApplicationUser() { }

    public static ApplicationUser Create(string userName, string fullName, bool isAdmin)
    {
        return new ApplicationUser
        {
            UserName = userName,
            FullName = fullName,
            IsAdmin = isAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string fullName, bool isAdmin)
    {
        FullName = fullName;
        IsAdmin = isAdmin;
    }

    /// <summary>Where this user may change stock: null = everywhere, "warehouse" = shelves only, "areaz" = Area Z only.</summary>
    public string? EditScope { get; private set; }
    public void SetEditScope(string? scope) => EditScope = scope is "warehouse" or "areaz" ? scope : null;

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
