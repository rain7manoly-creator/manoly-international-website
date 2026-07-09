using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(c => c.Name).IsUnique();

        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(450);
        builder.Property(c => c.UpdatedBy).HasMaxLength(450);

        builder.HasMany(c => c.Products)
            .WithOne(p => p.Category)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Products)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(p => new { p.CategoryId, p.Name }).IsUnique();

        builder.Property(p => p.CreatedBy).IsRequired().HasMaxLength(450);
        builder.Property(p => p.UpdatedBy).HasMaxLength(450);

        builder.HasMany(p => p.ShelfInventories)
            .WithOne(si => si.Product)
            .HasForeignKey(si => si.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.AreaZInventories)
            .WithOne(az => az.Product)
            .HasForeignKey(az => az.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.PurchaseOrderItems)
            .WithOne(poi => poi.Product)
            .HasForeignKey(poi => poi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(p => p.ShelfInventories).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.AreaZInventories).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.PurchaseOrderItems).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ShelfConfiguration : IEntityTypeConfiguration<Shelf>
{
    public void Configure(EntityTypeBuilder<Shelf> builder)
    {
        builder.ToTable("Shelves");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).IsRequired().HasMaxLength(10);
        builder.HasIndex(s => s.Code).IsUnique();

        builder.Property(s => s.Label)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(1);

        builder.Property(s => s.Number).IsRequired();

        builder.Property(s => s.MaxPositions)
            .IsRequired()
            .HasDefaultValue(Shelf.MaxSlots);

        builder.Property(s => s.Side)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.HasMany(s => s.Inventories)
            .WithOne(si => si.Shelf)
            .HasForeignKey(si => si.ShelfId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(s => s.Inventories).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => new { s.Side, s.Number });
    }
}

public class ShelfInventoryConfiguration : IEntityTypeConfiguration<ShelfInventory>
{
    public void Configure(EntityTypeBuilder<ShelfInventory> builder)
    {
        builder.ToTable("ShelfInventory");
        builder.HasKey(si => si.Id);

        // SHELF-02: Position unique per shelf
        builder.HasIndex(si => new { si.ShelfId, si.Position })
            .IsUnique()
            .HasDatabaseName("IX_ShelfInventory_ShelfId_Position");

        builder.Property(si => si.Position).IsRequired();
        builder.Property(si => si.BundleCount).IsRequired();
        builder.Property(si => si.UnitsPerBundle).IsRequired();

        builder.Property(si => si.CreatedBy).IsRequired().HasMaxLength(450);
        builder.Property(si => si.UpdatedBy).HasMaxLength(450);

        builder.HasOne(si => si.OrderItem)
            .WithMany()
            .HasForeignKey(si => si.OrderItemId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(si => si.AdjustmentLogs)
            .WithOne(log => log.ShelfInventory)
            .HasForeignKey(log => log.ShelfInventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(si => si.AdjustmentLogs).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(si => si.ProductId).HasDatabaseName("IX_ShelfInventory_ProductId");

        builder.Ignore(si => si.TotalQuantity);
    }
}

public class InventoryAdjustmentLogConfiguration : IEntityTypeConfiguration<InventoryAdjustmentLog>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustmentLog> builder)
    {
        builder.ToTable("InventoryAdjustmentLogs");
        builder.HasKey(log => log.Id);

        builder.Property(log => log.AdjustedBy).IsRequired().HasMaxLength(450);
        builder.Property(log => log.AdjustedAt).IsRequired();
        builder.Property(log => log.Notes).HasMaxLength(300);

        builder.HasIndex(log => log.ShelfInventoryId).HasDatabaseName("IX_AdjustmentLog_ShelfInventoryId");
        builder.HasIndex(log => log.AdjustedAt).HasDatabaseName("IX_AdjustmentLog_AdjustedAt");
    }
}

public class AreaZInventoryConfiguration : IEntityTypeConfiguration<AreaZInventory>
{
    public void Configure(EntityTypeBuilder<AreaZInventory> builder)
    {
        builder.ToTable("AreaZInventory");
        builder.HasKey(az => az.Id);

        builder.Property(az => az.BundleCount).IsRequired();
        builder.Property(az => az.UnitsPerBundle).IsRequired();
        builder.Property(az => az.Notes).HasMaxLength(500);
        builder.Property(az => az.IsDispatched).IsRequired();
        builder.Property(az => az.DispatchedBy).HasMaxLength(450);

        builder.Property(az => az.CreatedBy).IsRequired().HasMaxLength(450);
        builder.Property(az => az.UpdatedBy).HasMaxLength(450);

        builder.HasIndex(az => az.ProductId).HasDatabaseName("IX_AreaZInventory_ProductId");

        // AREAZ-01 partial unique index applied in DbContext (PostgreSQL HasFilter)

        builder.Ignore(az => az.TotalQuantity);
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");
        builder.HasKey(po => po.Id);

        builder.Property(po => po.Supplier).IsRequired().HasMaxLength(200);

        builder.Property(po => po.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(po => po.OrderedAt).IsRequired();
        builder.Property(po => po.Notes).HasMaxLength(500);

        builder.Property(po => po.CreatedBy).IsRequired().HasMaxLength(450);
        builder.Property(po => po.UpdatedBy).HasMaxLength(450);
        builder.Property(po => po.LastUpdatedBy).HasMaxLength(450);

        builder.HasMany(po => po.Items)
            .WithOne(item => item.Order)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(po => po.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(po => po.Status).HasDatabaseName("IX_PurchaseOrders_Status");
        builder.HasIndex(po => po.OrderedAt).HasDatabaseName("IX_PurchaseOrders_OrderedAt");

        builder.Ignore(po => po.IsTerminal);
        builder.Ignore(po => po.HasItems);
        builder.Ignore(po => po.NextStatus);
        builder.Ignore(po => po.CanAdvance);
    }
}

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.BundleCount).IsRequired();
        builder.Property(item => item.UnitsPerBundle).IsRequired();
        builder.Property(item => item.GoesToAreaZ).IsRequired();
        builder.Property(item => item.ShelvedBy).HasMaxLength(450);

        builder.Property(item => item.CreatedBy).IsRequired().HasMaxLength(450);
        builder.Property(item => item.UpdatedBy).HasMaxLength(450);

        builder.HasOne(item => item.ReceivedShelf)
            .WithMany()
            .HasForeignKey(item => item.ReceivedShelfId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(item => item.OrderId).HasDatabaseName("IX_PurchaseOrderItems_OrderId");
        builder.HasIndex(item => item.ProductId).HasDatabaseName("IX_PurchaseOrderItems_ProductId");

        builder.Ignore(item => item.TotalQuantity);
        builder.Ignore(item => item.IsShelved);
        builder.Ignore(item => item.IsPendingShelving);
    }
}

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Area)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Action).IsRequired().HasMaxLength(500);
        builder.Property(a => a.PerformedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.PerformedByName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.PerformedByRole).IsRequired().HasMaxLength(20);
        builder.Property(a => a.CreatedAt).IsRequired();

        // Newest-first listing + area filter
        builder.HasIndex(a => a.CreatedAt).HasDatabaseName("IX_ActivityLogs_CreatedAt");
        builder.HasIndex(a => a.Area).HasDatabaseName("IX_ActivityLogs_Area");
    }
}

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.IsAdmin).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasIndex(u => u.IsActive).HasDatabaseName("IX_Users_IsActive");
    }
}
