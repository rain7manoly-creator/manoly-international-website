using Microsoft.AspNetCore.Identity;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Middleware;

/// <summary>
/// AUTH-02: If admin disables an active session's user, this middleware
/// signs them out on their next request.
/// </summary>
public class DisabledAccountMiddleware
{
    private readonly RequestDelegate _next;

    public DisabledAccountMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = await userManager.GetUserAsync(context.User);

            if (user != null && !user.IsActive)
            {
                await signInManager.SignOutAsync();
                context.Response.Redirect("/Account/Login?disabled=true");
                return;
            }
        }

        await _next(context);
    }
}
