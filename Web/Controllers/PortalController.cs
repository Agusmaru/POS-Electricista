using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Dominio;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Negocio;

namespace Flux.Web.Controllers;

public class PortalModel
{
    public string Titulo { get; set; }
    public UsuarioCatalogo Usuario { get; set; }
    public List<ArticuloCatalogo> Productos { get; set; } = [];
    public List<string> Marcas { get; set; } = [];
    public List<string> Tipos { get; set; } = [];
    public List<Presupuesto> Presupuestos { get; set; } = [];
    public List<UsuarioCatalogo> Usuarios { get; set; } = [];
    public UsuarioCatalogo UsuarioEditado { get; set; }
    public ArticuloCatalogo Producto { get; set; }
    public Presupuesto Presupuesto { get; set; }
    public int CarritoCantidad { get; set; }
    public string Q { get; set; } = "";
    public string Marca { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Rol { get; set; } = "";
    public string Estado { get; set; } = "";
    public bool Inactivos { get; set; }
    public int Pagina { get; set; } = 1;
    public int Paginas { get; set; } = 1;
    public int Total { get; set; }
    public string Vista { get; set; } = "lista";
    public string Error { get; set; }
    public static string Precio(decimal? p) => p.HasValue ? "$ " + p.Value.ToString("N2", CultureInfo.GetCultureInfo("es-AR")) : "A consultar";
    public static string Numero(decimal? n) => n?.ToString(CultureInfo.InvariantCulture) ?? "";
}

[Authorize]
public class PortalController : Controller
{
    private readonly CatalogoNegocio catalogo = new();
    private readonly PresupuestoNegocio presupuestos = new();
    private readonly SeguridadCatalogo seguridad = new();
    private UsuarioCatalogo usuario;
    private readonly IWebHostEnvironment entorno;
    public PortalController(IWebHostEnvironment entorno) => this.entorno = entorno;
    private PortalModel Modelo(string titulo) => new() { Titulo = titulo, Usuario = usuario, CarritoCantidad = usuario == null ? 0 : ObtenerCarrito(false)?.Items.Count ?? 0 };

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Response.Headers.CacheControl = "no-store";
        if (context.ActionDescriptor.RouteValues["action"] != "Error" && User.Identity?.IsAuthenticated == true)
        {
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)) usuario = seguridad.Obtener(id);
            if (usuario == null)
            {
                await HttpContext.SignOutAsync();
                context.Result = Redirect("/Login");
                return;
            }
        }
        var executed = await next();
        if (executed.Exception is ArgumentException ex && !executed.ExceptionHandled)
        {
            executed.ExceptionHandled = true;
            Response.StatusCode = 400;
            var model = Modelo("Revisá los datos"); model.Error = ex.Message;
            executed.Result = View("Error", model);
        }
    }

    [AllowAnonymous, HttpGet("/Login")]
    public IActionResult Login() => usuario != null ? Redirect("/Catalogo") : View(Modelo("Ingresá a Flux"));

    [AllowAnonymous, HttpPost("/Login")]
    public async Task<IActionResult> Ingresar(IFormCollection form)
    {
        var u = seguridad.Login(Texto(form, "email", 200), Texto(form, "password", 128, false));
        if (u == null)
        {
            Response.StatusCode = 400;
            var model = Modelo("Ingresá a Flux"); model.Error = "Email o contraseña incorrectos, usuario inactivo o acceso temporalmente bloqueado.";
            return View("Login", model);
        }
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Name, u.Nombre), new Claim(ClaimTypes.Role, u.Admin ? "Admin" : "Asesor") };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return Redirect("/Catalogo");
    }

    [HttpGet("/Catalogo")]
    public IActionResult Catalogo(string q = "", string marca = "", string tipo = "", bool inactivos = false, int pagina = 1, string vista = "")
    {
        q = (q ?? "").Trim(); marca = (marca ?? "").Trim(); tipo = (tipo ?? "").Trim();
        if (q.Length > 250 || marca.Length > 120 || tipo.Length > 120) throw new ArgumentException("Los filtros son demasiado extensos.");
        vista = vista is "lista" or "tarjetas" ? vista : Request.Cookies["catalogo-vista"] ?? "lista";
        if (vista is not ("lista" or "tarjetas")) vista = "lista";
        Response.Cookies.Append("catalogo-vista", vista, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true, MaxAge = TimeSpan.FromDays(365) });
        var todos = catalogo.Buscar(inactivos: usuario.Admin && inactivos);
        var filtrados = q == "" && marca == "" && tipo == "" ? todos : catalogo.Buscar(q, marca, tipo, usuario.Admin && inactivos);
        var m = Modelo("Materiales para tu obra");
        m.Total = filtrados.Count; m.Paginas = Math.Max(1, (int)Math.Ceiling(m.Total / 30m)); m.Pagina = Math.Clamp(pagina, 1, m.Paginas);
        m.Productos = filtrados.Skip((m.Pagina - 1) * 30).Take(30).ToList();
        foreach (var producto in m.Productos) if (!ImagenDisponible(producto.Imagen)) producto.Imagen = "";
        m.Marcas = todos.Select(p => p.Marca.Nombre).Distinct().Order().ToList(); m.Tipos = todos.Select(p => p.Tipo).Distinct().Order().ToList();
        m.Q = q; m.Marca = marca; m.Tipo = tipo; m.Inactivos = usuario.Admin && inactivos; m.Vista = vista;
        return View(m);
    }

    [HttpPost("/Carrito/Agregar")]
    public IActionResult AgregarCarrito(IFormCollection f)
    {
        var producto = catalogo.Obtener(Entero(f,"producto"));
        if (producto == null || !producto.Activo) throw new ArgumentException("El producto ya no está disponible.");
        if (!ImagenDisponible(producto.Imagen)) producto.Imagen = "";
        var cantidad = Numero(f,"cantidad",true);
        var carrito = ObtenerCarrito();
        var existente = carrito.Items.FirstOrDefault(i => i.ProductoId == producto.Id);
        if (existente == null) carrito.Items.Add(PresupuestoNegocio.DesdeProducto(producto,cantidad,producto.PrecioEstimado ?? 0m,""));
        else existente.Cantidad = Math.Min(1000000m, existente.Cantidad + cantidad);
        GuardarCarrito(carrito);
        TempData["Mensaje"] = producto.Nombre + " se agregó al presupuesto.";
        string volver = Texto(f,"volver",500);
        return Redirect(volver.StartsWith("/Catalogo",StringComparison.Ordinal) ? volver : "/Catalogo");
    }

    [HttpGet("/Carrito")]
    public IActionResult Carrito()
    {
        var m = Modelo("Revisar presupuesto");
        m.Presupuesto = ObtenerCarrito();
        foreach (var item in m.Presupuesto.Items) if (!ImagenDisponible(item.Imagen)) item.Imagen = "";
        m.CarritoCantidad = m.Presupuesto.Items.Count;
        return View(m);
    }

    [HttpPost("/Carrito")]
    public IActionResult EditarCarrito(IFormCollection f)
    {
        var carrito = ObtenerCarrito();
        switch (Texto(f,"accion",20))
        {
            case "nombre":
                carrito.Nombre = Texto(f,"nombre",160);
                if (carrito.Nombre == "") throw new ArgumentException("Ingresá un nombre para la obra.");
                break;
            case "cantidad":
                var item = carrito.Items.FirstOrDefault(i => i.ProductoId == Entero(f,"producto")) ?? throw new ArgumentException("El producto ya no está en el carrito.");
                item.Cantidad = Numero(f,"cantidad",true);
                break;
            case "quitar":
                carrito.Items.RemoveAll(i => i.ProductoId == Entero(f,"producto"));
                break;
            case "vaciar":
                carrito.Items.Clear();
                break;
            case "confirmar":
                if (carrito.Items.Count == 0) throw new ArgumentException("Agregá al menos un producto antes de confirmar.");
                carrito.Local = "";
                carrito.Observaciones ??= "";
                int id = presupuestos.Guardar(carrito,usuario);
                HttpContext.Session.Remove(ClaveCarrito);
                TempData["Mensaje"] = "Orden generada correctamente. Ya podés descargar el PDF.";
                return Redirect("/Presupuesto?id=" + id);
            default: throw new ArgumentException("Acción inválida.");
        }
        GuardarCarrito(carrito);
        TempData["Mensaje"] = f["accion"] == "nombre" ? "Nombre de obra actualizado." : "Carrito actualizado.";
        return Redirect("/Carrito");
    }

    [HttpGet("/EditarProducto")]
    public IActionResult EditarProducto(int id = 0)
    {
        if (!usuario.Admin) return StatusCode(403);
        var m = Modelo(id == 0 ? "Nuevo producto" : "Editar producto");
        m.Producto = id == 0 ? new ArticuloCatalogo { Tipo = "Sin clasificar", Unidad = "unidad", PrecioEstimado = 0, Activo = true } : catalogo.Obtener(id);
        return m.Producto == null ? NotFound() : View(m);
    }

    [HttpPost("/EditarProducto"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> GuardarProducto(IFormCollection f, IFormFile imagen, int id = 0)
    {
        if (!usuario.Admin) return StatusCode(403);
        var anterior = id == 0 ? null : catalogo.Obtener(id); if (id != 0 && anterior == null) return NotFound();
        var p = new ArticuloCatalogo { Id = id, Nombre = Texto(f,"nombre",250), Descripcion = Texto(f,"descripcion",1500), Marca = new() { Nombre = Texto(f,"marca",120) }, Categoria = new() { Nombre = Texto(f,"categoria",120) }, CodigoCatalogo = Texto(f,"codigo",160), CodigoLocal = Texto(f,"local",160), Tipo = Texto(f,"tipo",120), Unidad = Texto(f,"unidad",30), PrecioEstimado = Precio(f,"precio") ?? 0m, Imagen = anterior?.Imagen ?? "", Activo = f["activo"] == "1" };
        if (p.Tipo == "" || p.Unidad == "") throw new ArgumentException("Completá tipo y unidad.");
        string nueva = "";
        if (imagen is { Length: > 0 }) { nueva = await GuardarImagen(imagen); p.Imagen = nueva; }
        else if (f["quitarImagen"] == "1") p.Imagen = "";
        try { catalogo.Guardar(p); }
        catch { if (nueva != "") BorrarImagen(nueva); throw; }
        if (anterior?.Imagen is { Length: > 0 } && anterior.Imagen != p.Imagen) BorrarImagen(anterior.Imagen);
        TempData["Mensaje"] = id == 0 ? "Producto creado correctamente." : "Producto actualizado correctamente.";
        return Redirect("/Catalogo");
    }

    [HttpGet("/Presupuestos")]
    public IActionResult Presupuestos() { var m = Modelo("Presupuestos"); m.Presupuestos = presupuestos.Listar(usuario); return View(m); }

    [HttpPost("/Presupuestos")]
    public IActionResult GestionarPresupuestos(IFormCollection f)
    {
        switch (Texto(f,"accion",20))
        {
            case "nuevo": return Redirect("/Presupuesto?id=" + presupuestos.Guardar(new Presupuesto { Nombre = Texto(f,"nombre",160), Local = Texto(f,"local",200) }, usuario));
            case "duplicar": return Redirect("/Presupuesto?id=" + presupuestos.Duplicar(Entero(f,"id"), usuario));
            case "eliminar": presupuestos.Eliminar(Entero(f,"id"), usuario, Entero(f,"revision")); return Redirect("/Presupuestos");
            default: throw new ArgumentException("Acción inválida.");
        }
    }

    [HttpGet("/Presupuesto")]
    public IActionResult Presupuesto(int id, string buscar = "")
    {
        var p = presupuestos.Obtener(id, usuario); if (p == null) return NotFound();
        var m = Modelo(p.Nombre); m.Presupuesto = p; m.Q = buscar ?? "";
        var compare = CultureInfo.GetCultureInfo("es-AR").CompareInfo;
        var encontrados = m.Q == "" ? new List<ArticuloCatalogo>() : catalogo.Buscar().Where(a => compare.IndexOf(a.Nombre + " " + a.Descripcion + " " + a.CodigoCatalogo + " " + a.CodigoLocal + " " + a.Marca.Nombre + " " + a.Tipo, m.Q, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0).ToList();
        m.Total = encontrados.Count; m.Productos = encontrados.Take(30).ToList(); return View(m);
    }

    [HttpPost("/Presupuesto")]
    public IActionResult GuardarPresupuesto(int id, IFormCollection f)
    {
        var p = presupuestos.Obtener(id, usuario); if (p == null) return NotFound();
        if (Entero(f,"revision") != p.Revision) throw new ArgumentException("El presupuesto cambió. Recargá la página para continuar.");
        switch (Texto(f,"accion",20))
        {
            case "guardar": p.Nombre = Texto(f,"nombre",160); p.Local = Texto(f,"local",200); p.Observaciones = Texto(f,"observaciones",1500); break;
            case "agregar": p.Items.Add(PresupuestoNegocio.DesdeProducto(catalogo.Obtener(Entero(f,"producto")), Numero(f,"cantidad",true), Precio(f,"precio"), Texto(f,"nota",500))); break;
            case "item": case "quitar":
                var item = p.Items.FirstOrDefault(i => i.Id == Entero(f,"item")); if (item == null) throw new ArgumentException("El renglón ya no existe.");
                if (f["accion"] == "quitar") p.Items.Remove(item);
                else { item.Cantidad = Numero(f,"cantidad",true); item.PrecioUnitario = Precio(f,"precio"); item.Observaciones = Texto(f,"nota",500); } break;
            default: throw new ArgumentException("Acción inválida.");
        }
        presupuestos.Guardar(p, usuario); return Redirect("/Presupuesto?id=" + p.Id);
    }

    [HttpGet("/DescargarPdf")]
    public IActionResult DescargarPdf(int id)
    {
        var p = presupuestos.Obtener(id, usuario); if (p == null) return NotFound();
        return File(new PresupuestoPdf().Generar(p), "application/pdf", "orden-presupuesto-" + id.ToString("D6") + ".pdf");
    }

    [HttpGet("/UsuariosCatalogo")]
    public IActionResult UsuariosCatalogo(string q="",string rol="",string estado="",int editar=0,bool nuevo=false)
    {
        if(!usuario.Admin)return StatusCode(403);
        q=(q??"").Trim();rol=(rol??"").Trim();estado=(estado??"").Trim();
        if(q.Length>200||rol is not ("" or "admin" or "asesor")||estado is not ("" or "activo" or "inactivo"))throw new ArgumentException("Los filtros de usuarios no son válidos.");
        var m=Modelo("Usuarios");m.Q=q;m.Rol=rol;m.Estado=estado;m.Usuarios=seguridad.Listar(q,rol,estado);m.Total=m.Usuarios.Count;
        if(editar>0){m.UsuarioEditado=seguridad.ObtenerGestion(editar);if(m.UsuarioEditado==null)return NotFound();}
        else if(nuevo)m.UsuarioEditado=new UsuarioCatalogo{Activo=true};
        return View(m);
    }

    [HttpPost("/UsuariosCatalogo")]
    public IActionResult CrearUsuario(IFormCollection f)
    {
        if (!usuario.Admin) return StatusCode(403);
        string accion=Texto(f,"accion",20);int id=EnteroOpcional(f,"id");
        if(accion=="estado")
        {
            var actual=seguridad.ObtenerGestion(id)??throw new ArgumentException("El usuario ya no existe.");
            seguridad.GuardarGestion(id,actual.Nombre,actual.Email,actual.Admin,!actual.Activo,"",usuario.Id);
            TempData["Mensaje"]=actual.Activo?"Usuario desactivado.":"Usuario activado.";
        }
        else if(accion=="guardar")
        {
            seguridad.GuardarGestion(id,Texto(f,"nombre",120),Texto(f,"email",200),f["rol"]=="admin",f["activo"]=="1",Texto(f,"password",128,false),usuario.Id);
            TempData["Mensaje"]=id==0?"Usuario creado correctamente.":"Usuario actualizado correctamente.";
        }
        else throw new ArgumentException("Acción inválida.");
        return Redirect("/UsuariosCatalogo");
    }

    [HttpGet("/Cuenta")]
    public IActionResult Cuenta() => View(Modelo("Cambiar contraseña"));

    [HttpPost("/Cuenta")]
    public async Task<IActionResult> GuardarCuenta(IFormCollection f)
    {
        if (f["accion"] != "salir") seguridad.CambiarPassword(usuario.Id,Texto(f,"actual",128,false),Texto(f,"nueva",128,false));
        await HttpContext.SignOutAsync(); return Redirect("/Login");
    }

    [AllowAnonymous, Route("/Error")]
    public IActionResult Error() { Response.StatusCode = 500; var m = Modelo("No se pudo completar la operación"); m.Error = "Revisá los datos y la conexión con la base. Si estabas creando un usuario, verificá que el email no exista."; return View(m); }
    [AllowAnonymous, HttpGet("/AccesoDenegado")]
    public IActionResult AccesoDenegado() => StatusCode(403);

    private static string Texto(IFormCollection f,string key,int max,bool trim = true)
    { var s = f[key].ToString(); if (trim) s = s.Trim(); if (s.Length > max) throw new ArgumentException("El campo " + key + " supera " + max + " caracteres."); return s; }
    private static int Entero(IFormCollection f,string key) => int.TryParse(f[key],out int n) && n >= 0 ? n : throw new ArgumentException("Valor inválido: " + key);
    private static int EnteroOpcional(IFormCollection f,string key) => string.IsNullOrWhiteSpace(f[key]) ? 0 : Entero(f,key);
    private static decimal Numero(IFormCollection f,string key,bool cantidad = false)
    {
        var s = Texto(f,key,40).Replace(',','.');
        if (!decimal.TryParse(s,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out decimal n) || n < (cantidad ? 0.001m : 0m) || n > (cantidad ? 1000000m : 1000000000m) || decimal.Round(n,cantidad ? 3 : 2) != n)
            throw new ArgumentException(cantidad ? "La cantidad debe ser positiva y tener hasta tres decimales." : "Ingresá un precio positivo o cero, con hasta dos decimales.");
        return n;
    }
    private static decimal? Precio(IFormCollection f,string key) => string.IsNullOrWhiteSpace(f[key]) ? null : Numero(f,key);
    private string ClaveCarrito => "carrito-" + usuario.Id;
    private Presupuesto ObtenerCarrito(bool crear = true)
    {
        var json = HttpContext.Session.GetString(ClaveCarrito);
        if (!string.IsNullOrEmpty(json)) return JsonSerializer.Deserialize<Presupuesto>(json) ?? NuevoCarrito();
        return crear ? NuevoCarrito() : null;
    }
    private static Presupuesto NuevoCarrito() => new() { Nombre = "Instalación eléctrica " + DateTime.Now.ToString("yyyyMMdd-HHmm"), Fecha = DateTime.Now };
    private void GuardarCarrito(Presupuesto carrito) => HttpContext.Session.SetString(ClaveCarrito, JsonSerializer.Serialize(carrito));
    private async Task<string> GuardarImagen(IFormFile archivo)
    {
        if (archivo.Length > 5 * 1024 * 1024) throw new ArgumentException("La imagen no puede superar 5 MB.");
        byte[] cabecera = new byte[12];
        await using var lectura = archivo.OpenReadStream();
        int leidos = await lectura.ReadAsync(cabecera);
        string extension = leidos >= 4 && cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4e && cabecera[3] == 0x47 ? ".png"
            : leidos >= 3 && cabecera[0] == 0xff && cabecera[1] == 0xd8 && cabecera[2] == 0xff ? ".jpg"
            : leidos >= 12 && System.Text.Encoding.ASCII.GetString(cabecera,0,4) == "RIFF" && System.Text.Encoding.ASCII.GetString(cabecera,8,4) == "WEBP" ? ".webp"
            : throw new ArgumentException("La imagen debe ser JPG, PNG o WebP.");
        string carpeta = Path.Combine(entorno.WebRootPath, "uploads", "productos"); Directory.CreateDirectory(carpeta);
        string nombre = Guid.NewGuid().ToString("N") + extension;
        lectura.Position = 0;
        await using var destino = System.IO.File.Create(Path.Combine(carpeta, nombre));
        await lectura.CopyToAsync(destino);
        return nombre;
    }
    private void BorrarImagen(string nombre)
    {
        string seguro = Path.GetFileName(nombre); if (seguro == "") return;
        string ruta = Path.Combine(entorno.WebRootPath, "uploads", "productos", seguro);
        if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);
    }
    private bool ImagenDisponible(string nombre)
    {
        string seguro = Path.GetFileName(nombre ?? "");
        return seguro != "" && System.IO.File.Exists(Path.Combine(entorno.WebRootPath,"uploads","productos",seguro));
    }
}
