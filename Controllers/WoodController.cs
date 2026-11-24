using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Services;
using ManolyWarehouse.Infrastructure.Services;

namespace ManolyWarehouse.Controllers;

/// <summary>The wood warehouse (مستودع الخشب) — separate from the shelves, Area Z and the drawer stock.</summary>
[Authorize(Policy = "AuthenticatedUser")]
[Route("wood")]
public class WoodController : Controller
{
    private readonly IWoodService _wood;
    public WoodController(IWoodService wood) => _wood = wood;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _wood.ListAsync(ct));

    /// <summary>The wood warehouse's own warnings (separate from the main warehouse's).</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(CancellationToken ct) => View(await _wood.LowAsync(ct));

    [HttpGet("add")]
    [EditArea("wood")]
    public async Task<IActionResult> Add(CancellationToken ct)
    {
        ViewBag.Types = await _wood.TypesAsync(ct);
        return View();
    }

    [HttpPost("add")]
    [EditArea("wood")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string type, decimal? thickness, decimal? length, decimal? width, string? color, string? notes, decimal quantity,
        string? code, string? name, string? unit, string? perBundle, string? again, CancellationToken ct)
    {
        var id = await _wood.CreateAsync(type, thickness, length, width, color, notes, quantity, code, name, unit, perBundle, ct);
        TempData["SuccessMessage"] = "تمت إضافة صنف الخشب.";
        return again == "1" ? Redirect($"/wood/add?type={Uri.EscapeDataString(type ?? "")}") : Redirect($"/wood#w{id}");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Item(int id, CancellationToken ct)
    {
        var d = await _wood.GetAsync(id, ct);
        if (d == null) return NotFound();
        ViewBag.Types = await _wood.TypesAsync(ct);
        return View(d);
    }

    /// <summary>New quantity (from − / + on the list, or typed on the item page).</summary>
    [HttpPost("{id:int}/qty")]
    [EditArea("wood")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Qty(int id, decimal quantity, string? note, string? back, CancellationToken ct)
    {
        await _wood.SetQuantityAsync(id, quantity, note, ct);
        TempData["SuccessMessage"] = $"تم الحفظ: {quantity:#,0.###}.";
        return LocalRedirect(!string.IsNullOrEmpty(back) && Url.IsLocalUrl(back) ? back : $"/wood#w{id}");
    }

    [HttpPost("{id:int}/edit")]
    [EditArea("wood")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string type, decimal? thickness, decimal? length, decimal? width, string? color, string? notes,
        string? code, string? name, string? unit, string? perBundle, CancellationToken ct)
    {
        await _wood.UpdateDetailsAsync(id, type, thickness, length, width, color, notes, code, name, unit, perBundle, ct);
        TempData["SuccessMessage"] = "تم حفظ التفاصيل.";
        return Redirect($"/wood/{id}");
    }

    [HttpPost("{id:int}/alert")]
    [EditArea("wood")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Alert(int id, string mode, int? at, CancellationToken ct)
    {
        await _wood.SetAlertAsync(id, mode switch { "never" => -1, "at" => Math.Max(1, at ?? 1), _ => null }, ct);
        TempData["SuccessMessage"] = "تم حفظ التنبيه.";
        return Redirect($"/wood/{id}");
    }

    [HttpPost("{id:int}/delete")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _wood.DeleteAsync(id, ct);
        TempData["SuccessMessage"] = "تم حذف صنف الخشب.";
        return Redirect("/wood");
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var rows = await _wood.ListAsync(ct);
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("مستودع الخشب");
        ws.RightToLeft = true;
        string[] head = { "كود", "الصنف", "المجموعة", "الكمية", "الوحدة", "لوح بالربطة", "ربطة", "ملاحظة" };
        for (var c = 0; c < head.Length; c++) ws.Cell(1, c + 1).Value = head[c];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var w in rows)
        {
            ws.Cell(r, 1).Value = w.Code ?? "";
            ws.Cell(r, 2).Value = w.Name;
            ws.Cell(r, 3).Value = w.Type;
            ws.Cell(r, 4).Value = w.Quantity;
            ws.Cell(r, 5).Value = w.Unit;
            ws.Cell(r, 6).Value = w.PerBundle ?? "";
            if (w.Bundles is decimal bn) ws.Cell(r, 7).Value = bn;
            ws.Cell(r, 8).Value = w.Notes ?? "";
            r++;
        }
        ws.Cell(r, 3).Value = "مجموع الألواح"; ws.Cell(r, 4).FormulaA1 = $"SUMIF(E2:E{r - 1},\"لوح\",D2:D{r - 1})"; ws.Row(r).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"مستودع الخشب {DateTime.Now:yyyy-MM-dd}.xlsx");
    }
}
