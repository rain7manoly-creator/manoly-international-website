using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ManolyWarehouse.Domain.Entities;
using ManolyWarehouse.Extensions;
using ManolyWarehouse.Infrastructure.Persistence;
using ManolyWarehouse.Infrastructure.Seeding;
using ManolyWarehouse.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ─── Logging ───────────────────────────────────────────────────────────
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .WriteTo.Console());

// ─── Services ──────────────────────────────────────────────────────────
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddIdentityServices();
builder.Services.AddAuthorizationPolicies();
builder.Services.AddApplicationServices();

// Health check hits the DB — Fly's http_service.checks calls /health so a
// wedged app or unreachable Neon is taken out of rotation instead of serving errors.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// ─── Data Protection ───────────────────────────────────────────────────
// Persist encryption keys to PostgreSQL so antiforgery tokens and auth
// cookies remain valid across container restarts / Render.com re-deploys.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("manoly-warehouse");

builder.Services.AddControllersWithViews(options =>
{
    // CSRF tokens enabled by default for all POST forms
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "X-CSRF-TOKEN";
    options.HeaderName = "X-CSRF-TOKEN";
});

// QuestPDF community license
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

// ─── Forwarded Headers ─────────────────────────────────────────────────
// Required on Fly.io / Render — SSL is terminated at the proxy layer. Without
// this, Request.Scheme stays "http" so auth cookies miss the Secure flag and
// the real client IP is lost. KnownNetworks/KnownProxies are cleared because
// the proxy is not on a loopback address (the default trust list).
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// ─── Database Migration + Seeding ──────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migration applied.");

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var config = services.GetRequiredService<IConfiguration>();

        await DbInitializer.InitializeAsync(userManager, roleManager, config, logger);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database initialization failed.");
        throw;
    }
}

// ─── Pipeline ──────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    // In dev, redirect HTTP → HTTPS locally
    app.UseHttpsRedirection();
}
else
{
    // In production (Render.com), SSL is terminated at the load balancer.
    // UseHttpsRedirection would cause infinite redirect loops — skip it.
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStaticFiles();
app.UseSerilogRequestLogging();

app.UseRouting();

app.UseAuthentication();

// AUTH-02: disabled accounts get signed out on next request
app.UseMiddleware<DisabledAccountMiddleware>();

app.UseAuthorization();

// Domain exceptions translated to user-friendly messages
app.UseMiddleware<DomainExceptionMiddleware>();

app.MapHealthChecks("/health").AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
