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
}

public class EditAccess : IEditAccess
{
    private readonly IHttpContextAccessor _http;
    private readonly AppDbContext _db;
    private string? _scope; private bool _loaded;

    public EditAccess(IHttpContextAccessor http, AppDbContext db) { _http = http; _db = db; }

    private async Task<string?> ScopeAsync()
    {
        if (_loaded) return _scope;
        var id = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        _scope = id == null ? null : await _db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.EditScope).FirstOrDefaultAsync();
        _loaded = true;
        return _scope;
    }

    public async Task<bool> CanEditWarehouseAsync() => await ScopeAsync() is null or "warehouse";
    public async Task<bool> CanEditAreaZAsync() => await ScopeAsync() is null or "areaz";
    public async Task<bool> CanEditAnythingAsync() => await ScopeAsync() is not "view";
    public async Task<bool> CanEditWoodAsync() => await ScopeAsync() is null or "wood";
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
        if (!ok && (_area == "any" || !await access.CanEditAnythingAsync()))
            throw new DomainException("حسابك للعرض فقط — لا يمكنك تعديل أي شيء.");
        if (!ok)
            throw new DomainException(_area == "wood" ? "ليس لديك صلاحية التعديل في مستودع الخشب."
                : _area == "areaz"
                ? "ليس لديك صلاحية التعديل في Area Z. يمكنك التعديل في المستودع فقط."
                : "ليس لديك صلاحية التعديل في المستودع. يمكنك التعديل في Area Z فقط.");
        await next();
    }
}
