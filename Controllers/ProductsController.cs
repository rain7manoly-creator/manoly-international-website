using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Interfaces;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("products")]
public class ProductsController : Controller
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ManolyWarehouse.Application.Services.IStockService _stockService;

    public ProductsController(IProductService productService, ICategoryService categoryService,
        ManolyWarehouse.Application.Services.IStockService stockService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _stockService = stockService;
    }

    private const int PageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, string? q = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var model = await _productService.ListPagedAsync(page, PageSize, q, ct);
        ViewBag.SearchQuery = q ?? string.Empty;
        return View(model);
    }

    [HttpGet("add")]
    public async Task<IActionResult> Add(CancellationToken ct)
    {
        ViewBag.Categories = await _categoryService.ListAsync(ct);
        return View();
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string name, int categoryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("name", "اسم المنتج مطلوب");
            ViewBag.Categories = await _categoryService.ListAsync(ct);
            return View();
        }

        await _productService.CreateAsync(name, categoryId, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var model = await _productService.GetByIdAsync(id, ct);
        if (model == null) return NotFound();
        ViewBag.Stock = await _stockService.GetLinkForProductAsync(id, ct);
        ViewBag.AlertAt = await _stockService.GetProductAlertAtAsync(id, ct);
        return View(model);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await _productService.GetByIdAsync(id, ct);
        if (model == null) return NotFound();
        ViewBag.Categories = await _categoryService.ListAsync(ct);
        ViewBag.StockItems = await _stockService.ListForLinkingAsync(ct);
        ViewBag.CurrentStockId = (await _stockService.GetLinkForProductAsync(id, ct))?.Id;
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, int categoryId, int? stockItemId, string? back, CancellationToken ct)
    {
        await _productService.UpdateAsync(id, name, categoryId, ct);
        await _stockService.LinkProductAsync(id, stockItemId, ct);
        TempData["SuccessMessage"] = "تم حفظ المنتج — الاسم الجديد يظهر في كل الرفوف و Area Z والمخزون.";
        // Opened from a shelf: go back to that shelf
        if (!string.IsNullOrEmpty(back) && Url.IsLocalUrl(back)) return LocalRedirect(back);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("{id:int}/delete")]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        if (product == null) return NotFound();

        var impact = await _productService.GetDeletionImpactAsync(id, ct);
        ViewBag.ProductId = product.Id;
        ViewBag.ProductName = product.Name;
        return View(impact);
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _productService.DeleteAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }
}
