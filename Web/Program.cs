using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Negocio;
using Flux.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(o => { o.FormFieldName = "csrf"; o.Cookie.Name = "FluxCore10.Csrf"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict; });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "FluxCore10.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.LoginPath = "/Login";
    o.AccessDeniedPath = "/AccesoDenegado";
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    o.SlidingExpiration = true;
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddSingleton<CatalogoCache>();
builder.Services.AddSingleton<ImagenProductoService>();
builder.Services.Configure<ProveedorOpciones>(builder.Configuration.GetSection("Proveedor"));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = "FluxCore10.Carrito";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.IdleTimeout = TimeSpan.FromHours(4);
});
builder.Services.AddDataProtection().SetApplicationName("FluxElectricistasCore10")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
CatalogoDatos.CadenaConexion = Environment.GetEnvironmentVariable("ELECTRICISTAS_CORE10_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Electricistas");
EncodingSetup();
var app = builder.Build();
app.UseExceptionHandler("/Error");
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "same-origin";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; img-src 'self' data:; form-action 'self'; frame-ancestors 'none'; base-uri 'self'";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/salud", () => Results.Ok(new { estado = "ok" })).AllowAnonymous();
app.MapGet("/disponibilidad", async (IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    try
    {
        await using var connection = await CatalogoDatos.AbrirAsync(cancellationToken);
        using var command = CatalogoDatos.Comando(connection, "SELECT 1");
        var sqlDisponible = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
        var imagenesDisponibles = Directory.Exists(Path.Combine(environment.WebRootPath, "uploads", "productos"));
        return sqlDisponible && imagenesDisponibles
            ? Results.Ok(new { estado = "disponible", sql = true, imagenes = true })
            : Results.Json(new { estado = "no disponible", sql = sqlDisponible, imagenes = imagenesDisponibles }, statusCode: 503);
    }
    catch
    {
        return Results.Json(new { estado = "no disponible", sql = false, imagenes = false }, statusCode: 503);
    }
}).AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/Catalogo"));
app.Run();

static void EncodingSetup()
{
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("es-AR");
    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
}
