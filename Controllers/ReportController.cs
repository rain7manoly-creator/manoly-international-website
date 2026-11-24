using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Services;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Extensions;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Controllers;

public record ReportLine(int Id, string Name, string Group, string Unit, decimal Before, decimal After, int Moves, bool Warn)
{
    public decimal Net => After - Before;
}
public record ReportEvent(DateTime At, string Who, string Area, string Text);

public class DayReport
{
    public DateOnly Day { get; init; }
    public bool IsToday { get; init; }
    public List<ReportLine> MainLines { get; init; } = new();
    public List<ReportEvent> MainEvents { get; init; } = new();
    public List<ReportLine> WoodLines { get; init; } = new();
    public List<ReportEvent> WoodEvents { get; init; } = new();
    public int MainOutNow { get; init; }
    public int MainLowNow { get; init; }
    public int WoodLowNow { get; init; }
}

/// <summary>«تقرير اليوم»: the day's moves in the main warehouse and the wood warehouse, and the warnings that matter.</summary>
[Authorize(Policy = "AuthenticatedUser")]
[Route("report")]
public class ReportController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStockService _stock;
    private readonly IWoodService _wood;

    public ReportController(AppDbContext db, IStockService stock, IWoodService wood) { _db = db; _stock = stock; _wood = wood; }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? d, CancellationToken ct)
    {
        var today = LocalTimeExtensions.LocalToday();
        var day = DateOnly.TryParse(d, out var parsed) && parsed <= today ? parsed : today;
        var (dayFrom, dayTo) = LocalTimeExtensions.LocalDayUtc(day);

        // Main warehouse: stock lines that moved (by people, not automatic steps)
        var moves = await _db.StockMovements.AsNoTracking()
            .Where(m => m.CreatedAt >= dayFrom && m.CreatedAt < dayTo && m.UserId != null)
            .Select(m => new { m.StockItemId, m.TotalBefore, m.TotalAfter, m.CreatedAt, m.Id,
                               m.StockItem.Name, m.StockItem.Color, m.StockItem.Family, m.StockItem.AlertAt })
            .ToListAsync(ct);
        var mainLines = moves.GroupBy(m => m.StockItemId).Select(g =>
        {
            var first = g.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).First();
            var last = g.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Last();
            var name = first.Name + (first.Color == "white" ? " — أبيض" : first.Color == "grey" ? " — سكني" : "");
            var after = last.TotalAfter ?? 0;
            return new ReportLine(g.Key, name, StockService.FamilyNames.GetValueOrDefault(first.Family, first.Family),
                StockItem.CountsInCartons(first.Family) ? "كرتونة" : "حبة", first.TotalBefore ?? 0, after, g.Count(),
                first.AlertAt != -1 && after <= (first.AlertAt ?? 0));
        }).Where(l => l.Net != 0 || l.Warn).OrderBy(l => l.Net).ToList();

        var mainEvents = (await _db.ActivityLogs.AsNoTracking()
            .Where(a => a.CreatedAt >= dayFrom && a.CreatedAt < dayTo && a.Area != ActivityArea.Wood)
            .OrderByDescending(a => a.CreatedAt).Take(400).ToListAsync(ct))
            .Select(a => new ReportEvent(a.CreatedAt, a.PerformedByName, a.Area switch
            {
                ActivityArea.Shelves => "الرفوف", ActivityArea.AreaZ => "Area Z", ActivityArea.Orders => "الطلبيات", _ => "المخزون"
            }, a.Action)).ToList();

        // Wood warehouse
        var wmoves = await (from m in _db.WoodMovements.AsNoTracking()
                            join w in _db.WoodItems.AsNoTracking() on m.WoodItemId equals w.Id
                            where m.CreatedAt >= dayFrom && m.CreatedAt < dayTo && m.UserId != null
                            select new { m, w }).ToListAsync(ct);
        var woodLines = wmoves.GroupBy(x => x.w.Id).Select(g =>
        {
            var first = g.OrderBy(x => x.m.CreatedAt).ThenBy(x => x.m.Id).First();
            var last = g.OrderBy(x => x.m.CreatedAt).ThenBy(x => x.m.Id).Last();
            var w = first.w;
            return new ReportLine(w.Id, w.DisplayName, w.Type, w.Unit, first.m.Before, last.m.After, g.Count(),
                w.AlertAt != -1 && last.m.After <= (w.AlertAt ?? 0));
        }).Where(l => l.Net != 0 || l.Warn).OrderBy(l => l.Net).ToList();
        var woodEvents = wmoves.OrderByDescending(x => x.m.CreatedAt).Select(x =>
        {
            var delta = x.m.After - x.m.Before;
            return new ReportEvent(x.m.CreatedAt, x.m.UserName ?? "—", x.w.Type,
                $"«{x.w.DisplayName}»: من {x.m.Before:#,0.###} إلى {x.m.After:#,0.###} {x.w.Unit} ({(delta > 0 ? "+" : "")}{delta:#,0.###}) · {x.m.Reason}");
        }).ToList();

        var alerts = await _stock.GetAlertsAsync(ct);
        var model = new DayReport
        {
            Day = day, IsToday = day == today,
            MainLines = mainLines, MainEvents = mainEvents, WoodLines = woodLines, WoodEvents = woodEvents,
            MainOutNow = alerts.Count(a => a.IsOut && !a.AreaZ), MainLowNow = alerts.Count(a => !a.IsOut || a.AreaZ),
            WoodLowNow = await _wood.CountLowAsync(ct)
        };
        return View(model);
    }
}
