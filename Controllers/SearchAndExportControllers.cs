using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;

namespace ManolyWarehouse.Controllers;

[Authorize(Policy = "AuthenticatedUser")]
public class SearchController : Controller
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet("/search")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return View(new SearchResultsViewModel { Query = string.Empty });

        var results = await _searchService.SearchAsync(q, ct);
        return View(results);
    }
}

[Authorize(Policy = "AdminOnly")]
public class ExportController : Controller
{
    private readonly IInventoryPdfExporter _exporter;

    public ExportController(IInventoryPdfExporter exporter)
    {
        _exporter = exporter;
    }

    [HttpGet("/export/inventory")]
    public async Task<IActionResult> Inventory(CancellationToken ct)
    {
        var pdf = await _exporter.GenerateInventorySnapshotAsync(ct);
        var filename = $"inventory-snapshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
        return File(pdf, "application/pdf", filename);
    }
}
