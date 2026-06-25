using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Infrastructure.Persistence;

namespace ManolyWarehouse.Application.Services;

public class WarehouseGridService : IWarehouseGridService
{
    private readonly AppDbContext _db;
    private readonly ILogger<WarehouseGridService> _logger;

    public WarehouseGridService(AppDbContext db, ILogger<WarehouseGridService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<WarehouseGridViewModel> GetGridAsync(CancellationToken ct = default)
    {
        // Single query: for each shelf, count its occupied slots
        // Left join via GroupBy on ShelfInventory keeps shelves with 0 inventory
        var shelfData = await _db.Shelves
            .AsNoTracking()
            .OrderBy(s => s.Side)
            .ThenBy(s => s.Number)
            .ThenBy(s => s.Label)
            .Select(s => new
            {
                s.Id,
                s.Code,
                s.Label,
                s.Number,
                s.Side,
                s.MaxPositions,
                OccupiedSlots = _db.ShelfInventory.Count(si => si.ShelfId == s.Id)
            })
            .ToListAsync(ct);

        var cells = shelfData.Select(s => new ShelfGridCell
        {
            ShelfId = s.Id,
            Code = s.Code,
            Label = s.Label,
            Number = s.Number,
            OccupiedSlots = s.OccupiedSlots,
            MaxPositions = s.MaxPositions
        }).ToList();

        var sideAbc = cells.Where(c => c.Label is "A" or "B" or "C").ToList();
        var sideDef = cells.Where(c => c.Label is "D" or "E" or "F" or "G" or "H").ToList();

        var totalLocations = cells.Count;
        var occupiedLocations = cells.Count(c => c.OccupiedSlots > 0);
        var fullLocations = cells.Count(c => c.IsFull);

        _logger.LogDebug(
            "Grid loaded: {Total} locations, {Occupied} occupied, {Full} full",
            totalLocations, occupiedLocations, fullLocations);

        return new WarehouseGridViewModel
        {
            SideAbc = sideAbc,
            SideDef = sideDef,
            TotalLocations = totalLocations,
            OccupiedLocations = occupiedLocations,
            FullLocations = fullLocations
        };
    }
}
