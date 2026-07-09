using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Interfaces;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("logs")]
public class LogsController : Controller
{
    private readonly IActivityLogService _activityLog;

    public LogsController(IActivityLogService activityLog)
    {
        _activityLog = activityLog;
    }

    private const int PageSize = 30;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? area = null, int page = 1, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var model = await _activityLog.ListAsync(area, page, PageSize, ct);
        ViewBag.ActiveArea = area;
        return View(model);
    }
}
