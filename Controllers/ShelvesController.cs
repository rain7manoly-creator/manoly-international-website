using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AuthenticatedUser")]
[Route("shelves")]
public class ShelvesController : Controller
{
    private readonly IShelfService _shelfService;
    private readonly IWarehouseGridService _gridService;

    public ShelvesController(IShelfService shelfService, IWarehouseGridService gridService)
    {
        _shelfService = shelfService;
        _gridService = gridService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await _gridService.GetGridAsync(ct);
        return View(model);
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> Detail(string code, CancellationToken ct)
    {
        var model = await _shelfService.GetByCodeAsync(code, ct);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet("{code}/add")]
    public async Task<IActionResult> Add(string code, [FromServices] IProductService productService, CancellationToken ct)
    {
        ViewBag.Products = await productService.ListAsync(ct);
        return View(new AddShelfInventoryRequest { ShelfCode = code, Position = 1, BundleCount = 1, UnitsPerBundle = 1 });
    }

    [HttpPost("{code}/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string code, AddShelfInventoryRequest request, CancellationToken ct)
    {
        request.ShelfCode = code;
        if (!ModelState.IsValid) return View(request);

        await _shelfService.AddInventoryAsync(request, ct);
        return RedirectToAction(nameof(Detail), new { code });
    }

    [HttpGet("{code}/inventory/{id:int}/edit")]
    public async Task<IActionResult> Edit(string code, int id, CancellationToken ct)
    {
        var shelf = await _shelfService.GetByCodeAsync(code, ct);
        if (shelf == null) return NotFound();
        var slot = shelf.Slots.FirstOrDefault(s => s.InventoryId == id);
        if (slot == null) return NotFound();
        ViewBag.ShelfCode = code;
        ViewBag.InventoryId = id;
        ViewBag.BundleCount = slot.BundleCount;
        ViewBag.UnitsPerBundle = slot.UnitsPerBundle;
        return View("Edit");
    }

    [HttpPost("{code}/inventory/{id:int}/adjust")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(
        string code, int id, int bundleCount, int unitsPerBundle, string? notes, CancellationToken ct)
    {
        await _shelfService.AdjustInventoryAsync(id, bundleCount, unitsPerBundle, notes, ct);
        return RedirectToAction(nameof(Detail), new { code });
    }

    [HttpPost("{code}/inventory/{id:int}/delete")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string code, int id, CancellationToken ct)
    {
        await _shelfService.DeleteInventoryAsync(id, ct);
        return RedirectToAction(nameof(Detail), new { code });
    }
}
