using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AuthenticatedUser")]
[Route("area-z")]
public class AreaZController : Controller
{
    private readonly IAreaZService _areaZService;

    public AreaZController(IAreaZService areaZService)
    {
        _areaZService = areaZService;
    }

    private const int PageSize = 20;

    /// <summary>Area Z (Inside company Warehouses): all warehouses, or one with ?w=id.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int? w = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var warehouses = await _areaZService.ListWarehousesAsync(ct);
        if (w is not null && warehouses.All(x => x.Id != w)) w = null;
        ViewBag.Warehouses = warehouses;
        ViewBag.WarehouseId = w;
        var model = await _areaZService.ListActiveAsync(page, PageSize, w, ct);
        return View(model);
    }

    [HttpGet("add")]
    public async Task<IActionResult> Add([FromServices] IProductService productService, int? w, CancellationToken ct)
    {
        ViewBag.Products = await productService.ListAsync(ct);
        ViewBag.Warehouses = await _areaZService.ListWarehousesAsync(ct);
        ViewBag.WarehouseId = w;
        return View();
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        int productId, int bundleCount, int unitsPerBundle, string? notes, int warehouseId, CancellationToken ct)
    {
        await _areaZService.AddAsync(productId, bundleCount, unitsPerBundle, notes, warehouseId, ct);
        return RedirectToAction(nameof(Index), new { w = warehouseId });
    }

    // ── Warehouses inside Area Z ──────────────────────────────────────
    [HttpGet("warehouses")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
        => View(await _areaZService.ListWarehousesAsync(ct));

    [HttpPost("warehouses")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWarehouse(string name, CancellationToken ct)
    {
        var id = await _areaZService.CreateWarehouseAsync(name, ct);
        TempData["SuccessMessage"] = "تم إنشاء المستودع.";
        return RedirectToAction(nameof(Index), new { w = id });
    }

    [HttpPost("warehouses/{id:int}/rename")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameWarehouse(int id, string name, CancellationToken ct)
    {
        await _areaZService.RenameWarehouseAsync(id, name, ct);
        TempData["SuccessMessage"] = "تم تغيير الاسم.";
        return RedirectToAction(nameof(Warehouses));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var model = await _areaZService.GetByIdAsync(id, ct);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet("{id:int}/edit")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await _areaZService.GetByIdAsync(id, ct);
        if (model == null) return NotFound();
        ViewBag.Warehouses = await _areaZService.ListWarehousesAsync(ct);
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id, int bundleCount, int unitsPerBundle, string? notes, int? warehouseId, CancellationToken ct)
    {
        await _areaZService.UpdateAsync(id, bundleCount, unitsPerBundle, notes, warehouseId, ct);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/delete")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _areaZService.DeleteAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/dispatch")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispatch(int id, CancellationToken ct)
    {
        await _areaZService.DispatchAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/shelve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Shelve(
        int id, string shelfCode, int position,
        [FromServices] ManolyWarehouse.Infrastructure.Persistence.AppDbContext db,
        CancellationToken ct)
    {
        var shelf = await db.Shelves
            .FirstOrDefaultAsync(s => s.Code == shelfCode.ToUpper().Trim(), ct);

        if (shelf == null)
        {
            TempData["ErrorMessage"] = $"الرف {shelfCode} غير موجود.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        await _areaZService.MoveToShelfAsync(id, shelf.Id, position, ct);
        TempData["SuccessMessage"] = $"تم نقل المنتج إلى الرف {shelfCode}";
        return RedirectToAction(nameof(Index));
    }
}
