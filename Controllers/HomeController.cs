using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Interfaces;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AuthenticatedUser")]
public class HomeController : Controller
{
    private readonly IWarehouseGridService _gridService;

    public HomeController(IWarehouseGridService gridService)
    {
        _gridService = gridService;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await _gridService.GetGridAsync(ct);
        return View(model);
    }
}
