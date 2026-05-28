using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Controllers;
using Xunit;

namespace ManolyWarehouse.Tests.Controllers;

public class CategoriesControllerTests
{
    private readonly Mock<ICategoryService> _categorySvc = new();
    private readonly CategoriesController _sut;

    public CategoriesControllerTests()
    {
        _sut = new CategoriesController(_categorySvc.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    // ────────────────────────────────────────────────────────────────────────
    // Add POST — blank name blocked (CAT-01)
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Add_POST_BlankName_ReturnsViewWithModelError(string name)
    {
        var result = await _sut.Add(name, default);

        result.Should().BeOfType<ViewResult>();
        _sut.ModelState.IsValid.Should().BeFalse();
        _categorySvc.Verify(s => s.CreateAsync(It.IsAny<string>(), default), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Add POST — valid name creates and redirects to Index
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_POST_ValidName_CreatesAndRedirectsToIndex()
    {
        _categorySvc.Setup(s => s.CreateAsync("أبواب", default)).ReturnsAsync(1);

        var result = await _sut.Add("أبواب", default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _categorySvc.Verify(s => s.CreateAsync("أبواب", default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edit POST — calls service with trimmed name
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_POST_ValidInput_UpdatesAndRedirectsToIndex()
    {
        _categorySvc.Setup(s => s.UpdateAsync(2, "نوافذ", default)).Returns(Task.CompletedTask);

        var result = await _sut.Edit(2, "نوافذ", default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _categorySvc.Verify(s => s.UpdateAsync(2, "نوافذ", default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Delete POST — calls service and redirects to Index
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_POST_CallsServiceAndRedirectsToIndex()
    {
        _categorySvc.Setup(s => s.DeleteAsync(3, default)).Returns(Task.CompletedTask);

        var result = await _sut.Delete(3, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _categorySvc.Verify(s => s.DeleteAsync(3, default), Times.Once);
    }
}
