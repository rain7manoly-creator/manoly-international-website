using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Controllers;
using Xunit;

namespace ManolyWarehouse.Tests.Services;

/// <summary>
/// Verifies that all mutating endpoints use POST (never GET for writes).
/// GET requests must be idempotent — DB-02 (CSRF protection only works on POST).
/// </summary>
public class HttpVerbTests
{
    private static bool ActionHasAttribute<TAttr>(Type controller, string actionName)
        where TAttr : Attribute
        => controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == actionName)
            .Any(m => m.GetCustomAttribute<TAttr>() != null);

    private static bool ControllerHasGetAndPost(Type controller, string actionName)
    {
        var methods = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == actionName)
            .ToList();
        bool hasGet  = methods.Any(m => m.GetCustomAttribute<HttpGetAttribute>()  != null);
        bool hasPost = methods.Any(m => m.GetCustomAttribute<HttpPostAttribute>() != null);
        return hasGet && hasPost;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Write endpoints must use POST, never GET
    // ────────────────────────────────────────────────────────────────────────

    [Fact] public void ShelvesController_Delete_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(ShelvesController), "Delete").Should().BeTrue();

    [Fact] public void ShelvesController_Adjust_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(ShelvesController), "Adjust").Should().BeTrue();

    [Fact] public void OrdersController_Advance_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(OrdersController), "Advance").Should().BeTrue();

    [Fact] public void OrdersController_Cancel_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(OrdersController), "Cancel").Should().BeTrue();

    [Fact] public void OrdersController_Delete_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(OrdersController), "Delete").Should().BeTrue();

    [Fact] public void OrdersController_AddItem_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(OrdersController), "AddItem").Should().BeTrue();

    [Fact] public void OrdersController_ReceiveToAreaZ_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(OrdersController), "ReceiveToAreaZ").Should().BeTrue();

    [Fact] public void AreaZController_Dispatch_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(AreaZController), "Dispatch").Should().BeTrue();

    [Fact] public void AreaZController_Shelve_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(AreaZController), "Shelve").Should().BeTrue();

    [Fact] public void CategoriesController_Delete_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(CategoriesController), "Delete").Should().BeTrue();

    [Fact] public void UsersController_ToggleActive_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(UsersController), "ToggleActive").Should().BeTrue();

    [Fact] public void UsersController_ResetPassword_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(UsersController), "ResetPassword").Should().BeTrue();

    [Fact] public void AccountController_Logout_IsHttpPost()
        => ActionHasAttribute<HttpPostAttribute>(typeof(AccountController), "Logout").Should().BeTrue();

    // ────────────────────────────────────────────────────────────────────────
    // Form pages must have both GET and POST overloads
    // ────────────────────────────────────────────────────────────────────────

    [Fact] public void AccountController_Login_HasGetAndPost()
        => ControllerHasGetAndPost(typeof(AccountController), "Login").Should().BeTrue();

    [Fact] public void OrdersController_Create_HasGetAndPost()
        => ControllerHasGetAndPost(typeof(OrdersController), "Create").Should().BeTrue();

    [Fact] public void ShelvesController_Add_HasGetAndPost()
        => ControllerHasGetAndPost(typeof(ShelvesController), "Add").Should().BeTrue();
}
