using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Dominio;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Negocio;
using Flux.Web.Services;

namespace Flux.Web.Controllers;

public class PortalModel
{
    public string Titulo { get; set; }
    public UsuarioCatalogo Usuario { get; set; }
    public List<ArticuloCatalogo> Productos { get; set; } = [];
    public List<string> Marcas { get; set; } = [];
    public HashSet<string> ImagenesDisponibles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
    public string Orden { get; set; } = "";
    public string EstadoCatalogo { get; set; } = "activos";
    public int Pagina { get; set; } = 1;
    public int Paginas { get; set; } = 1;
    public int Total { get; set; }
    public int PresupuestosNormales { get; set; }
    public int PresupuestosPrueba { get; set; }
    public string Vista { get; set; } = "lista";
    public string Tema { get; set; } = "dark";
    public string CatalogoVersion { get; set; } = "";
    public string Error { get; set; }
    public ProveedorOpciones Proveedor { get; set; } = new();
    public string MensajeCompartir { get; set; } = "";
    public bool SolicitudNueva { get; set; }
    public static string Numero(decimal? n) => n?.ToString(CultureInfo.InvariantCulture) ?? "";
}

[Authorize]
public class PortalController : Controller
{
    private readonly CatalogoNegocio catalogo = new();
    private readonly PresupuestoNegocio presupuestos = new();
    private readonly CarritoNegocio carritos = new();
    private readonly SeguridadCatalogo seguridad = new();
    private UsuarioCatalogo usuario;
    private readonly CatalogoCache catalogoCache;
    private readonly ImagenProductoService imagenService;
    private readonly ProveedorOpciones proveedor;
    public PortalController(CatalogoCache catalogoCache, ImagenProductoService imagenService, IOptions<ProveedorOpciones> proveedor)
    {
        this.catalogoCache = catalogoCache;
        this.imagenService = imagenService;
        this.proveedor = proveedor.Value;
    }
    private const string ClaveTema = "tema";
    private PortalModel Modelo(string titulo) => new() { Titulo = titulo, Usuario = usuario, Tema = ObtenerTema(), CarritoCantidad = usuario == null ? 0 : ObtenerCarrito(false)?.Items.Count ?? 0, Error = TempData["Error"] as string, Proveedor = proveedor };
    private bool SolicitaJson() => Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
        || Request.Headers["X-Requested-With"] == "XMLHttpRequest";

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
            if (SolicitaJson())
            {
                executed.Result = new JsonResult(new { ok = false, error = ex.Message }) { StatusCode = 400 };
                return;
            }
            Response.StatusCode = 400;
            var model = Modelo("Revisá los datos"); model.Error = ex.Message;
            executed.Result = View("Error", model);
        }
    }

    [AllowAnonymous, HttpGet("/Login")]
    public IActionResult Login() => usuario != null ? Redirect("/Catalogo") : View(Modelo("Ingresá a Flux"));

    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("/Login")]
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

    [AllowAnonymous, HttpPost("/Tema")]
    public IActionResult CambiarTema(IFormCollection form)
    {
        var tema = form["tema"].ToString().Trim().ToLowerInvariant();
        if (tema is not ("dark" or "light")) return BadRequest(new { ok = false, error = "Tema inválido." });
        HttpContext.Session.SetString(ClaveTema, tema);
        if (SolicitaJson()) return Json(new { ok = true, tema });
        var volver = form["volver"].ToString();
        return Url.IsLocalUrl(volver) ? LocalRedirect(volver) : Redirect(usuario == null ? "/Login" : "/Catalogo");
    }

    [HttpGet("/Catalogo")]
    public IActionResult Catalogo(string q = "", string marca = "", string tipo = "", string estado = "activos", bool? inactivos = null, int pagina = 1, string vista = "")
    {
        q = (q ?? "").Trim(); marca = (marca ?? "").Trim(); tipo = (tipo ?? "").Trim();
        estado = (estado ?? "activos").Trim().ToLowerInvariant();
        if (inactivos == true && estado == "activos") estado = "todos";
        if (!usuario.Admin || estado is not ("activos" or "inactivos" or "todos")) estado = "activos";
        if (q.Length > 250 || marca.Length > 120 || tipo.Length > 120) throw new ArgumentException("Los filtros son demasiado extensos.");
        vista = vista is "lista" or "tarjetas" ? vista : Request.Cookies["catalogo-vista"] ?? "lista";
        if (vista is not ("lista" or "tarjetas")) vista = "lista";
        Response.Cookies.Append("catalogo-vista", vista, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true, MaxAge = TimeSpan.FromDays(365) });
        var snapshot = catalogoCache.Obtener(usuario.Admin);
        var todos = estado switch
        {
            "inactivos" => snapshot.Productos.Where(p => !p.Activo).ToList(),
            "todos" => snapshot.Productos.ToList(),
            _ => snapshot.Productos.Where(p => p.Activo).ToList()
        };
        var compare = CultureInfo.GetCultureInfo("es-AR").CompareInfo;
        var filtrados = todos.Where(p =>
            (q == "" || compare.IndexOf($"{p.Nombre} {p.Descripcion} {p.Marca.Nombre} {p.Categoria.Nombre} {p.CodigoCatalogo} {p.CodigoLocal} {p.Tipo}", q, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
            && (marca == "" || string.Equals(p.Marca.Nombre, marca, StringComparison.CurrentCultureIgnoreCase))
            && (tipo == "" || string.Equals(p.Tipo, tipo, StringComparison.CurrentCultureIgnoreCase))).ToList();
        var m = Modelo("Materiales para tu obra");
        m.Total = filtrados.Count; m.Paginas = Math.Max(1, (int)Math.Ceiling(m.Total / 30m)); m.Pagina = Math.Clamp(pagina, 1, m.Paginas);
        m.Productos = filtrados.Skip((m.Pagina - 1) * 30).Take(30).ToList();
        m.ImagenesDisponibles = m.Productos.Where(producto => imagenService.Disponible(producto.Imagen))
            .Select(producto => producto.Imagen).ToHashSet(StringComparer.OrdinalIgnoreCase);
        m.Marcas = todos.Select(p => p.Marca.Nombre).Distinct().Order().ToList(); m.Tipos = todos.Select(p => p.Tipo).Distinct().Order().ToList();
        m.Q = q; m.Marca = marca; m.Tipo = tipo; m.EstadoCatalogo = estado; m.Vista = vista; m.CatalogoVersion = snapshot.Version;
        return View(m);
    }

    [HttpGet("/Catalogo/Datos")]
    public IActionResult DatosCatalogo()
    {
        var snapshot = catalogoCache.Obtener(usuario.Admin);
        return Json(new
        {
            version = snapshot.Version,
            productos = snapshot.Productos.Select(p => new
            {
                id = p.Id,
                nombre = p.Nombre,
                descripcion = p.Descripcion,
                marca = p.Marca.Nombre,
                categoria = p.Categoria.Nombre,
                codigoCatalogo = p.CodigoCatalogo,
                codigoLocal = p.CodigoLocal,
                tipo = p.Tipo,
                unidad = p.Unidad,
                imagen = imagenService.Disponible(p.Imagen) ? p.Imagen : "",
                activo = p.Activo,
                colores = p.Colores.Select(c => new { id = c.Id, nombre = c.Nombre, codigoHex = c.CodigoHex })
            })
        });
    }

    [HttpPost("/Carrito/Agregar")]
    public IActionResult AgregarCarrito(IFormCollection f)
    {
        var producto = catalogo.Obtener(Entero(f,"producto"));
        if (producto == null || !producto.Activo) throw new ArgumentException("El producto ya no está disponible.");
        if (!imagenService.Disponible(producto.Imagen)) producto.Imagen = "";
        var cantidad = CantidadEntera(f,"cantidad");
        var color = catalogo.ResolverColor(producto, EnteroOpcional(f,"color"));
        var carrito = ObtenerCarrito();
        var existente = carrito.Items.FirstOrDefault(i => i.ProductoId == producto.Id && i.ColorId == color?.Id);
        if (existente == null) carrito.Items.Add(PresupuestoNegocio.DesdeProducto(producto,cantidad,"",color));
        else existente.Cantidad = Math.Min(1000000m, existente.Cantidad + cantidad);
        GuardarCarrito(carrito);
        string mensaje = producto.Nombre + (color == null ? "" : " · " + color.Nombre) + " se agregó a la lista.";
        if (SolicitaJson()) return Json(new { ok = true, message = mensaje, cartCount = carrito.Items.Count, productId = producto.Id, colorId = color?.Id });
        TempData["Mensaje"] = mensaje;
        string volver = Texto(f,"volver",500);
        return Redirect(volver.StartsWith("/Catalogo",StringComparison.Ordinal) ? volver : "/Catalogo");
    }

    [HttpGet("/Carrito")]
    public IActionResult Carrito()
    {
        var m = Modelo("Lista de materiales");
        m.Presupuesto = ObtenerCarrito();
        var normalizado = false;
        foreach (var item in m.Presupuesto.Items.Where(item => item.Cantidad != decimal.Truncate(item.Cantidad)))
        {
            item.Cantidad = Math.Clamp(decimal.Round(item.Cantidad, 0, MidpointRounding.AwayFromZero), 1m, 1000000m);
            normalizado = true;
        }
        if (normalizado) GuardarCarrito(m.Presupuesto);
        foreach (var item in m.Presupuesto.Items) if (!imagenService.Disponible(item.Imagen)) item.Imagen = "";
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
                var item = carrito.Items.FirstOrDefault(i => i.ProductoId == Entero(f,"producto") && i.ColorId == ColorOpcional(f)) ?? throw new ArgumentException("El material ya no está en la lista.");
                item.Cantidad = CantidadEntera(f,"cantidad");
                break;
            case "quitar":
                var productoId = Entero(f,"producto"); var colorId = ColorOpcional(f);
                carrito.Items.RemoveAll(i => i.ProductoId == productoId && i.ColorId == colorId);
                break;
            case "vaciar":
                carrito.Items.Clear();
                break;
            case "confirmar":
                if (carrito.Items.Count == 0) throw new ArgumentException("Agregá al menos un material antes de generar la solicitud.");
                if (carrito.Items.Any(item => item.Cantidad != decimal.Truncate(item.Cantidad)))
                    throw new ArgumentException("Todas las cantidades de la lista deben ser números enteros.");
                var catalogoActual = catalogo.Buscar(inactivos: true).ToDictionary(producto => producto.Id);
                var renglonesActualizados = new List<ItemPresupuesto>();
                var problemas = new List<string>();
                foreach (var renglon in carrito.Items)
                {
                    if (!catalogoActual.TryGetValue(renglon.ProductoId, out var productoActual) || !productoActual.Activo)
                    {
                        problemas.Add(renglon.Nombre + ": producto inactivo o inexistente");
                        continue;
                    }
                    try
                    {
                        var colorActual = catalogo.ResolverColor(productoActual, renglon.ColorId ?? 0);
                        var actualizado = PresupuestoNegocio.DesdeProducto(productoActual, renglon.Cantidad, renglon.Observaciones, colorActual);
                        actualizado.Id = renglon.Id;
                        renglonesActualizados.Add(actualizado);
                    }
                    catch (ArgumentException ex) { problemas.Add(renglon.Nombre + ": " + ex.Message); }
                }
                if (problemas.Count > 0)
                {
                    var detalle = string.Join("; ", problemas.Take(5));
                    if (problemas.Count > 5) detalle += $"; y {problemas.Count - 5} producto(s) más";
                    TempData["Error"] = "No se puede enviar la solicitud. Revisá estos materiales: " + detalle + ".";
                    return Redirect("/Carrito");
                }
                carrito.Items = renglonesActualizados;
                carrito.Local = "";
                carrito.Observaciones ??= "";
                int id = presupuestos.Guardar(carrito,usuario);
                HttpContext.Session.Remove(ClaveCarrito);
                carritos.Eliminar(usuario.Id);
                TempData["Mensaje"] = "Solicitud preparada correctamente. Ya podés descargar o compartir el PDF.";
                return Redirect("/Presupuesto?id=" + id + "&nueva=1");
            default: throw new ArgumentException("Acción inválida.");
        }
        GuardarCarrito(carrito);
        TempData["Mensaje"] = f["accion"] == "nombre" ? "Nombre de obra actualizado." : "Lista actualizada.";
        return Redirect("/Carrito");
    }

    [HttpGet("/EditarProducto")]
    public IActionResult EditarProducto(int id = 0)
    {
        if (!usuario.Admin) return StatusCode(403);
        var m = Modelo(id == 0 ? "Nuevo producto" : "Editar producto");
        m.Producto = id == 0 ? new ArticuloCatalogo { Tipo = "Sin clasificar", Unidad = "unidad", Activo = true } : catalogo.Obtener(id);
        return m.Producto == null ? NotFound() : View(m);
    }

    [HttpPost("/EditarProducto"), RequestSizeLimit(45 * 1024 * 1024)]
    public async Task<IActionResult> GuardarProducto(IFormCollection f, IFormFile[] imagenes, int id = 0)
    {
        if (!usuario.Admin) return StatusCode(403);
        var anterior = id == 0 ? null : catalogo.Obtener(id); if (id != 0 && anterior == null) return NotFound();
        var p = new ArticuloCatalogo { Id = id, Nombre = Texto(f,"nombre",250), Descripcion = Texto(f,"descripcion",1500), Marca = new() { Nombre = Texto(f,"marca",120) }, Categoria = new() { Nombre = Texto(f,"categoria",120) }, CodigoCatalogo = Texto(f,"codigo",160), CodigoLocal = Texto(f,"local",160), Tipo = Texto(f,"tipo",120), Unidad = Texto(f,"unidad",30), Imagen = anterior?.Imagen ?? "", Activo = f["activo"] == "1" };
        if (p.Tipo == "" || p.Unidad == "") throw new ArgumentException("Completá tipo y unidad.");
        var nuevasSolicitadas = (imagenes ?? []).Where(x => x is { Length: > 0 }).ToList();
        if (nuevasSolicitadas.Count > 8) throw new ArgumentException("Podés subir hasta 8 fotos por vez.");
        var quitar = f["quitarFotos"].Select(x => int.TryParse(x,out var fotoId) && fotoId > 0 ? fotoId : 0).Where(x => x > 0).Distinct().ToList();
        var restantes = anterior?.Fotos.Count(x => !quitar.Contains(x.Id)) ?? 0;
        if (restantes + nuevasSolicitadas.Count > 12) throw new ArgumentException("Un producto puede tener hasta 12 fotos asociadas.");
        var nuevas = new List<string>();
        try
        {
            foreach (var archivo in nuevasSolicitadas) nuevas.Add(await imagenService.GuardarAsync(archivo, HttpContext.RequestAborted));
            var productoId = catalogo.Guardar(p);
            var principal = EnteroOpcional(f,"fotoPrincipal");
            catalogo.GuardarFotos(productoId, nuevas, quitar, principal == 0 ? null : principal);
        }
        catch { foreach (var nueva in nuevas) imagenService.EliminarArchivoNuevo(nueva); throw; }
        catalogoCache.Invalidar();
        TempData["Mensaje"] = id == 0 ? "Producto creado correctamente." : "Producto actualizado correctamente.";
        return Redirect("/Catalogo");
    }

    [HttpPost("/Producto/Estado")]
    public IActionResult CambiarEstadoProducto(IFormCollection f)
    {
        if (!usuario.Admin) return StatusCode(403);
        var activo = f["activo"] == "1";
        catalogo.CambiarEstado(Entero(f,"producto"), activo);
        catalogoCache.Invalidar();
        TempData["Mensaje"] = activo ? "Producto activado correctamente." : "Producto desactivado correctamente.";
        var volver = f["volver"].ToString();
        return Url.IsLocalUrl(volver) ? LocalRedirect(volver) : Redirect("/Catalogo");
    }
    [HttpGet("/Presupuestos")]
    public IActionResult Presupuestos(string q = "", string tipo = "", string orden = "recientes", int pagina = 1)
    {
        q = (q ?? "").Trim(); tipo = (tipo ?? "").Trim(); orden = (orden ?? "").Trim();
        if (q.Length > 160 || tipo is not ("" or "normal" or "prueba") || orden is not ("recientes" or "antiguos" or "nombre"))
            throw new ArgumentException("Los filtros de solicitudes no son válidos.");
        pagina = Math.Max(1, pagina);
        var resultado = presupuestos.Listar(usuario, q, tipo, orden, pagina, 20);
        var paginas = Math.Max(1, (int)Math.Ceiling(resultado.Total / 20m));
        if (pagina > paginas)
        {
            pagina = paginas;
            resultado = presupuestos.Listar(usuario, q, tipo, orden, pagina, 20);
        }
        var m = Modelo("Solicitudes");
        m.Presupuestos = resultado.Items; m.Q = q; m.Tipo = tipo; m.Orden = orden;
        m.Total = resultado.Total; m.PresupuestosNormales = resultado.Normales; m.PresupuestosPrueba = resultado.Pruebas;
        m.Pagina = pagina; m.Paginas = paginas;
        return View(m);
    }

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
    public IActionResult Presupuesto(int id, string buscar = "", bool nueva = false)
    {
        var p = presupuestos.Obtener(id, usuario); if (p == null) return NotFound();
        var m = Modelo(p.Nombre); m.Presupuesto = p; m.Q = buscar ?? ""; m.SolicitudNueva = nueva; m.MensajeCompartir = ConstruirMensaje(p);
        var compare = CultureInfo.GetCultureInfo("es-AR").CompareInfo;
        var encontrados = m.Q == "" ? new List<ArticuloCatalogo>() : catalogo.Buscar().Where(a => compare.IndexOf(a.Nombre + " " + a.Descripcion + " " + a.CodigoCatalogo + " " + a.CodigoLocal + " " + a.Marca.Nombre + " " + a.Tipo, m.Q, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0).ToList();
        m.Total = encontrados.Count; m.Productos = encontrados.Take(30).ToList(); return View(m);
    }

    [HttpPost("/Presupuesto")]
    public IActionResult GuardarPresupuesto(int id, IFormCollection f)
    {
        var p = presupuestos.Obtener(id, usuario); if (p == null) return NotFound();
        if (Entero(f,"revision") != p.Revision) throw new ArgumentException("La solicitud cambió. Recargá la página para continuar.");
        switch (Texto(f,"accion",20))
        {
            case "guardar": p.Nombre = Texto(f,"nombre",160); p.Local = Texto(f,"local",200); p.Observaciones = Texto(f,"observaciones",1500); break;
            case "agregar":
                var producto = catalogo.Obtener(Entero(f,"producto"));
                var color = catalogo.ResolverColor(producto, EnteroOpcional(f,"color"));
                var cantidad = CantidadEntera(f,"cantidad");
                var existente = p.Items.FirstOrDefault(i => i.ProductoId == producto.Id && i.ColorId == color?.Id);
                if (existente == null) p.Items.Add(PresupuestoNegocio.DesdeProducto(producto, cantidad, Texto(f,"nota",500), color));
                else existente.Cantidad = Math.Min(1000000m, existente.Cantidad + cantidad);
                break;
            case "item": case "quitar":
                var item = p.Items.FirstOrDefault(i => i.Id == Entero(f,"item")); if (item == null) throw new ArgumentException("El renglón ya no existe.");
                if (f["accion"] == "quitar") p.Items.Remove(item);
                else { item.Cantidad = CantidadEntera(f,"cantidad"); item.Observaciones = Texto(f,"nota",500); } break;
            default: throw new ArgumentException("Acción inválida.");
        }
        presupuestos.Guardar(p, usuario); return Redirect("/Presupuesto?id=" + p.Id);
    }

    [HttpGet("/DescargarPdf")]
    public IActionResult DescargarPdf(int id)
    {
        var p = presupuestos.Obtener(id, usuario); if (p == null) return NotFound();
        return File(new PresupuestoPdf().Generar(p), "application/pdf", "solicitud-cotizacion-" + id.ToString("D6") + ".pdf");
    }

    [HttpPost("/Solicitud/RegistrarCompartida")]
    public IActionResult RegistrarSolicitudCompartida(IFormCollection f)
    {
        var guardado = presupuestos.RegistrarCompartida(Entero(f,"id"), usuario, Texto(f,"canal",20));
        return Json(new { ok = true, persisted = guardado });
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
    private static int? ColorOpcional(IFormCollection f) { var id=EnteroOpcional(f,"color"); return id==0 ? null : id; }
    private static decimal CantidadEntera(IFormCollection f,string key)
    {
        var s = Texto(f,key,40).Replace(',','.');
        if (!decimal.TryParse(s,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var cantidad)
            || cantidad < 1 || cantidad > 1000000 || cantidad != decimal.Truncate(cantidad))
            throw new ArgumentException("La cantidad debe ser un número entero entre 1 y 1.000.000.");
        return cantidad;
    }
    private string ClaveCarrito => "carrito-" + usuario.Id;
    private string ObtenerTema()
    {
        var tema = HttpContext.Session.GetString(ClaveTema);
        if (tema is "dark" or "light") return tema;
        HttpContext.Session.SetString(ClaveTema, "dark");
        return "dark";
    }
    private Presupuesto ObtenerCarrito(bool crear = true)
    {
        var persistido = carritos.Obtener(usuario.Id);
        if (persistido != null)
        {
            HttpContext.Session.SetString(ClaveCarrito, JsonSerializer.Serialize(persistido));
            return persistido;
        }
        var json = HttpContext.Session.GetString(ClaveCarrito);
        if (!string.IsNullOrEmpty(json)) return JsonSerializer.Deserialize<Presupuesto>(json) ?? NuevoCarrito();
        return crear ? NuevoCarrito() : null;
    }
    private static Presupuesto NuevoCarrito() => new() { Nombre = "Instalación eléctrica " + DateTime.Now.ToString("yyyyMMdd-HHmm"), Fecha = DateTime.Now };
    private string ConstruirMensaje(Presupuesto solicitud)
    {
        var lineas = new List<string>
        {
            $"Hola{(string.IsNullOrWhiteSpace(proveedor.Nombre) ? "" : " " + proveedor.Nombre)}, quisiera solicitar cotización de los siguientes materiales:",
            ""
        };
        foreach (var item in solicitud.Items.Take(20))
        {
            var color = string.IsNullOrWhiteSpace(item.ColorNombre) ? "" : " · " + item.ColorNombre;
            lineas.Add($"• {item.Nombre}{color} — {item.Cantidad:0} {item.Unidad}");
        }
        if (solicitud.Items.Count > 20) lineas.Add($"• y {solicitud.Items.Count - 20} materiales más en el PDF");
        lineas.Add("");
        lineas.Add($"Solicitud Nº {solicitud.Id:D6} · {solicitud.Nombre}");
        lineas.Add("Adjunto el PDF con el detalle completo.");
        lineas.Add("");
        lineas.Add("Gracias.");
        return string.Join("\n", lineas);
    }
    private void GuardarCarrito(Presupuesto carrito)
    {
        HttpContext.Session.SetString(ClaveCarrito, JsonSerializer.Serialize(carrito));
        carritos.Guardar(usuario.Id, carrito);
    }
}
