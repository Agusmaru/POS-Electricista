using System.Text.Json;
using Dominio;
using Microsoft.AspNetCore.Mvc;
using Negocio;

namespace Flux.Web.Controllers;

public partial class PortalController
{
    private static readonly JsonSerializerOptions ComercialJson = new() { PropertyNameCaseInsensitive = true };
    private BorradorComercialArchivo BorradoresComerciales()
    {
        var env=HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        return new BorradorComercialArchivo(Path.Combine(env.ContentRootPath,"App_Data","presupuestos-comerciales"));
    }

    [HttpGet("/GenerarPresupuesto")]
    public IActionResult GenerarPresupuesto()
    {
        var m=Modelo("Generar Presupuesto");
        m.Comercial=BorradoresComerciales().Obtener(usuario.Id);
        return View(m);
    }

    [HttpPost("/GenerarPresupuesto/Nuevo")]
    public IActionResult NuevoComercial()
    {
        BorradoresComerciales().Nuevo(usuario.Id);
        return Redirect("/GenerarPresupuesto");
    }

    [HttpPost("/GenerarPresupuesto"), RequestSizeLimit(200000)]
    public IActionResult GuardarComercial([FromForm] string contenido,[FromForm] string accion)
    {
        if(accion is not ("guardar" or "pdf"))throw new ArgumentException("Acción inválida.");
        PresupuestoComercial p;
        try { p=JsonSerializer.Deserialize<PresupuestoComercial>(contenido ?? "",ComercialJson) ?? throw new JsonException(); }
        catch(JsonException){throw new ArgumentException("El presupuesto contiene datos inválidos.");}
        p.Validar();
        if(accion=="guardar")
        {
            BorradoresComerciales().Guardar(usuario.Id,p);
            return Json(new{ok=true,total=p.Total,neto=p.Neto,iva=p.Iva});
        }
        BorradoresComerciales().Comprobar(usuario.Id,p);
        var env=HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var assets=Path.Combine(env.WebRootPath,"img","presupuesto");
        var pdf=new PresupuestoComercialPdf().Generar(p,System.IO.File.ReadAllBytes(Path.Combine(assets,"logo.jpg")),System.IO.File.ReadAllBytes(Path.Combine(assets,"pie.jpg")));
        return File(pdf,"application/pdf","presupuesto-comercial.pdf");
    }
}
