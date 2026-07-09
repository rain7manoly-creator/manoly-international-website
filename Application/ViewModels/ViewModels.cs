using System.ComponentModel.DataAnnotations;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Application.ViewModels;

// ─── Pagination ────────────────────────────────────────────────────────

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class PaginationViewModel
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;
    public string BaseUrl { get; init; } = "";
    public static PaginationViewModel From<T>(PagedResult<T> result, string baseUrl) =>
        new() { Page = result.Page, TotalPages = result.TotalPages, BaseUrl = baseUrl };
}

// ─── Auth ──────────────────────────────────────────────────────────────

public class LoginViewModel
{
    [Required(ErrorMessage = "اسم المستخدم مطلوب")]
    public string UserName { get; set; } = default!;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = default!;

    public string? ReturnUrl { get; set; }
}

// ─── Warehouse Grid ────────────────────────────────────────────────────

public class WarehouseGridViewModel
{
    public IReadOnlyList<ShelfGridCell> SideAbc { get; init; } = Array.Empty<ShelfGridCell>();
    public IReadOnlyList<ShelfGridCell> SideDef { get; init; } = Array.Empty<ShelfGridCell>();
    public int TotalLocations { get; init; }
    public int OccupiedLocations { get; init; }
    public int FullLocations { get; init; }
}

public class ShelfGridCell
{
    public int ShelfId { get; init; }
    public string Code { get; init; } = default!;
    public string Label { get; init; } = default!;
    public int Number { get; init; }
    public int OccupiedSlots { get; init; }
    public int MaxPositions { get; init; } = Shelf.MaxSlots;
    public bool IsFull => OccupiedSlots >= MaxPositions;
    public bool IsEmpty => OccupiedSlots == 0;
}

// ─── Shelf Detail ──────────────────────────────────────────────────────

public class ShelfDetailViewModel
{
    public int ShelfId { get; init; }
    public string Code { get; init; } = default!;
    public string Label { get; init; } = default!;
    public int Number { get; init; }
    public IReadOnlyList<ShelfSlotViewModel> Slots { get; init; } = Array.Empty<ShelfSlotViewModel>();
    public int OccupiedCount { get; init; }
    public int MaxPositions { get; init; } = Shelf.MaxSlots;
    public int Capacity => MaxPositions;
}

public class ShelfSlotViewModel
{
    public int Position { get; init; }
    public int? InventoryId { get; init; }
    public int? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? CategoryName { get; init; }
    public int BundleCount { get; init; }
    public int UnitsPerBundle { get; init; }
    public int TotalQuantity => BundleCount * UnitsPerBundle;
    public bool IsEmpty => InventoryId == null;
}

public class AddShelfInventoryRequest
{
    public string ShelfCode { get; set; } = default!;

    [Range(1, int.MaxValue, ErrorMessage = "اختر المنتج")]
    public int ProductId { get; set; }

    // Upper bound is per-shelf (Shelf.MaxPositions) and enforced in ShelfService.
    [Range(1, 99, ErrorMessage = "رقم الموضع غير صالح")]
    public int Position { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "عدد الربطات يجب أن يكون 1 أو أكثر")]
    public int BundleCount { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "عدد الوحدات في الربطة يجب أن يكون 1 أو أكثر")]
    public int UnitsPerBundle { get; set; }
}

// ─── Products & Categories ─────────────────────────────────────────────

public class ProductListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public string CategoryName { get; init; } = default!;
    public int ShelfLocationCount { get; init; }
    public bool IsInAreaZ { get; init; }
}

public class ProductDetailViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = default!;
    public IReadOnlyList<ProductShelfLocationViewModel> ShelfLocations { get; init; } = Array.Empty<ProductShelfLocationViewModel>();
    public AreaZInventoryViewModel? AreaZ { get; init; }
}

public class ProductShelfLocationViewModel
{
    public int InventoryId { get; init; }
    public string ShelfCode { get; init; } = default!;
    public int Position { get; init; }
    public int BundleCount { get; init; }
    public int UnitsPerBundle { get; init; }
    public int TotalQuantity => BundleCount * UnitsPerBundle;
}

public class AreaZInventoryViewModel
{
    public int Id { get; init; }
    public int BundleCount { get; init; }
    public int UnitsPerBundle { get; init; }
    public int TotalQuantity => BundleCount * UnitsPerBundle;
    public string? Notes { get; init; }
    public DateTime EnteredAt { get; init; }
}

public class ProductDeletionImpact
{
    public int ShelfInventoryCount { get; init; }
    public int AreaZCount { get; init; }
    public int OrderItemsCount { get; init; }
    public bool HasImpact => ShelfInventoryCount > 0 || AreaZCount > 0 || OrderItemsCount > 0;
}

public class CategoryViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public int ProductCount { get; init; }
}

// ─── Orders ────────────────────────────────────────────────────────────

public class OrderListItemViewModel
{
    public int Id { get; init; }
    public string Supplier { get; init; } = default!;
    public OrderStatus Status { get; init; }
    public DateTime OrderedAt { get; init; }
    public DateOnly? ExpectedArrivalDate { get; init; }
    public int ItemCount { get; init; }
    public int PendingShelvingCount { get; init; }
}

public class OrderDetailViewModel
{
    public int Id { get; init; }
    public string Supplier { get; init; } = default!;
    public OrderStatus Status { get; init; }
    public OrderStatus? NextStatus { get; init; }
    public bool CanAdvance { get; init; }
    public bool IsTerminal { get; init; }
    public DateTime OrderedAt { get; init; }
    public DateOnly? ExpectedArrivalDate { get; init; }
    public string? Notes { get; init; }
    public string CreatedBy { get; init; } = default!;
    public DateTime? LastUpdatedAt { get; init; }
    public string? LastUpdatedBy { get; init; }
    public IReadOnlyList<OrderItemViewModel> Items { get; init; } = Array.Empty<OrderItemViewModel>();
}

public class OrderItemViewModel
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public int BundleCount { get; init; }
    public int UnitsPerBundle { get; init; }
    public int TotalQuantity => BundleCount * UnitsPerBundle;
    public string? ReceivedShelfCode { get; init; }
    public int? Position { get; init; }
    public bool GoesToAreaZ { get; init; }
    public bool IsShelved { get; init; }
}

public class CreateOrderRequest
{
    [Required(ErrorMessage = "اسم المورد مطلوب")]
    [StringLength(200, MinimumLength = 2)]
    public string Supplier { get; set; } = default!;

    public DateOnly? ExpectedArrivalDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

// ─── Area Z ────────────────────────────────────────────────────────────

public class AreaZItemViewModel
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public string CategoryName { get; init; } = default!;
    public int BundleCount { get; init; }
    public int UnitsPerBundle { get; init; }
    public int TotalQuantity => BundleCount * UnitsPerBundle;
    public string? Notes { get; init; }
    public DateTime EnteredAt { get; init; }
    public string EnteredBy { get; init; } = default!;
}

// ─── Activity Log ──────────────────────────────────────────────────────

public class ActivityLogViewModel
{
    public int Id { get; init; }
    public ActivityArea Area { get; init; }
    public string Action { get; init; } = default!;
    public string PerformedByName { get; init; } = default!;
    public string PerformedByRole { get; init; } = default!;
    public DateTime CreatedAt { get; init; }

    public bool IsAdmin => PerformedByRole == "Admin";

    /// <summary>Arabic label for the area, for filter pills and rows.</summary>
    public string AreaLabel => Area switch
    {
        ActivityArea.Shelves => "الرفوف",
        ActivityArea.AreaZ   => "منطقة Z",
        ActivityArea.Orders  => "الطلبيات",
        _ => Area.ToString()
    };

    /// <summary>Arabic label for the performer's role.</summary>
    public string RoleLabel => IsAdmin ? "مسؤول" : "موظف";
}

// ─── Users ─────────────────────────────────────────────────────────────

public class UserViewModel
{
    public string Id { get; init; } = default!;
    public string UserName { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public bool IsAdmin { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class CreateUserRequest
{
    [Required(ErrorMessage = "اسم المستخدم مطلوب")]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression("^[a-zA-Z0-9_]+$", ErrorMessage = "أحرف لاتينية وأرقام وشرطة سفلية فقط")]
    public string UserName { get; set; } = default!;

    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = default!;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = default!;

    public bool IsAdmin { get; set; }
}

// ─── Search ────────────────────────────────────────────────────────────

public class SearchResultsViewModel
{
    public string Query { get; init; } = default!;
    public IReadOnlyList<ProductSearchResult> Products { get; init; } = Array.Empty<ProductSearchResult>();
    public IReadOnlyList<ShelfSearchResult> Shelves { get; init; } = Array.Empty<ShelfSearchResult>();
    public IReadOnlyList<SupplierSearchResult> Suppliers { get; init; } = Array.Empty<SupplierSearchResult>();
    public bool HasResults =>
        Products.Count > 0 || Shelves.Count > 0 || Suppliers.Count > 0;
}

public class ProductSearchResult
{
    public int ProductId { get; init; }
    public string Name { get; init; } = default!;
    public string CategoryName { get; init; } = default!;
    public int LocationCount { get; init; }
    public bool IsInAreaZ { get; init; }
}

public class ShelfSearchResult
{
    public int ShelfId { get; init; }
    public string Code { get; init; } = default!;
    public int OccupiedSlots { get; init; }
}

public class SupplierSearchResult
{
    public string Supplier { get; init; } = default!;
    public int OrderCount { get; init; }
}
