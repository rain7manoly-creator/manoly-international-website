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
    public async Task<IActionResult> Edit(string id, string fullName, bool isAdmin, CancellationToken ct)
    {
        await _userService.UpdateProfileAsync(id, fullName, isAdmin, ct);
        return RedirectToAction(nameof(Index));
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
