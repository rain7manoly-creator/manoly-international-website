using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ManolyWarehouse.Application.ViewModels;
using Xunit;

namespace ManolyWarehouse.Tests.ViewModels;

/// <summary>
/// Tests DataAnnotations validation on all ViewModels — catches regressions
/// where validation attributes are accidentally removed.
/// </summary>
public class ViewModelValidationTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var ctx = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }

    private static bool IsValid(object model) => !Validate(model).Any();

    // ────────────────────────────────────────────────────────────────────────
    // LoginViewModel
    // ────────────────────────────────────────────────────────────────────────

    [Fact] public void LoginViewModel_MissingUserName_IsInvalid()
        => IsValid(new LoginViewModel { Password = "Pass123!" }).Should().BeFalse();

    [Fact] public void LoginViewModel_MissingPassword_IsInvalid()
        => IsValid(new LoginViewModel { UserName = "admin" }).Should().BeFalse();

    [Fact] public void LoginViewModel_BothFields_IsValid()
        => IsValid(new LoginViewModel { UserName = "admin", Password = "Pass123!" }).Should().BeTrue();

    // ────────────────────────────────────────────────────────────────────────
    // AddShelfInventoryRequest — SHELF-02, SHELF-03 guards
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]   // below min
    [InlineData(7)]   // above max (6 slots)
    public void AddShelfInventoryRequest_InvalidPosition_IsInvalid(int position)
    {
        var req = new AddShelfInventoryRequest
        {
            ShelfCode = "A01", ProductId = 1, Position = position,
            BundleCount = 1, UnitsPerBundle = 1
        };
        IsValid(req).Should().BeFalse();
    }

    [Fact]
    public void AddShelfInventoryRequest_ValidPositionRange_IsValid()
    {
        for (int pos = 1; pos <= 6; pos++)
        {
            var req = new AddShelfInventoryRequest
            {
                ShelfCode = "A01", ProductId = 1, Position = pos,
                BundleCount = 1, UnitsPerBundle = 1
            };
            IsValid(req).Should().BeTrue($"position {pos} is within valid range 1-6");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddShelfInventoryRequest_ZeroBundleCount_IsInvalid(int count)
    {
        var req = new AddShelfInventoryRequest
        {
            ShelfCode = "A01", ProductId = 1, Position = 1,
            BundleCount = count, UnitsPerBundle = 1
        };
        IsValid(req).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddShelfInventoryRequest_ZeroUnitsPerBundle_IsInvalid(int units)
    {
        var req = new AddShelfInventoryRequest
        {
            ShelfCode = "A01", ProductId = 1, Position = 1,
            BundleCount = 1, UnitsPerBundle = units
        };
        IsValid(req).Should().BeFalse();
    }

    [Fact]
    public void AddShelfInventoryRequest_MissingProductId_IsInvalid()
    {
        var req = new AddShelfInventoryRequest
        {
            ShelfCode = "A01", Position = 1, BundleCount = 1, UnitsPerBundle = 1
            // ProductId = 0 (default) — no product selected
        };
        IsValid(req).Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // CreateOrderRequest — ORD-05
    // ────────────────────────────────────────────────────────────────────────

    [Fact] public void CreateOrderRequest_EmptySupplier_IsInvalid()
        => IsValid(new CreateOrderRequest { Supplier = "" }).Should().BeFalse();

    [Fact] public void CreateOrderRequest_TooShortSupplier_IsInvalid()
        => IsValid(new CreateOrderRequest { Supplier = "A" }).Should().BeFalse();

    [Fact] public void CreateOrderRequest_ValidSupplier_IsValid()
        => IsValid(new CreateOrderRequest { Supplier = "Al-Khaleej Wood" }).Should().BeTrue();

    [Fact] public void CreateOrderRequest_NotesTooLong_IsInvalid()
    {
        var req = new CreateOrderRequest
        {
            Supplier = "Valid Supplier",
            Notes = new string('أ', 501) // exceeds 500 char limit
        };
        IsValid(req).Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // CreateUserRequest — AUTH-03, AUTH-04, AUTH-06
    // ────────────────────────────────────────────────────────────────────────

    [Fact] public void CreateUserRequest_MissingUserName_IsInvalid()
        => IsValid(new CreateUserRequest { FullName = "Test", Password = "Pass123!" }).Should().BeFalse();

    [Fact] public void CreateUserRequest_TooShortUserName_IsInvalid()
        => IsValid(new CreateUserRequest { UserName = "ab", FullName = "Test", Password = "Pass123!" }).Should().BeFalse();

    [Fact] public void CreateUserRequest_TooLongUserName_IsInvalid()
        => IsValid(new CreateUserRequest
            { UserName = new string('a', 51), FullName = "Test", Password = "Pass123!" }).Should().BeFalse();

    [Fact] public void CreateUserRequest_InvalidCharsInUserName_IsInvalid()
    {
        // Arabic chars not allowed — AUTH-03: Latin alphanumeric + underscore only
        var req = new CreateUserRequest
            { UserName = "أحمد", FullName = "Test", Password = "Pass123!" };
        IsValid(req).Should().BeFalse();
    }

    [Fact] public void CreateUserRequest_ValidUserName_IsValid()
        => IsValid(new CreateUserRequest
            { UserName = "worker_1", FullName = "Test User", Password = "Pass123456!" }).Should().BeTrue();

    [Fact] public void CreateUserRequest_PasswordTooShort_IsInvalid()
        => IsValid(new CreateUserRequest
            { UserName = "user1", FullName = "Test", Password = "Pass1" }).Should().BeFalse();

    [Fact] public void CreateUserRequest_MissingFullName_IsInvalid()
        => IsValid(new CreateUserRequest { UserName = "user1", Password = "Pass123!" }).Should().BeFalse();

    [Fact] public void CreateUserRequest_FullNameTooShort_IsInvalid()
        => IsValid(new CreateUserRequest { UserName = "user1", FullName = "A", Password = "Pass123!" }).Should().BeFalse();

    // ────────────────────────────────────────────────────────────────────────
    // Computed properties — no DB storage (SHELF-09, AREAZ-07)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShelfSlotViewModel_TotalQuantity_IsComputedCorrectly()
    {
        var slot = new ShelfSlotViewModel { BundleCount = 5, UnitsPerBundle = 12 };
        slot.TotalQuantity.Should().Be(60);
    }

    [Fact]
    public void OrderItemViewModel_TotalQuantity_IsComputedCorrectly()
    {
        var item = new OrderItemViewModel { BundleCount = 3, UnitsPerBundle = 100 };
        item.TotalQuantity.Should().Be(300);
    }

    [Fact]
    public void AreaZItemViewModel_TotalQuantity_IsComputedCorrectly()
    {
        var item = new AreaZItemViewModel { BundleCount = 10, UnitsPerBundle = 50 };
        item.TotalQuantity.Should().Be(500);
    }

    [Fact]
    public void AreaZInventoryViewModel_TotalQuantity_IsComputedCorrectly()
    {
        var vm = new AreaZInventoryViewModel { BundleCount = 7, UnitsPerBundle = 25 };
        vm.TotalQuantity.Should().Be(175);
    }

    // ────────────────────────────────────────────────────────────────────────
    // ShelfGridCell — IsFull / IsEmpty logic
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShelfGridCell_IsEmpty_WhenOccupiedSlotsIsZero()
    {
        var cell = new ShelfGridCell { OccupiedSlots = 0 };
        cell.IsEmpty.Should().BeTrue();
        cell.IsFull.Should().BeFalse();
    }

    [Fact]
    public void ShelfGridCell_IsFull_WhenOccupiedSlotsReachesMax()
    {
        var cell = new ShelfGridCell { OccupiedSlots = 6 }; // Shelf.MaxSlots
        cell.IsFull.Should().BeTrue();
        cell.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ShelfGridCell_NeitherFullNorEmpty_WhenPartiallyOccupied()
    {
        var cell = new ShelfGridCell { OccupiedSlots = 3 };
        cell.IsEmpty.Should().BeFalse();
        cell.IsFull.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // ProductDeletionImpact — HasImpact logic (PROD-03)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ProductDeletionImpact_HasImpact_WhenShelfInventoryExists()
        => new ProductDeletionImpact { ShelfInventoryCount = 2 }.HasImpact.Should().BeTrue();

    [Fact]
    public void ProductDeletionImpact_HasImpact_WhenAreaZExists()
        => new ProductDeletionImpact { AreaZCount = 1 }.HasImpact.Should().BeTrue();

    [Fact]
    public void ProductDeletionImpact_HasImpact_WhenOrderItemsExist()
        => new ProductDeletionImpact { OrderItemsCount = 5 }.HasImpact.Should().BeTrue();

    [Fact]
    public void ProductDeletionImpact_NoImpact_WhenAllZero()
        => new ProductDeletionImpact().HasImpact.Should().BeFalse();

    // ────────────────────────────────────────────────────────────────────────
    // SearchResultsViewModel — HasResults logic
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SearchResultsViewModel_HasResults_WhenProductsExist()
    {
        var vm = new SearchResultsViewModel
        {
            Query = "test",
            Products = new List<ProductSearchResult> { new() }
        };
        vm.HasResults.Should().BeTrue();
    }

    [Fact]
    public void SearchResultsViewModel_NoResults_WhenAllEmpty()
    {
        var vm = new SearchResultsViewModel { Query = "nothing" };
        vm.HasResults.Should().BeFalse();
    }
}
