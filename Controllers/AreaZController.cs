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

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await _areaZService.ListActiveAsync(ct);
        return View(model);
    }

    [HttpGet("add")]
    public async Task<IActionResult> Add([FromServices] IProductService productService, CancellationToken ct)
    {
        ViewBag.Products = await productService.ListAsync(ct);
        return View();
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        int productId, int bundleCount, int unitsPerBundle, string? notes, CancellationToken ct)
    {
        await _areaZService.AddAsync(productId, bundleCount, unitsPerBundle, notes, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var model = await _areaZService.GetByIdAsync(id, ct);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id, int bundleCount, int unitsPerBundle, string? notes, CancellationToken ct)
    {
        await _areaZService.UpdateAsync(id, bundleCount, unitsPerBundle, notes, ct);
        return RedirectToAction(nameof(Detail), new { id });
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
