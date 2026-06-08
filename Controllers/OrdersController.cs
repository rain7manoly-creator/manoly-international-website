using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AuthenticatedUser")]
[Route("orders")]
public class OrdersController : Controller
{
    private readonly IPurchaseOrderService _orderService;

    public OrdersController(IPurchaseOrderService orderService)
    {
        _orderService = orderService;
    }

    private const int PageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var model = await _orderService.ListAsync(status, page, PageSize, ct);
        return View(model);
    }

    [HttpGet("create")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult Create() => View();

    [HttpPost("create")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(request);

        var orderId = await _orderService.CreateAsync(request, ct);
        return RedirectToAction(nameof(Detail), new { id = orderId });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, [FromServices] IProductService productService, CancellationToken ct)
    {
        var model = await _orderService.GetByIdAsync(id, ct);
        if (model == null) return NotFound();
        ViewBag.Products = await productService.ListAsync(ct);
        return View(model);
    }

    [HttpPost("{id:int}/advance")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Advance(int id, CancellationToken ct)
    {
        await _orderService.AdvanceStatusAsync(id, ct);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _orderService.CancelAsync(id, ct);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/delete")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _orderService.DeleteAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("bulk-delete")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete([FromForm] List<int> ids, CancellationToken ct)
    {
        if (ids.Count > 0)
            await _orderService.BulkDeleteAsync(ids, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/items/add")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(
        int id, int productId, int bundleCount, int unitsPerBundle, CancellationToken ct)
    {
        await _orderService.AddItemAsync(id, productId, bundleCount, unitsPerBundle, ct);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/items/{itemId:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditItem(
        int id, int itemId, int bundleCount, int unitsPerBundle, CancellationToken ct)
    {
        await _orderService.UpdateItemQuantityAsync(itemId, bundleCount, unitsPerBundle, ct);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/items/{itemId:int}/delete")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, int itemId, CancellationToken ct)
    {
        await _orderService.DeleteItemAsync(itemId, ct);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/items/{itemId:int}/receive-shelf")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceiveToShelf(
        int id, int itemId, string shelfCode, int position,
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

        await _orderService.ReceiveItemToShelfAsync(itemId, shelf.Id, position, ct);
        TempData["SuccessMessage"] = $"تم تعيين الصنف للرف {shelfCode}";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/items/{itemId:int}/receive-areaz")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceiveToAreaZ(int id, int itemId, CancellationToken ct)
    {
        await _orderService.ReceiveItemToAreaZAsync(itemId, ct);
        return RedirectToAction(nameof(Detail), new { id });
    }
}
