using ManolyWarehouse.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ManolyWarehouse.Middleware;

/// <summary>
/// Catches DomainException thrown by services/entities and surfaces
/// the Arabic message to the user via TempData, then redirects back.
/// For non-domain exceptions, lets the default handler take over.
/// </summary>
public class DomainExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DomainExceptionMiddleware> _logger;

    public DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITempDataDictionaryFactory tempDataFactory)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain rule violation: {Message}", ex.Message);

            var tempData = tempDataFactory.GetTempData(context);
            tempData["ErrorMessage"] = ex.Message;

            var referer = context.Request.Headers["Referer"].ToString();
            context.Response.Redirect(string.IsNullOrEmpty(referer) ? "/" : referer);
        }
    }
}
