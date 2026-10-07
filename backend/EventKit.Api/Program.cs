using System.Security.Claims;
using System.Threading.RateLimiting;
using EventKit.Api;
using EventKit.Api.Data;
using EventKit.Api.Domain;
using EventKit.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var migrate = args.Contains("--migrate");
var seed = args.Contains("--seed");
LocalEnvironment.Load();
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--migrate" && a != "--seed").ToArray());
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BusinessClock>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<IPasswordHasher<StaffUser>, PasswordHasher<StaffUser>>();
var databaseUrl = builder.Configuration["DATABASE_URL"] ?? throw new InvalidOperationException("Set DATABASE_URL to your Neon connection string. See README.md.");
builder.Services.AddDbContext<EventKitDbContext>(options => options.UseNpgsql(DatabaseConnection.Parse(databaseUrl)));
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 128 * 1024);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
    o.Cookie.Name = "eventkit.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o => {
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();

if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var direct = builder.Configuration["DATABASE_URL_UNPOOLED"] ?? databaseUrl;
    var options = new DbContextOptionsBuilder<EventKitDbContext>().UseNpgsql(DatabaseConnection.Parse(direct)).Options;
    await using var migrationDb = new EventKitDbContext(options);
    await migrationDb.Database.MigrateAsync();
    await DatabaseSetup.EnsureStaff(migrationDb, builder.Configuration, builder.Environment.IsDevelopment());
    if (seed)
    {
        if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Demo data can only be seeded in Development.");
        await DatabaseSetup.Seed(migrationDb, scope.ServiceProvider.GetRequiredService<BusinessClock>().Today);
    }
    return;
}

app.UseExceptionHandler();
app.Use(async (context, next) => {
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    try { await next(); }
    catch (DomainException ex) { await Results.Problem(title: ex.Message, statusCode: ex.StatusCode).ExecuteAsync(context); }
    catch (AntiforgeryValidationException) { await Results.Problem(title: "Your session has changed. Refresh the page and try again.", statusCode: 400).ExecuteAsync(context); }
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api") && context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "EventKit" }));
app.MapGet("/api/auth/session", (HttpContext context, IAntiforgery antiforgery) => {
    var token = antiforgery.GetAndStoreTokens(context).RequestToken;
    return Results.Ok(new {
        user = context.User.Identity?.IsAuthenticated == true ? new {
            name = context.User.FindFirstValue(ClaimTypes.Name), email = context.User.FindFirstValue(ClaimTypes.Email)
        } : null,
        csrfToken = token,
        demo = app.Environment.IsDevelopment() && string.IsNullOrEmpty(app.Configuration["Admin:Password"])
    });
});
app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext context, EventKitDbContext db, IPasswordHasher<StaffUser> hasher, CancellationToken ct) => {
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256)
        return Results.Problem(title: "Enter your email and password.", statusCode: 400);
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.StaffUsers.SingleOrDefaultAsync(u => u.Email == email, ct);
    if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        return Results.Problem(title: "Email or password is incorrect.", statusCode: 401);
    var identity = new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Email, user.Email)
    ], CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Ok(new { user.Name, user.Email });
}).RequireRateLimiting("login");
app.MapPost("/api/auth/logout", async (HttpContext context) => {
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
}).RequireAuthorization();
var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/equipment", async (DateOnly? start, DateOnly? end, BookingService service, BusinessClock clock, CancellationToken ct) =>
    Results.Ok(await service.Catalogue(start ?? clock.Today, end ?? start ?? clock.Today, ct)));
api.MapPost("/equipment", async (EquipmentRequest request, BookingService service, CancellationToken ct) => {
    var item = await service.SaveEquipment(null, request, ct);
    return Results.Created($"/api/equipment/{item.Id}", item);
});
api.MapPut("/equipment/{id:guid}", async (Guid id, EquipmentRequest request, BookingService service, CancellationToken ct) =>
    Results.Ok(await service.SaveEquipment(id, request, ct)));
api.MapGet("/bookings", async (BookingService service, CancellationToken ct) => Results.Ok(await service.ListBookings(ct)));
api.MapPost("/bookings", async (BookingRequest request, BookingService service, CancellationToken ct) => {
    var booking = await service.Create(request, ct);
    return Results.Created($"/api/bookings/{booking.Id}", booking);
});
api.MapPost("/bookings/{id:guid}/{action}", async (Guid id, string action, BookingService service, CancellationToken ct) =>
    Results.Ok(await service.Transition(id, action, ct)));
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
await app.RunAsync();

public partial class Program;
