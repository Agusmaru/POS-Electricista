namespace Dominio;

public sealed class RenglonComercial
{
    public string Articulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal Cantidad { get; set; } = 1;
    public decimal Precio { get; set; }
    public decimal Alicuota { get; set; } = 21;
    public decimal Neto => decimal.Round(Cantidad * Precio, 2, MidpointRounding.AwayFromZero);
    public decimal Iva => decimal.Round(Neto * Alicuota / 100, 2, MidpointRounding.AwayFromZero);
    public decimal Total => Neto + Iva;
}

public sealed class PresupuestoComercial
{
    public string Numero { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow.AddHours(-3).Date;
    public string Cliente { get; set; } = "";
    public string Cuit { get; set; } = "";
    public string Domicilio { get; set; } = "";
    public string Localidad { get; set; } = "";
    public string Asunto { get; set; } = "";
    public string Contactos { get; set; } = "glopez@febel.com.ar · 1165296077\nmgomez@furnariusenergy.com · 1144722202\ntecnica@furnariusenergy.com · 1133564720";
    public string ManoObraDescripcion { get; set; } = "Mano de Obra Especializada (0,09 USD/W)";
    public decimal ManoObra { get; set; } = 0;
    public string Condiciones { get; set; } = "A efectos de cobranza, el importe a pagar en pesos (ARS) se ajustará por la cotización Dólar billete Banco Nación tipo vendedor del día inmediato al del efectuado el pago.";
    public string Nota { get; set; } = "Nuestra empresa garantiza que el diseño e instalación de este sistema solar fotovoltaico se rige estrictamente por los lineamientos del Régimen de Generación Distribuida (Ley 27.424) y las disposiciones de la Secretaría de Energía de la Nación, utilizando exclusivamente componentes certificados bajo normas internacionales (IEC) para asegurar la máxima seguridad y eficiencia operativa.";
    public List<RenglonComercial> Items { get; set; } = [];
    public decimal Neto => Items.Sum(x => x.Neto);
    public decimal Iva => Items.Sum(x => x.Iva);
    public decimal Total => Items.Sum(x => x.Total) + ManoObra;
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Cliente) || string.IsNullOrWhiteSpace(Numero) || string.IsNullOrWhiteSpace(Asunto)) throw new ArgumentException("Completá número, cliente y asunto.");
        if (Fecha.Year < 2000 || Fecha.Year > 2100) throw new ArgumentException("La fecha no es válida.");
        foreach (var t in new[] { Numero, Cliente, Cuit, Domicilio, Localidad, Asunto, ManoObraDescripcion })
            if (t == null || t.Length > 250) throw new ArgumentException("Los datos del encabezado admiten hasta 250 caracteres.");
        foreach (var t in new[] { Contactos, Condiciones, Nota })
            if (t == null || t.Length > 1500) throw new ArgumentException("Los textos adicionales admiten hasta 1500 caracteres.");
        if (Items == null || Items.Count is < 1 or > 100) throw new ArgumentException("Ingresá entre 1 y 100 filas.");
        if (ManoObra < 0 || ManoObra > 100000000 || decimal.Round(ManoObra, 2) != ManoObra) throw new ArgumentException("La mano de obra admite hasta dos decimales y no puede ser negativa.");
        foreach (var x in Items)
        {
            if (x == null || string.IsNullOrWhiteSpace(x.Articulo) || x.Articulo.Length > 100 || x.Descripcion == null || x.Descripcion.Length > 500) throw new ArgumentException("Cada fila necesita un artículo (hasta 100 caracteres) y una descripción de hasta 500 caracteres.");
            if (x.Cantidad <= 0 || x.Cantidad > 1000000 || decimal.Round(x.Cantidad, 3) != x.Cantidad || x.Precio < 0 || x.Precio > 100000000 || decimal.Round(x.Precio, 2) != x.Precio) throw new ArgumentException("Revisá cantidades (hasta 3 decimales) y precios (hasta 2 decimales).");
            if (x.Alicuota is not (0m or 10.5m or 21m or 27m)) throw new ArgumentException("Seleccioná una alícuota de IVA válida.");
        }
    }
    public static PresupuestoComercial Plantilla() => new();
}
