using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Controllers;
using ManolyWarehouse.Domain.Entities;
using Xunit;

namespace ManolyWarehouse.Tests.Controllers;

public class OrdersControllerTests
{
    private readonly Mock<IPurchaseOrderService> _orderSvc = new();
    private readonly OrdersController _sut;

    public OrdersControllerTests()
    {
        _sut = new OrdersController(_orderSvc.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    // ────────────────────────────────────────────────────────────────────────
    // Index — passes status filter to service
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("Ordered")]
    [InlineData("Received")]
    public async Task Index_PassesStatusFilterToService(string? status)
    {
        _orderSvc.Setup(s => s.ListAsync(status, default))
                 .ReturnsAsync(new List<OrderListItemViewModel>());

        await _sut.Index(status, default);

        _orderSvc.Verify(s => s.ListAsync(status, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Create POST — valid request creates order and redirects to Detail
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_POST_ValidRequest_RedirectsToDetail()
    {
        _orderSvc.Setup(s => s.CreateAsync(It.IsAny<CreateOrderRequest>(), default)).ReturnsAsync(99);

        var result = await _sut.Create(
            new CreateOrderRequest { Supplier = "Al-Khaleej Wood" }, default) as RedirectToActionResult;

        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Detail");
        result.RouteValues!["id"].Should().Be(99);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Create POST — invalid model returns view without calling service
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_POST_InvalidModel_ReturnsViewWithoutCallingService()
    {
        _sut.ModelState.AddModelError("Supplier", "required");

        var result = await _sut.Create(new CreateOrderRequest(), default);

        result.Should().BeOfType<ViewResult>();
        _orderSvc.Verify(s => s.CreateAsync(It.IsAny<CreateOrderRequest>(), default), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Detail — unknown order returns 404
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_UnknownId_Returns404()
    {
        _orderSvc.Setup(s => s.GetByIdAsync(999, default)).ReturnsAsync((OrderDetailViewModel?)null);
        var productSvc = new Mock<IProductService>();
        productSvc.Setup(p => p.ListAsync(default)).ReturnsAsync(new List<ProductListItemViewModel>());

        var result = await _sut.Detail(999, productSvc.Object, default);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Advance — calls service and redirects back to Detail
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Advance_CallsServiceAndRedirectsToDetail()
    {
        _orderSvc.Setup(s => s.AdvanceStatusAsync(5, default)).Returns(Task.CompletedTask);

        var result = await _sut.Advance(5, default) as RedirectToActionResult;

        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Detail");
        result.RouteValues!["id"].Should().Be(5);
        _orderSvc.Verify(s => s.AdvanceStatusAsync(5, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Cancel — calls service and redirects back to Detail
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_CallsServiceAndRedirectsToDetail()
    {
        _orderSvc.Setup(s => s.CancelAsync(7, default)).Returns(Task.CompletedTask);

        var result = await _sut.Cancel(7, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Detail");
        result.RouteValues!["id"].Should().Be(7);
        _orderSvc.Verify(s => s.CancelAsync(7, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // AddItem — calls service and redirects to Detail
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddItem_ValidInput_CallsServiceAndRedirects()
    {
        _orderSvc.Setup(s => s.AddItemAsync(3, 10, 5, 12, default)).Returns(Task.CompletedTask);

        var result = await _sut.AddItem(3, 10, 5, 12, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Detail");
        _orderSvc.Verify(s => s.AddItemAsync(3, 10, 5, 12, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // ReceiveToAreaZ — calls service and redirects to Detail
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReceiveToAreaZ_CallsServiceAndRedirects()
    {
        _orderSvc.Setup(s => s.ReceiveItemToAreaZAsync(20, default)).Returns(Task.CompletedTask);

        var result = await _sut.ReceiveToAreaZ(3, 20, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Detail");
        _orderSvc.Verify(s => s.ReceiveItemToAreaZAsync(20, default), Times.Once);
    }
}
