using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<ShelfInventory> ShelfInventory => Set<ShelfInventory>();
    public DbSet<InventoryAdjustmentLog> InventoryAdjustmentLogs => Set<InventoryAdjustmentLog>();
    public DbSet<AreaZInventory> AreaZInventory => Set<AreaZInventory>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all IEntityTypeConfiguration<T> from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Rename Identity tables to friendlier names (no "AspNet" prefix)
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims");

        // AREAZ-01: PostgreSQL partial unique index — one active row per product
        builder.Entity<AreaZInventory>()
            .HasIndex(az => az.ProductId)
            .HasFilter("\"IsDispatched\" = false")
            .IsUnique()
            .HasDatabaseName("IX_AreaZInventory_ProductId_Active");

        SeedShelves(builder);
    }

    /// <summary>
    /// Seed all 414 shelf locations:
    /// Side ABC: A1..A69, B1..B69, C1..C69 → 207 shelves
    /// Side DEF: D1..D69, E1..E69, F1..F69 → 207 shelves
    /// </summary>
    private static void SeedShelves(ModelBuilder builder)
    {
        var shelves = new List<object>();
        int id = 1;

        foreach (var (sideLabels, side) in new (char[], WarehouseSide)[]
        {
            (new[] { 'A', 'B', 'C' }, WarehouseSide.ABC),
            (new[] { 'D', 'E', 'F' }, WarehouseSide.DEF)
        })
        {
            foreach (var number in Enumerable.Range(1, 69))
            {
                foreach (var label in sideLabels)
                {
                    shelves.Add(new
                    {
                        Id = id++,
                        Code = $"{label}{number}",
                        Label = label.ToString(),
                        Number = number,
                        Side = side
                    });
                }
            }
        }

        builder.Entity<Shelf>().HasData(shelves);
    }
}
