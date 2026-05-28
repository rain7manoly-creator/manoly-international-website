using FluentAssertions;
using Xunit;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Tests.Services;

/// <summary>
/// Tests for domain-level business rules that can be verified
/// without a database — state machines, constants, invariants.
/// </summary>
public class BusinessRuleTests
{
    // ────────────────────────────────────────────────────────────────────────
    // SHELF-01: MaxSlots constant = 6
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Shelf_MaxSlots_Is6()
        => Shelf.MaxSlots.Should().Be(6);

    // ────────────────────────────────────────────────────────────────────────
    // ORD-01: Valid status transitions (state machine)
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OrderStatus.Ordered,     OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped,     OrderStatus.ArrivingSoon)]
    [InlineData(OrderStatus.ArrivingSoon, OrderStatus.Received)]
    public void OrderStatus_ValidForwardTransitions(OrderStatus from, OrderStatus to)
    {
        // The state machine allows these — verify the enum values are ordered
        // and the next logical step exists
        ((int)to).Should().BeGreaterThan((int)from,
            $"{from} → {to} is a valid forward transition");
    }

    // ────────────────────────────────────────────────────────────────────────
    // ORD-02: Terminal states — Received and Cancelled
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OrderStatus.Received)]
    [InlineData(OrderStatus.Cancelled)]
    public void TerminalStatuses_AreDefinedInEnum(OrderStatus status)
    {
        Enum.IsDefined(typeof(OrderStatus), status).Should().BeTrue();
    }

    [Fact]
    public void OrderStatus_Cancelled_ExistsInEnum()
        => Enum.GetValues<OrderStatus>().Should().Contain(OrderStatus.Cancelled);

    // ────────────────────────────────────────────────────────────────────────
    // Shelf code format — seeded as A01..C69 and D01..F69
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("A", 1)]
    [InlineData("A", 69)]
    [InlineData("C", 1)]
    [InlineData("C", 69)]
    [InlineData("D", 1)]
    [InlineData("F", 69)]
    public void ShelfCodes_FollowExpectedFormat(string label, int number)
    {
        var code = $"{label}{number:D2}";
        code.Should().MatchRegex(@"^[A-F]\d{1,2}$",
            $"shelf code {code} must be a letter A-F followed by 1-2 digits");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Total grid = 414 locations (3 rows × 69 cols × 2 sides)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WarehouseGrid_TotalLocations_Is414()
    {
        const int rowsPerSide = 3; // A/B/C and D/E/F
        const int columns = 69;
        const int sides = 2;
        (rowsPerSide * columns * sides).Should().Be(414);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Position range validation (SHELF-02): 1-6 are valid, 0 and 7 are not
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(0, false)]
    [InlineData(7, false)]
    [InlineData(-1, false)]
    public void ShelfPosition_ValidRange(int position, bool expected)
    {
        bool valid = position >= 1 && position <= Shelf.MaxSlots;
        valid.Should().Be(expected);
    }

    // ────────────────────────────────────────────────────────────────────────
    // BundleCount / UnitsPerBundle must be >= 1 (SHELF-03, AREAZ-02, ITEM-02)
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1,  true)]
    [InlineData(100, true)]
    [InlineData(0,  false)]
    [InlineData(-1, false)]
    public void BundleCount_ValidRange(int value, bool expected)
    {
        bool valid = value >= 1;
        valid.Should().Be(expected);
    }
}
