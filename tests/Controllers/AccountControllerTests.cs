using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Controllers;
using ManolyWarehouse.Domain.Entities;
using Xunit;

// Disambiguate — MVC also has a SignInResult
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace ManolyWarehouse.Tests.Controllers;

public class AccountControllerTests
{
    private readonly Mock<SignInManager<ApplicationUser>> _signInMgr;
    private readonly Mock<UserManager<ApplicationUser>> _userMgr;
    private readonly AccountController _sut;

    public AccountControllerTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userMgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _signInMgr = new Mock<SignInManager<ApplicationUser>>(
            _userMgr.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        _sut = new AccountController(
            _signInMgr.Object,
            _userMgr.Object,
            new Mock<ILogger<AccountController>>().Object);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Wire up IUrlHelper so Url.IsLocalUrl() works inside RedirectToLocal()
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.IsLocalUrl(It.Is<string>(s => s.StartsWith("/")))).Returns(true);
        urlHelper.Setup(u => u.IsLocalUrl(It.Is<string>(s => !s.StartsWith("/")))).Returns(false);
        _sut.Url = urlHelper.Object;
    }

    // ────────────────────────────────────────────────────────────────────────
    // AUTH-01 — Disabled account rejected at login
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_POST_DisabledAccount_ReturnsViewWithArabicError()
    {
        var user = ApplicationUser.Create("worker1", "Worker One", false);
        user.Deactivate();
        _userMgr.Setup(m => m.FindByNameAsync("worker1")).ReturnsAsync(user);

        var result = await _sut.Login(new LoginViewModel { UserName = "worker1", Password = "Pass123!" });

        var view = result.Should().BeOfType<ViewResult>().Subject;
        _sut.ModelState.IsValid.Should().BeFalse();
        _sut.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("تعطيل"));
        _signInMgr.Verify(m =>
            m.PasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(),
                                  It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // AUTH-01 — Unknown username returns same generic error (no user-enumeration)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_POST_UnknownUser_ReturnsGenericPasswordError()
    {
        _userMgr.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

        var result = await _sut.Login(new LoginViewModel { UserName = "nobody", Password = "x" });

        var view = result.Should().BeOfType<ViewResult>().Subject;
        _sut.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("غير صحيحة"));
    }

    // ────────────────────────────────────────────────────────────────────────
    // AUTH-01 — Wrong password → same generic error
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_POST_WrongPassword_ReturnsGenericError()
    {
        var user = ApplicationUser.Create("admin1", "Admin One", true);
        _userMgr.Setup(m => m.FindByNameAsync("admin1")).ReturnsAsync(user);
        _signInMgr.Setup(m => m.PasswordSignInAsync(user, "bad", true, true))
                  .ReturnsAsync(IdentitySignInResult.Failed);

        var result = await _sut.Login(new LoginViewModel { UserName = "admin1", Password = "bad" });

        _sut.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("غير صحيحة"));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Lockout message in Arabic
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_POST_LockedOut_ReturnsLockoutMessage()
    {
        var user = ApplicationUser.Create("locked", "Locked User", false);
        _userMgr.Setup(m => m.FindByNameAsync("locked")).ReturnsAsync(user);
        _signInMgr.Setup(m => m.PasswordSignInAsync(user, It.IsAny<string>(), true, true))
                  .ReturnsAsync(IdentitySignInResult.LockedOut);

        var result = await _sut.Login(new LoginViewModel { UserName = "locked", Password = "Pass123!" });

        _sut.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("مقفل"));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Successful login → redirect to home
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_POST_ValidCredentials_RedirectsToHome()
    {
        var user = ApplicationUser.Create("admin1", "Admin One", true);
        _userMgr.Setup(m => m.FindByNameAsync("admin1")).ReturnsAsync(user);
        _signInMgr.Setup(m => m.PasswordSignInAsync(user, "Pass123!", true, true))
                  .ReturnsAsync(IdentitySignInResult.Success);

        var result = await _sut.Login(new LoginViewModel { UserName = "admin1", Password = "Pass123!" });

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        redirect.ControllerName.Should().Be("Home");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Successful login with local returnUrl → redirects to it
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_POST_WithLocalReturnUrl_RedirectsToReturnUrl()
    {
        var user = ApplicationUser.Create("admin1", "Admin One", true);
        _userMgr.Setup(m => m.FindByNameAsync("admin1")).ReturnsAsync(user);
        _signInMgr.Setup(m => m.PasswordSignInAsync(user, "Pass123!", true, true))
                  .ReturnsAsync(IdentitySignInResult.Success);

        var result = await _sut.Login(new LoginViewModel
        {
            UserName = "admin1", Password = "Pass123!", ReturnUrl = "/orders"
        });

        result.Should().BeOfType<LocalRedirectResult>()
              .Which.Url.Should().Be("/orders");
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET /login?disabled=true pre-populates the Arabic disabled error
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Login_GET_DisabledFlag_AddsModelError()
    {
        var result = _sut.Login(returnUrl: null, disabled: true);

        result.Should().BeOfType<ViewResult>();
        _sut.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("تعطيل"));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Invalid ModelState → return view, never call SignIn
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_POST_InvalidModelState_ReturnsViewWithoutSignIn()
    {
        _sut.ModelState.AddModelError("UserName", "required");

        var result = await _sut.Login(new LoginViewModel());

        result.Should().BeOfType<ViewResult>();
        _signInMgr.Verify(m =>
            m.PasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(),
                                  It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }
}
