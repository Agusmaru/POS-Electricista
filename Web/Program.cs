using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Negocio;

var builder = WebApplication.CreateBuilder(args);
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
CatalogoNegocio.PrepararEtapa2();
CatalogoNegocio.PrepararColores();
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
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/salud", () => Results.Ok(new { estado = "ok" })).AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/Catalogo"));
app.Run();

static void EncodingSetup()
{
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("es-AR");
    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
}
