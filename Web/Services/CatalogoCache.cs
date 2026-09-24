using System.Security.Cryptography;
using System.Text;
using Dominio;
using Negocio;

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
    private List<ArticuloCatalogo> todos = [];
    private List<ArticuloCatalogo> activos = [];
    private string version = "";
    private DateTime cargadoHastaUtc = DateTime.MinValue;

    public CatalogoSnapshot Obtener(bool incluirInactivos)
    {
        lock (sincronizacion)
        {
            if (DateTime.UtcNow >= cargadoHastaUtc) Recargar();
            return new CatalogoSnapshot
            {
                Version = version,
                Productos = incluirInactivos ? todos : activos
            };
        }
    }

    public void Invalidar()
    {
        lock (sincronizacion) cargadoHastaUtc = DateTime.MinValue;
    }

    private void Recargar()
    {
        todos = new CatalogoNegocio().Buscar(inactivos: true);
        activos = todos.Where(producto => producto.Activo).ToList();
        version = CalcularVersion(todos);
        cargadoHastaUtc = DateTime.UtcNow.Add(Vigencia);
    }

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
