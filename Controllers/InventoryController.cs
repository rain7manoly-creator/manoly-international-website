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

    private const int PageSize = 20;

    [HttpGet("/inventory")]
    public async Task<IActionResult> Index(
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var vm = await _inventorySvc.GetSummaryAsync(category, page, PageSize, ct);
        return View(vm);
    }
}
