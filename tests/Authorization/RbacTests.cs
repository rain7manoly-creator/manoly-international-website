using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Controllers;
using Xunit;

namespace ManolyWarehouse.Tests.Authorization;

/// <summary>
/// Verifies that the correct [Authorize] attributes are applied to each
/// controller and action, as defined in the RBAC matrix.
/// These are structural reflection tests — they catch accidental removal
/// of [Authorize] attributes during refactoring.
/// </summary>
public class RbacTests
{
    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static AuthorizeAttribute? GetControllerAuthorize<T>()
        => typeof(T).GetCustomAttribute<AuthorizeAttribute>();

    private static AuthorizeAttribute? GetActionAuthorize<T>(string methodName)
        => typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                    ?.GetCustomAttribute<AuthorizeAttribute>();

    private static bool HasAdminPolicy<T>()
        => GetControllerAuthorize<T>()?.Policy == "AdminOnly";

    private static bool HasAuthPolicy<T>()
        => GetControllerAuthorize<T>()?.Policy == "AuthenticatedUser";

    // ────────────────────────────────────────────────────────────────────────
    // Controllers that require Admin role only
    // ────────────────────────────────────────────────────────────────────────

    [Fact] public void ProductsController_RequiresAdminPolicy()
        => HasAdminPolicy<ProductsController>().Should().BeTrue();

    [Fact] public void CategoriesController_RequiresAdminPolicy()
        => HasAdminPolicy<CategoriesController>().Should().BeTrue();

    [Fact] public void UsersController_RequiresAdminPolicy()
        => HasAdminPolicy<UsersController>().Should().BeTrue();

    // ────────────────────────────────────────────────────────────────────────
    // Controllers accessible to any authenticated user (Admin + Worker)
    // ────────────────────────────────────────────────────────────────────────

    [Fact] public void HomeController_RequiresAuthenticatedUserPolicy()
        => HasAuthPolicy<HomeController>().Should().BeTrue();

    [Fact] public void ShelvesController_RequiresAuthenticatedUserPolicy()
        => HasAuthPolicy<ShelvesController>().Should().BeTrue();

    [Fact] public void OrdersController_RequiresAuthenticatedUserPolicy()
        => HasAuthPolicy<OrdersController>().Should().BeTrue();

    [Fact] public void AreaZController_RequiresAuthenticatedUserPolicy()
        => HasAuthPolicy<AreaZController>().Should().BeTrue();

    [Fact] public void SearchController_RequiresAuthenticatedUserPolicy()
        => HasAuthPolicy<SearchController>().Should().BeTrue();

    // ────────────────────────────────────────────────────────────────────────
    // AccountController — AllowAnonymous at class level
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AccountController_HasAllowAnonymousAttribute()
    {
        typeof(AccountController)
            .GetCustomAttribute<AllowAnonymousAttribute>()
            .Should().NotBeNull("AccountController must be accessible without login");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Specific actions that must be Admin-only within shared controllers
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShelvesController_Delete_RequiresAdminPolicy()
    {
        // DELETE /shelves/{code}/inventory/{id} — Admin only (RBAC matrix)
        var method = typeof(ShelvesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Delete" && m.GetParameters().Any(p => p.Name == "id"));

        method.Should().NotBeNull();
        method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy
            .Should().Be("AdminOnly", "only admins can delete shelf inventory entries");
    }

    [Fact]
    public void OrdersController_Create_RequiresAdminPolicy()
    {
        // GET + POST /orders/create — Admin only
        var methods = typeof(OrdersController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Create")
            .ToList();

        methods.Should().NotBeEmpty();
        methods.Should().AllSatisfy(m =>
            m.GetCustomAttribute<AuthorizeAttribute>()?.Policy
                .Should().Be("AdminOnly", "only admins can create purchase orders"));
    }

    [Fact]
    public void OrdersController_Delete_RequiresAdminPolicy()
    {
        var method = typeof(OrdersController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Delete");

        method.Should().NotBeNull();
        method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy
            .Should().Be("AdminOnly");
    }

    [Fact]
    public void OrdersController_AddItem_RequiresAdminPolicy()
    {
        var method = typeof(OrdersController)
            .GetMethod("AddItem", BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull();
        method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy
            .Should().Be("AdminOnly", "only admins can add line items to orders");
    }

    [Fact]
    public void AreaZController_Dispatch_RequiresAdminPolicy()
    {
        var method = typeof(AreaZController)
            .GetMethod("Dispatch", BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull();
        method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy
            .Should().Be("AdminOnly", "dispatch is irreversible — admin only");
    }
}
