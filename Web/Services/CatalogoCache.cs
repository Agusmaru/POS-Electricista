using System.Security.Cryptography;
using System.Text;
using Dominio;
using Negocio;
using Microsoft.Data.SqlClient;

namespace Flux.Web.Services;

public sealed class CatalogoSnapshot
{
    public string Version { get; init; } = "";
    public IReadOnlyList<ArticuloCatalogo> Productos { get; init; } = [];
}

public sealed class CatalogoCache
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);
    private readonly object sincronizacion = new();
    private readonly SemaphoreSlim recarga = new(1, 1);
    private List<ArticuloCatalogo> todos = [];
    private List<ArticuloCatalogo> activos = [];
    private string version = "";
    private DateTime cargadoHastaUtc = DateTime.MinValue;

    public CatalogoSnapshot Obtener(bool incluirInactivos)
    {
        bool vencido, hayDatos;
        lock (sincronizacion)
        {
            vencido = DateTime.UtcNow >= cargadoHastaUtc;
            hayDatos = todos.Count > 0;
        }
        if (vencido)
        {
            if (recarga.Wait(0))
            {
                try
                {
                    lock (sincronizacion) vencido = DateTime.UtcNow >= cargadoHastaUtc;
                    if (vencido) Recargar();
                }
                finally { recarga.Release(); }
            }
            else if (!hayDatos)
            {
                recarga.Wait();
                recarga.Release();
            }
        }
        lock (sincronizacion)
        {
            return new CatalogoSnapshot
            {
                Version = version,
                Productos = (incluirInactivos ? todos : activos).Select(Copiar).ToList()
            };
        }
    }

    public void Invalidar()
    {
        lock (sincronizacion) cargadoHastaUtc = DateTime.MinValue;
    }

    private void Recargar()
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                var nuevosTodos = new CatalogoNegocio().Buscar(inactivos: true);
                var nuevosActivos = nuevosTodos.Where(producto => producto.Activo).ToList();
                var nuevaVersion = CalcularVersion(nuevosTodos);
                lock (sincronizacion)
                {
                    todos = nuevosTodos;
                    activos = nuevosActivos;
                    version = nuevaVersion;
                    cargadoHastaUtc = DateTime.UtcNow.Add(Vigencia);
                }
                return;
            }
            catch (SqlException ex) when (ex.Number == -2 && intento == 1)
            {
                Thread.Sleep(250);
            }
        }
    }

    private static ArticuloCatalogo Copiar(ArticuloCatalogo producto) => new()
    {
        Id = producto.Id,
        Nombre = producto.Nombre,
        Descripcion = producto.Descripcion,
        Marca = new() { Id = producto.Marca.Id, Nombre = producto.Marca.Nombre, Descripcion = producto.Marca.Descripcion, Activo = producto.Marca.Activo },
        Categoria = new() { Id = producto.Categoria.Id, Nombre = producto.Categoria.Nombre, Descripcion = producto.Categoria.Descripcion, Activo = producto.Categoria.Activo },
        CodigoCatalogo = producto.CodigoCatalogo,
        CodigoLocal = producto.CodigoLocal,
        Tipo = producto.Tipo,
        Unidad = producto.Unidad,
        Imagen = producto.Imagen,
        Origen = producto.Origen,
        Activo = producto.Activo,
        Colores = producto.Colores.Select(color => new ColorCatalogo { Id = color.Id, Nombre = color.Nombre, CodigoHex = color.CodigoHex, Activo = color.Activo, Orden = color.Orden }).ToList(),
        Fotos = producto.Fotos.Select(foto => new FotoProducto { Id = foto.Id, NombreArchivo = foto.NombreArchivo, Orden = foto.Orden, EsPrincipal = foto.EsPrincipal }).ToList()
    };

    private static string CalcularVersion(IEnumerable<ArticuloCatalogo> productos)
    {
        var contenido = new StringBuilder();
        foreach (var producto in productos)
        {
            contenido.Append(producto.Id).Append('|').Append(producto.Nombre).Append('|')
                .Append(producto.CodigoCatalogo).Append('|').Append(producto.CodigoLocal).Append('|')
                .Append(producto.Marca.Nombre).Append('|').Append(producto.Categoria.Nombre).Append('|')
                .Append(producto.Descripcion).Append('|').Append(producto.Tipo).Append('|')
                .Append(producto.Unidad).Append('|').Append(producto.Imagen).Append('|')
                .Append(producto.Activo ? '1' : '0');
            foreach (var color in producto.Colores)
                contenido.Append('|').Append(color.Id).Append(':').Append(color.Nombre).Append(':').Append(color.CodigoHex);
            contenido.AppendLine();
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenido.ToString())))[..16];
    }
}
