using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Interfaces;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("categories")]
public class CategoriesController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await _categoryService.ListAsync(ct);
        return View(model);
    }

    [HttpGet("add")]
    public IActionResult Add() => View();

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("name", "اسم التصنيف مطلوب");
            return View();
        }

        await _categoryService.CreateAsync(name, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, CancellationToken ct)
    {
        await _categoryService.UpdateAsync(id, name, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _categoryService.DeleteAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }
}
