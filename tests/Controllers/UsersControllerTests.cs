using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Controllers;
using Xunit;

namespace ManolyWarehouse.Tests.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUserService> _userSvc = new();
    private readonly UsersController _sut;

    public UsersControllerTests()
    {
        _sut = new UsersController(_userSvc.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    // ────────────────────────────────────────────────────────────────────────
    // Create POST — invalid model returns view without calling service
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_POST_InvalidModel_ReturnsViewWithoutCreating()
    {
        _sut.ModelState.AddModelError("UserName", "required");

        var result = await _sut.Create(new CreateUserRequest(), default);

        result.Should().BeOfType<ViewResult>();
        _userSvc.Verify(s => s.CreateAsync(It.IsAny<CreateUserRequest>(), default), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Create POST — valid request creates user and redirects to Index
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_POST_ValidRequest_CreatesAndRedirectsToIndex()
    {
        var req = new CreateUserRequest
        {
            UserName = "worker2", FullName = "أحمد علي",
            Password = "Pass123!", IsAdmin = false
        };
        _userSvc.Setup(s => s.CreateAsync(req, default)).ReturnsAsync("new-id");

        var result = await _sut.Create(req, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _userSvc.Verify(s => s.CreateAsync(req, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edit POST — updates profile and redirects to Index
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_POST_ValidInput_UpdatesAndRedirectsToIndex()
    {
        _userSvc.Setup(s => s.UpdateProfileAsync("uid-1", "محمد", true, default))
                .Returns(Task.CompletedTask);

        var result = await _sut.Edit("uid-1", "محمد", true, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _userSvc.Verify(s => s.UpdateProfileAsync("uid-1", "محمد", true, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // ResetPassword — calls service and redirects to Index
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_POST_CallsServiceAndRedirectsToIndex()
    {
        _userSvc.Setup(s => s.ResetPasswordAsync("uid-2", "NewPass1!", default))
                .Returns(Task.CompletedTask);

        var result = await _sut.ResetPassword("uid-2", "NewPass1!", default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _userSvc.Verify(s => s.ResetPasswordAsync("uid-2", "NewPass1!", default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // ToggleActive — calls service and redirects to Index (AUTH-02)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleActive_POST_CallsServiceAndRedirectsToIndex()
    {
        _userSvc.Setup(s => s.ToggleActiveAsync("uid-3", default)).Returns(Task.CompletedTask);

        var result = await _sut.ToggleActive("uid-3", default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _userSvc.Verify(s => s.ToggleActiveAsync("uid-3", default), Times.Once);
    }
}
