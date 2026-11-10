using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Services;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AuthenticatedUser")]
[Route("stock")]
public class StockController : Controller
{
    private readonly IStockService _stock;
    private readonly IWarehouseMapExport _map;
    private readonly IStockFileExport _files;

    public StockController(IStockService stock, IWarehouseMapExport map, IStockFileExport files)
    {
        _stock = stock;
        _map = map;
        _files = files;
    }

    /// <summary>The team's own stock file (sh · metro · screws · kent), same layout, numbers from the app.</summary>
    [HttpGet("export/{key}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ExportFile(string key, CancellationToken ct)
    {
        var result = await _files.BuildAsync(key, ct);
        if (result is null) return NotFound();
        return File(result.Value.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Value.FileName);
    }

    /// <summary>Stock search: Metro, SH, screws, Kent first, then every other product in the app.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _stock.GetPageAsync(ct));

    /// <summary>One stock line: its numbers, linked products and full history. Admins can correct it here.</summary>
    [HttpGet("items/{id:int}")]
    public async Task<IActionResult> Item(int id, CancellationToken ct)
    {
        var model = await _stock.GetItemAsync(id, ct);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpPost("items/{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Item(int id, int? warehouse, int? shops, int? ordered, int? onWay, int? total,
        string? note, CancellationToken ct)
    {
        await _stock.UpdateAsync(id, warehouse, shops, ordered, onWay, total, note, ct);
        TempData["SuccessMessage"] = "تم حفظ المخزون.";
        return RedirectToAction(nameof(Item), new { id });
    }

    /// <summary>Download the shelves as the "Rain New WareHouse" Excel, same layout as the hand-kept file.</summary>
    [HttpGet("export-warehouse")]
    public async Task<IActionResult> ExportWarehouse(CancellationToken ct)
    {
        var bytes = await _map.BuildAsync(ct);
        var local = Extensions.LocalTimeExtensions.ToLocal(DateTime.UtcNow);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Rain New Warehouse {local:yyyy-MM-dd}.xlsx");
    }

    /// <summary>Download the current stock as Excel, one sheet per family, for preparing orders.</summary>
    [HttpGet("export")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var bytes = await _stock.ExportExcelAsync(ct);
        var local = Extensions.LocalTimeExtensions.ToLocal(DateTime.UtcNow);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"manoly-stock-{local:yyyy-MM-dd}.xlsx");
    }
}
