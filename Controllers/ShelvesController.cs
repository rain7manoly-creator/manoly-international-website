using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AuthenticatedUser")]
[Route("shelves")]
public class ShelvesController : Controller
{
    private readonly IShelfService _shelfService;
    private readonly IWarehouseGridService _gridService;
    private readonly IProductService _productService;

    public ShelvesController(IShelfService shelfService, IWarehouseGridService gridService, IProductService productService)
    {
        _shelfService = shelfService;
        _gridService = gridService;
        _productService = productService;
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
    public async Task<IActionResult> Add(string code, int? position, CancellationToken ct)
    {
        var shelf = await _shelfService.GetByCodeAsync(code, ct);
        if (shelf == null) return NotFound();

        ViewBag.Products = await _productService.ListAsync(ct);
        ViewBag.MaxPositions = shelf.MaxPositions;
        ViewBag.OccupiedPositions = shelf.Slots
            .Where(s => !s.IsEmpty)
            .ToDictionary(s => s.Position, s => s.ProductName ?? "");

        var firstFree = Enumerable.Range(1, shelf.MaxPositions).FirstOrDefault(p => !shelf.Slots.Any(s => s.Position == p && !s.IsEmpty));
        return View(new AddShelfInventoryRequest { ShelfCode = code, Position = position ?? firstFree, BundleCount = 1, UnitsPerBundle = 1 });
    }

    [HttpPost("{code}/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string code, AddShelfInventoryRequest request, CancellationToken ct)
    {
        request.ShelfCode = code;
        if (!ModelState.IsValid)
        {
            ViewBag.Products = await _productService.ListAsync(ct);
            var shelf = await _shelfService.GetByCodeAsync(code, ct);
            ViewBag.MaxPositions = shelf?.MaxPositions ?? Shelf.MaxSlots;
            ViewBag.OccupiedPositions = shelf?.Slots
                .Where(s => !s.IsEmpty)
                .ToDictionary(s => s.Position, s => s.ProductName ?? "")
                ?? new Dictionary<int, string>();
            return View(request);
        }

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
