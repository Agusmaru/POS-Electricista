namespace Dominio;

// Se conserva la identidad del producto y se retiran los campos de stock y precio
// del antiguo POS.
public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public Marca Marca { get; set; } = new();
    public Categoria Categoria { get; set; } = new();
    public string Descripcion { get; set; }
    public bool Activo { get; set; }
}
