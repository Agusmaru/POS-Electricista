using System.Text;
using Dominio;
using Negocio;

namespace Pruebas;

public class ReglasCatalogoTests
{
    private static ArticuloCatalogo ProductoActivo() => new()
    {
        Id = 15,
        Nombre = "Cable unipolar",
        Descripcion = "Cable de prueba",
        Marca = new() { Nombre = "Furnarius" },
        Categoria = new() { Nombre = "Cables" },
        CodigoCatalogo = "CAB-15",
        Tipo = "Cable",
        Unidad = "metro",
        Activo = true
    };

    [Fact]
    public void CrearItemRechazaCantidadesConDecimales()
    {
        Assert.Throws<ArgumentException>(() => PresupuestoNegocio.DesdeProducto(ProductoActivo(), 1.5m, ""));
    }

    [Fact]
    public void CrearItemConservaProductoColorYCantidad()
    {
        var color = new ColorCatalogo { Id = 7, Nombre = "Rojo", Activo = true };
        var item = PresupuestoNegocio.DesdeProducto(ProductoActivo(), 12m, "Para tablero", color);

        Assert.Equal(15, item.ProductoId);
        Assert.Equal(12m, item.Cantidad);
        Assert.Equal(7, item.ColorId);
        Assert.Equal("Rojo", item.ColorNombre);
        Assert.Equal("Para tablero", item.Observaciones);
    }

    [Fact]
    public void PdfUsaMarcaFurnariusYOcultaPrecios()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var presupuesto = new Presupuesto
        {
            Id = 25,
            Nombre = "Obra de prueba",
            Autor = "Asesor",
            SolicitanteEmail = "asesor@furnarius.test",
            Fecha = new DateTime(2026, 10, 1),
            Items = [PresupuestoNegocio.DesdeProducto(ProductoActivo(), 3m, "")]
        };

        var contenido = Encoding.GetEncoding(1252).GetString(new PresupuestoPdf().Generar(presupuesto));

        Assert.Contains("FURNARIUS ENERGY", contenido);
        Assert.Contains("Solicitud de cotización de materiales", contenido);
        Assert.Contains("Obra: Obra de prueba", contenido);
        Assert.Contains("asesor@furnarius.test", contenido);
        Assert.Contains("Por favor, cotizar los materiales detallados.", contenido);
        Assert.DoesNotContain("PRECIO", contenido, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TOTAL", contenido, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModelosNoExponenImportes()
    {
        Assert.Null(typeof(ArticuloCatalogo).GetProperty("PrecioEstimado"));
        Assert.Null(typeof(ItemPresupuesto).GetProperty("PrecioUnitario"));
        Assert.Null(typeof(Presupuesto).GetProperty("Total"));
    }
}
