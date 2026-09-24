using System;
using System.Collections.Generic;
using System.Linq;

namespace Dominio
{
    public class ArticuloCatalogo : Producto
    {
        public string CodigoCatalogo { get; set; }
        public string CodigoLocal { get; set; }
        public string Tipo { get; set; }
        public string Unidad { get; set; }
        public decimal? PrecioEstimado { get; set; }
        public string Imagen { get; set; } = "";
        public string Origen { get; set; }
        public List<ColorCatalogo> Colores { get; set; } = new List<ColorCatalogo>();
        public bool RequiereColor { get { return Colores.Count > 0; } }
    }

    public class ColorCatalogo
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string CodigoHex { get; set; }
        public bool Activo { get; set; }
        public int Orden { get; set; }
    }

    public class UsuarioCatalogo
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public bool Admin { get; set; }
        public bool Activo { get; set; }
    }

    public class ItemPresupuesto
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Marca { get; set; }
        public string CodigoCatalogo { get; set; }
        public string CodigoLocal { get; set; }
        public string Tipo { get; set; }
        public string Unidad { get; set; }
        public string Imagen { get; set; } = "";
        public decimal Cantidad { get; set; }
        public int? ColorId { get; set; }
        public string ColorNombre { get; set; } = "";
        public decimal? PrecioUnitario { get; set; }
        public string Observaciones { get; set; }
        public decimal? Subtotal { get { return PrecioUnitario.HasValue ? Math.Round(Cantidad * PrecioUnitario.Value, 2, MidpointRounding.AwayFromZero) : (decimal?)null; } }
    }

    public class Presupuesto
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Autor { get; set; }
        public string Nombre { get; set; }
        public string Local { get; set; }
        public string Observaciones { get; set; }
        public DateTime Fecha { get; set; }
        public bool EsPrueba { get; set; }
        public int Revision { get; set; }
        public List<ItemPresupuesto> Items { get; set; } = new List<ItemPresupuesto>();
        public decimal Total { get { return Items.Sum(x => x.Subtotal ?? 0m); } }
        public bool Completo { get { return Items.Count > 0 && Items.All(x => x.PrecioUnitario.HasValue); } }
        public const string Aviso = "Lista de materiales orientativa. Verificá cantidades, especificaciones y disponibilidad antes de realizar la compra.";
    }
}
