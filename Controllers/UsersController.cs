using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("users")]
public class UsersController : Controller
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await _userService.ListAsync(ct);
        return View(model);
    }

    [HttpGet("create")]
    public IActionResult Create() => View();

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(request);

        await _userService.CreateAsync(request, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string fullName, bool isAdmin, CancellationToken ct, string? editScope = null)
    {
        if (editScope == "view") isAdmin = false;   // view only = no admin rights either
        await _userService.UpdateProfileAsync(id, fullName, isAdmin, ct);
        if (Request.HasFormContentType && Request.Form.ContainsKey("editScope"))
            await _userService.SetEditScopeAsync(id, editScope, ct);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Where the user may edit — set straight from the users list.</summary>
    [HttpPost("{id}/scope")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scope(string id, string[]? areas, bool isAdmin, CancellationToken ct)
    {
        var u = await _userService.GetByIdAsync(id, ct);
        if (u is null) return NotFound();
        // An admin edits everywhere; otherwise the ticked places (none ticked = view only)
        var scope = isAdmin ? null : ManolyWarehouse.Domain.Entities.ApplicationUser.NormalizeScope(string.Join(",", areas ?? Array.Empty<string>()) is var j && j.Length == 0 ? "view" : j);
        if (u.IsAdmin != isAdmin)
            await _userService.UpdateProfileAsync(id, u.FullName, isAdmin, ct);
        await _userService.SetEditScopeAsync(id, scope ?? "", ct);
        TempData["SuccessMessage"] = $"تم حفظ صلاحيات {u.FullName}.";
        return Redirect("/users#u-" + id);
    }

    [HttpPost("{id}/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword, CancellationToken ct)
    {
        await _userService.ResetPasswordAsync(id, newPassword, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id}/toggle-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id, CancellationToken ct)
    {
        await _userService.ToggleActiveAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }
}
