using ManolyWarehouse.Domain.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Domain.Exceptions;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Infrastructure.Services;

/// <summary>
/// Which part of the warehouse the signed-in user may change:
/// the big warehouse (shelves) and/or Area Z. Set per user on the users page.
/// </summary>
public interface IEditAccess
{
    Task<bool> CanEditWarehouseAsync();
    Task<bool> CanEditAreaZAsync();
    /// <summary>False only for view-only users.</summary>
    Task<bool> CanEditAnythingAsync();
    /// <summary>The wood warehouse: users who edit everywhere, and "wood only" users.</summary>
    Task<bool> CanEditWoodAsync();
    /// <summary>null = everywhere, or warehouse / areaz / wood / view.</summary>
    Task<string?> ScopeAsync();
}

public class EditAccess : IEditAccess
{
    private readonly IHttpContextAccessor _http;
    private readonly AppDbContext _db;
    private string? _scope; private bool _loaded;

    public EditAccess(IHttpContextAccessor http, AppDbContext db) { _http = http; _db = db; }

    public async Task<string?> ScopeAsync()
    {
        if (_loaded) return _scope;
        var id = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        _scope = id == null ? null : await _db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.EditScope).FirstOrDefaultAsync();
        _loaded = true;
        return _scope;
    }

    public async Task<bool> CanEditWarehouseAsync() => ApplicationUser.ScopeHas(await ScopeAsync(), "warehouse");
    public async Task<bool> CanEditAreaZAsync() => ApplicationUser.ScopeHas(await ScopeAsync(), "areaz");
    /// <summary>Orders belong to the main warehouse: users who may edit the shelves or Area Z.</summary>
    public async Task<bool> CanEditAnythingAsync() { var s = await ScopeAsync(); return ApplicationUser.ScopeHas(s, "warehouse") || ApplicationUser.ScopeHas(s, "areaz"); }
    public async Task<bool> CanEditWoodAsync() => ApplicationUser.ScopeHas(await ScopeAsync(), "wood");
}

/// <summary>Blocks a stock change outside the user's area: [EditArea("warehouse")] or [EditArea("areaz")].</summary>
[AttributeUsage(AttributeTargets.Method)]
public class EditAreaAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _area;
    public EditAreaAttribute(string area) => _area = area;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var access = context.HttpContext.RequestServices.GetRequiredService<IEditAccess>();
        var ok = _area switch
        {
            "areaz" => await access.CanEditAreaZAsync(),
            "any" => await access.CanEditAnythingAsync(),
            "wood" => await access.CanEditWoodAsync(),
            _ => await access.CanEditWarehouseAsync()
        };
        var scope = await access.ScopeAsync();
        if (!ok && scope == "view")
            throw new DomainException("حسابك للعرض فقط — لا يمكنك تعديل أي شيء.");
        if (!ok && scope == "wood")
            throw new DomainException("حسابك يعدّل في مستودع الخشب فقط.");
        if (!ok && _area == "wood")
            throw new DomainException("ليس لديك صلاحية التعديل في مستودع الخشب.");
        if (!ok && _area == "any")
            throw new DomainException("ليس لديك صلاحية لهذا التعديل.");
        if (!ok)
            throw new DomainException(_area == "wood" ? "ليس لديك صلاحية التعديل في مستودع الخشب."
                : _area == "areaz"
                ? "ليس لديك صلاحية التعديل في Area Z. يمكنك التعديل في المستودع فقط."
                : "ليس لديك صلاحية التعديل في المستودع. يمكنك التعديل في Area Z فقط.");
        await next();
    }
}
