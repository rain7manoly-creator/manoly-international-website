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

    /// <summary>Where this user may change stock: null = everywhere, "warehouse" = shelves only, "areaz" = Area Z only, "view" = nothing (view only).</summary>
    public string? EditScope { get; private set; }
    public void SetEditScope(string? scope) => EditScope = NormalizeScope(scope);

    public static readonly string[] Areas = { "warehouse", "areaz", "wood" };

    /// <summary>Any mix of warehouse / areaz / wood, e.g. "areaz,wood". All three = null (everywhere); none = "view".</summary>
    public static string? NormalizeScope(string? scope)
    {
        if (scope == null) return null;
        if (scope.Trim() == "view") return "view";
        var parts = scope.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => Areas.Contains(p)).Distinct().OrderBy(p => Array.IndexOf(Areas, p)).ToList();
        if (parts.Count == 0) return scope.Trim().Length == 0 ? null : "view";
        return parts.Count == Areas.Length ? null : string.Join(",", parts);
    }

    public static bool ScopeHas(string? scope, string area) =>
        scope == null || (scope != "view" && scope.Split(',').Contains(area));

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
