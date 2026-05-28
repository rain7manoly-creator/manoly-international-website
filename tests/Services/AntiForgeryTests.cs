using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ManolyWarehouse.Controllers;
using Xunit;

namespace ManolyWarehouse.Tests.Services;

/// <summary>
/// DB-02: All POST requests must carry ValidateAntiForgeryToken.
/// This reflects the business rule that all state-changing requests
/// must be CSRF-protected.
/// </summary>
public class AntiForgeryTests
{
    private static IEnumerable<MethodInfo> GetPostMethods(Type controllerType)
        => controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null);

    private static void AssertAllPostMethodsHaveAntiForgery(Type controllerType)
    {
        var postMethods = GetPostMethods(controllerType).ToList();
        postMethods.Should().NotBeEmpty($"{controllerType.Name} should have at least one POST method");

        foreach (var method in postMethods)
        {
            method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()
                .Should().NotBeNull(
                    $"{controllerType.Name}.{method.Name} is a POST action and must have [ValidateAntiForgeryToken]");
        }
    }

    [Fact] public void AccountController_AllPostActions_HaveAntiForgery()
        => AssertAllPostMethodsHaveAntiForgery(typeof(AccountController));

    [Fact] public void ShelvesController_AllPostActions_HaveAntiForgery()
        => AssertAllPostMethodsHaveAntiForgery(typeof(ShelvesController));

    [Fact] public void OrdersController_AllPostActions_HaveAntiForgery()
        => AssertAllPostMethodsHaveAntiForgery(typeof(OrdersController));

    [Fact] public void AreaZController_AllPostActions_HaveAntiForgery()
        => AssertAllPostMethodsHaveAntiForgery(typeof(AreaZController));

    [Fact] public void CategoriesController_AllPostActions_HaveAntiForgery()
        => AssertAllPostMethodsHaveAntiForgery(typeof(CategoriesController));

    [Fact] public void UsersController_AllPostActions_HaveAntiForgery()
        => AssertAllPostMethodsHaveAntiForgery(typeof(UsersController));

    [Fact] public void ProductsController_AllPostActions_HaveAntiForgery()
        => AssertAllPostMethodsHaveAntiForgery(typeof(ProductsController));
}
