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

    public ProductsController(IProductService productService, ICategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    private const int PageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var model = await _productService.ListPagedAsync(page, PageSize, ct);
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
        return View(model);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await _productService.GetByIdAsync(id, ct);
        if (model == null) return NotFound();
        ViewBag.Categories = await _categoryService.ListAsync(ct);
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, int categoryId, CancellationToken ct)
    {
        await _productService.UpdateAsync(id, name, categoryId, ct);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("{id:int}/delete")]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        if (product == null) return NotFound();

        var impact = await _productService.GetDeletionImpactAsync(id, ct);
        ViewBag.Impact = impact;
        return View(product);
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _productService.DeleteAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }
}
