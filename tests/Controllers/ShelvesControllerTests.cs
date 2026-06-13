using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Controllers;
using Xunit;

namespace ManolyWarehouse.Tests.Controllers;

public class ShelvesControllerTests
{
    private readonly Mock<IShelfService> _shelfSvc = new();
    private readonly Mock<IWarehouseGridService> _gridSvc = new();
    private readonly Mock<IProductService> _productSvc = new();
    private readonly ShelvesController _sut;

    public ShelvesControllerTests()
    {
        _sut = new ShelvesController(_shelfSvc.Object, _gridSvc.Object, _productSvc.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    // ────────────────────────────────────────────────────────────────────────
    // Detail — valid shelf returns 200
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_ValidCode_ReturnsViewWithModel()
    {
        var model = new ShelfDetailViewModel
        {
            ShelfId = 1, Code = "A01", Label = "A", Number = 1,
            OccupiedCount = 2,
            Slots = new List<ShelfSlotViewModel>()
        };
        _shelfSvc.Setup(s => s.GetByCodeAsync("A01", default)).ReturnsAsync(model);

        var result = await _sut.Detail("A01", default) as ViewResult;

        result.Should().NotBeNull();
        result!.Model.Should().Be(model);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Detail — unknown code returns 404
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_UnknownCode_Returns404()
    {
        _shelfSvc.Setup(s => s.GetByCodeAsync("Z99", default)).ReturnsAsync((ShelfDetailViewModel?)null);

        var result = await _sut.Detail("Z99", default);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Add POST — valid request redirects to Detail
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_POST_ValidRequest_RedirectsToDetail()
    {
        _shelfSvc.Setup(s => s.AddInventoryAsync(It.IsAny<AddShelfInventoryRequest>(), default))
                 .ReturnsAsync(1);

        var request = new AddShelfInventoryRequest
        {
            ShelfCode = "B05", ProductId = 10, Position = 3,
            BundleCount = 5, UnitsPerBundle = 12
        };

        var result = await _sut.Add("B05", request, default) as RedirectToActionResult;

        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Detail");
        result.RouteValues!["code"].Should().Be("B05");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Add POST — invalid ModelState returns view, never calls service
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_POST_InvalidModel_ReturnsViewWithoutCallingService()
    {
        _sut.ModelState.AddModelError("ProductId", "required");
        var request = new AddShelfInventoryRequest { ShelfCode = "A01" };

        var result = await _sut.Add("A01", request, default);

        result.Should().BeOfType<ViewResult>();
        _shelfSvc.Verify(s => s.AddInventoryAsync(It.IsAny<AddShelfInventoryRequest>(), default), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Adjust (edit qty) — calls service and redirects
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Adjust_ValidInput_CallsServiceAndRedirects()
    {
        _shelfSvc.Setup(s => s.AdjustInventoryAsync(7, 3, 10, null, default))
                 .Returns(Task.CompletedTask);

        var result = await _sut.Adjust("A01", 7, 3, 10, null, default) as RedirectToActionResult;

        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Detail");
        _shelfSvc.Verify(s => s.AdjustInventoryAsync(7, 3, 10, null, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Delete (Admin only) — calls service and redirects
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ValidId_CallsServiceAndRedirects()
    {
        _shelfSvc.Setup(s => s.DeleteInventoryAsync(42, default)).Returns(Task.CompletedTask);

        var result = await _sut.Delete("C10", 42, default) as RedirectToActionResult;

        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Detail");
        _shelfSvc.Verify(s => s.DeleteInventoryAsync(42, default), Times.Once);
    }
}
