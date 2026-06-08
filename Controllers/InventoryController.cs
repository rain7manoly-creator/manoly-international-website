using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Services;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AuthenticatedUser")]
public class InventoryController : Controller
{
    private readonly IInventoryService _inventorySvc;

    public InventoryController(IInventoryService inventorySvc)
        => _inventorySvc = inventorySvc;

    [HttpGet("/inventory")]
    public async Task<IActionResult> Index(
        [FromQuery] string? category,
        CancellationToken ct)
    {
        var vm = await _inventorySvc.GetSummaryAsync(category, ct);
        return View(vm);
    }
}
