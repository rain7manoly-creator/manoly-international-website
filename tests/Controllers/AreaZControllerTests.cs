using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Controllers;
using Xunit;

namespace ManolyWarehouse.Tests.Controllers;

public class AreaZControllerTests
{
    private readonly Mock<IAreaZService> _areaZSvc = new();
    private readonly AreaZController _sut;

    public AreaZControllerTests()
    {
        _sut = new AreaZController(_areaZSvc.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    // ────────────────────────────────────────────────────────────────────────
    // Index — delegates to ListActive (shows non-dispatched only)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_CallsListActiveAndReturnsView()
    {
        _areaZSvc.Setup(s => s.ListActiveAsync(default))
                 .ReturnsAsync(new List<AreaZItemViewModel>());

        var result = await _sut.Index(default);

        result.Should().BeOfType<ViewResult>();
        _areaZSvc.Verify(s => s.ListActiveAsync(default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Detail — unknown id returns 404
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_UnknownId_Returns404()
    {
        _areaZSvc.Setup(s => s.GetByIdAsync(999, default)).ReturnsAsync((AreaZItemViewModel?)null);

        var result = await _sut.Detail(999, default);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Add POST — calls service with correct params and redirects to Index
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_POST_ValidInput_CallsServiceAndRedirectsToIndex()
    {
        _areaZSvc.Setup(s => s.AddAsync(5, 10, 12, "notes", default)).ReturnsAsync(1);

        var result = await _sut.Add(5, 10, 12, "notes", default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _areaZSvc.Verify(s => s.AddAsync(5, 10, 12, "notes", default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edit POST — updates and redirects to Detail (AREAZ-03)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_POST_ValidInput_UpdatesAndRedirectsToDetail()
    {
        _areaZSvc.Setup(s => s.UpdateAsync(3, 8, 10, null, default)).Returns(Task.CompletedTask);

        var result = await _sut.Edit(3, 8, 10, null, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Detail");
        result.RouteValues!["id"].Should().Be(3);
        _areaZSvc.Verify(s => s.UpdateAsync(3, 8, 10, null, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Dispatch (Admin only) — calls service and redirects to Index (AREAZ-04)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatch_CallsServiceAndRedirectsToIndex()
    {
        _areaZSvc.Setup(s => s.DispatchAsync(7, default)).Returns(Task.CompletedTask);

        var result = await _sut.Dispatch(7, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _areaZSvc.Verify(s => s.DispatchAsync(7, default), Times.Once);
    }
}
