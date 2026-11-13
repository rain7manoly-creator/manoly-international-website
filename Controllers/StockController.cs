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

    /// <summary>Notifications: finished and about-to-finish items.</summary>
    [HttpGet("/alerts")]
    public async Task<IActionResult> Alerts(CancellationToken ct) => View(await _stock.GetAlertsAsync(ct));

    /// <summary>Set an item's warning number. mode: default (warn at 0) · never · at (with alertAt).</summary>
    [HttpPost("alert")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAlert(string kind, int id, string mode, int? alertAt, string? back, CancellationToken ct)
    {
        int? value = mode switch { "never" => -1, "at" => Math.Max(0, alertAt ?? 0), _ => null };
        await _stock.SetAlertAsync(kind == "line", id, value, ct);
        TempData["SuccessMessage"] = "تم حفظ التنبيه.";
        if (!string.IsNullOrEmpty(back) && Url.IsLocalUrl(back)) return LocalRedirect(back);
        return Redirect("/alerts");
    }

    /// <summary>What doesn't match the warehouse yet, and a button to match again.</summary>
    [HttpGet("check")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Check(CancellationToken ct) => View(await _stock.GetCheckAsync(ct));

    [HttpPost("check")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MatchNow(CancellationToken ct)
    {
        var (linked, changed) = await _stock.MatchNowAsync(ct);
        TempData["SuccessMessage"] = $"تمت المطابقة: ربط {linked} منتج، تعديل {changed} صنف.";
        return RedirectToAction(nameof(Check));
    }

    [HttpPost("check/connect")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Connect(int productId, string target, string? back, CancellationToken ct)
    {
        var name = await _stock.ConnectProductAsync(productId, target ?? "", ct);
        TempData["SuccessMessage"] = $"تم الربط بـ «{name}».";
        if (!string.IsNullOrEmpty(back) && Url.IsLocalUrl(back)) return LocalRedirect(back);
        return RedirectToAction(nameof(Check));
    }

    /// <summary>From a shelf: choose the stock line for one product.</summary>
    [HttpGet("connect")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ConnectProduct(int productId, string? back, CancellationToken ct)
    {
        var model = await _stock.GetConnectAsync(productId, ct);
        if (model is null) return NotFound();
        ViewBag.Back = !string.IsNullOrEmpty(back) && Url.IsLocalUrl(back) ? back : "/stock/check";
        return View("Connect", model);
    }

    /// <summary>From a stock line: choose products from the shelves / Area Z to count in it.</summary>
    [HttpGet("items/{id:int}/link")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> LinkProducts(int id, CancellationToken ct)
    {
        var model = await _stock.GetLinkCandidatesAsync(id, ct);
        if (model is null) return NotFound();
        return View("LinkProducts", model);
    }

    [HttpPost("items/{id:int}/unlink")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlink(int id, int productId, CancellationToken ct)
    {
        await _stock.LinkProductAsync(productId, null, ct);
        TempData["SuccessMessage"] = "تم إلغاء الربط.";
        return RedirectToAction(nameof(Item), new { id });
    }

    /// <summary>The team's own stock file (sh · metro · screws · kent), same layout, numbers from the app.</summary>
    [HttpGet("export/{key}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ExportFile(string key, CancellationToken ct)
    {
        var result = ManolyWarehouse.Domain.Entities.StockGroupRegistry.IsCustom(key)
            ? await BuildGroupExcelAsync(key, ct)
            : await _files.BuildAsync(key, ct);
        if (result is null) return NotFound();
        return File(result.Value.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Value.FileName);
    }

    // ── Created stock groups (e.g. مفصلات SH) ─────────────────────────

    [HttpGet("groups/new")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult NewGroup() => View();

    [HttpPost("groups/new")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewGroup(string name, string unit, string? lines, CancellationToken ct)
    {
        var key = await _stock.CreateGroupAsync(name, unit == "carton", lines, ct);
        TempData["SuccessMessage"] = "تم إنشاء المخزون.";
        return Redirect($"/stock/groups/{key}");
    }

    /// <summary>Manage one group: add lines; for created groups also rename, change unit, rename/delete lines.</summary>
    [HttpGet("groups/{family}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Group(string family, CancellationToken ct)
    {
        if (!StockService.NewLineFamilies.Contains(family)) return NotFound();
        var page = await _stock.GetPageAsync(ct);
        ViewBag.Family = family;
        return View(page.Items.Where(i => i.Family == family).ToList());
    }

    [HttpPost("groups/{family}/edit")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGroup(string family, string name, string unit, CancellationToken ct)
    {
        await _stock.UpdateGroupAsync(family, name, unit == "carton", ct);
        TempData["SuccessMessage"] = "تم الحفظ.";
        return Redirect($"/stock/groups/{family}");
    }

    [HttpPost("groups/{family}/delete")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(string family, CancellationToken ct)
    {
        await _stock.DeleteGroupAsync(family, ct);
        TempData["SuccessMessage"] = "تم حذف المخزون.";
        return Redirect("/stock");
    }

    [HttpPost("groups/{family}/add")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLines(string family, string lines, CancellationToken ct)
    {
        var n = await _stock.AddLinesAsync(family, lines, ct);
        TempData["SuccessMessage"] = $"تمت إضافة {n} سطر.";
        return Redirect($"/stock/groups/{family}");
    }

    [HttpPost("items/{id:int}/rename")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameLine(int id, string name, string? back, CancellationToken ct)
    {
        await _stock.RenameLineAsync(id, name, ct);
        TempData["SuccessMessage"] = "تم تعديل الاسم.";
        return LocalRedirect(!string.IsNullOrEmpty(back) && Url.IsLocalUrl(back) ? back : $"/stock/items/{id}");
    }

    [HttpPost("items/{id:int}/delete")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLine(int id, CancellationToken ct)
    {
        var family = await _stock.DeleteLineAsync(id, ct);
        TempData["SuccessMessage"] = "تم حذف السطر.";
        return Redirect($"/stock/groups/{family}");
    }

    /// <summary>A created group as a plain sheet: name, warehouse, each Area Z warehouse, total.</summary>
    private async Task<(byte[] Bytes, string FileName)?> BuildGroupExcelAsync(string family, CancellationToken ct)
    {
        var name = StockService.FamilyNames.GetValueOrDefault(family, family);
        var items = (await _stock.GetPageAsync(ct)).Items.Where(i => i.Family == family).ToList();
        var zNames = items.SelectMany(i => i.AreaZ).Select(z => z.Name).Distinct().ToList();
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet(name.Length > 31 ? name[..31] : name);
        ws.RightToLeft = true;
        var head = new List<string> { "الصنف", "المستودع" }; head.AddRange(zNames); head.Add("المجموع");
        for (var c = 0; c < head.Count; c++) ws.Cell(1, c + 1).Value = head[c];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var i in items)
        {
            ws.Cell(r, 1).Value = i.Name;
            ws.Cell(r, 2).Value = i.Shelves ?? 0;
            for (var z = 0; z < zNames.Count; z++) ws.Cell(r, 3 + z).Value = i.AreaZ.Where(a => a.Name == zNames[z]).Sum(a => a.Qty);
            ws.Cell(r, head.Count).Value = i.Total ?? 0;
            r++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), $"{name} {DateTime.Now:yyyy-MM-dd}.xlsx");
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
