using Dominio;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace Negocio
{
    public class CatalogoNegocio
    {
        public static void PrepararEtapa2()
        {
            using var c = CatalogoDatos.Abrir();
            using var cmd = CatalogoDatos.Comando(c, @"IF COL_LENGTH('EC_Productos','Imagen') IS NULL ALTER TABLE EC_Productos ADD Imagen nvarchar(260) NOT NULL CONSTRAINT DF_EC_Productos_Imagen DEFAULT '';
                UPDATE EC_Productos SET PrecioEstimado=0 WHERE PrecioEstimado IS NULL;");
            cmd.ExecuteNonQuery();
        }
        public static void PrepararColores()
        {
            using var c = CatalogoDatos.Abrir();
            using var cmd = CatalogoDatos.Comando(c, @"
IF OBJECT_ID('EC_Colores') IS NULL
BEGIN
    CREATE TABLE EC_Colores(
        Id int IDENTITY PRIMARY KEY,
        Nombre nvarchar(60) NOT NULL,
        CodigoHex varchar(7) NOT NULL,
        Activo bit NOT NULL CONSTRAINT DF_EC_Colores_Activo DEFAULT 1,
        Orden int NOT NULL,
        CONSTRAINT UQ_EC_Colores_Nombre UNIQUE(Nombre)
    );
END;
IF OBJECT_ID('EC_ProductoColores') IS NULL
BEGIN
    CREATE TABLE EC_ProductoColores(
        ProductoId int NOT NULL REFERENCES EC_Productos(Id),
        ColorId int NOT NULL REFERENCES EC_Colores(Id),
        CONSTRAINT PK_EC_ProductoColores PRIMARY KEY(ProductoId,ColorId)
    );
END;
IF COL_LENGTH('EC_Items','ColorId') IS NULL ALTER TABLE EC_Items ADD ColorId int NULL;
IF COL_LENGTH('EC_Items','ColorNombre') IS NULL ALTER TABLE EC_Items ADD ColorNombre nvarchar(60) NOT NULL CONSTRAINT DF_EC_Items_ColorNombre DEFAULT '';
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_EC_Items_Colores')
    ALTER TABLE EC_Items ADD CONSTRAINT FK_EC_Items_Colores FOREIGN KEY(ColorId) REFERENCES EC_Colores(Id);

MERGE EC_Colores AS destino
USING (VALUES
    (N'Marrón','#795548',1),(N'Negro','#161616',2),(N'Rojo','#D32F2F',3),
    (N'Azul','#1976D2',4),(N'Verde-Amarillo','#8BAF24',5),(N'Blanco','#F4F4F4',6)
) AS origen(Nombre,CodigoHex,Orden) ON destino.Nombre=origen.Nombre
WHEN MATCHED THEN UPDATE SET CodigoHex=origen.CodigoHex,Orden=origen.Orden
WHEN NOT MATCHED THEN INSERT(Nombre,CodigoHex,Activo,Orden) VALUES(origen.Nombre,origen.CodigoHex,1,origen.Orden);

INSERT EC_ProductoColores(ProductoId,ColorId)
SELECT p.Id,c.Id FROM EC_Productos p CROSS JOIN EC_Colores c
WHERE p.Activo=1 AND c.Activo=1
  AND (LOWER(LTRIM(RTRIM(p.Nombre)))=N'cable unipolar' OR p.CodigoCatalogo LIKE N'UNIP-%')
  AND NOT EXISTS(SELECT 1 FROM EC_ProductoColores pc WHERE pc.ProductoId=p.Id AND pc.ColorId=c.Id);");
            cmd.ExecuteNonQuery();
        }
        public List<ArticuloCatalogo> Buscar(string texto = "", string marca = "", string tipo = "", bool inactivos = false)
        {
            var lista = new List<ArticuloCatalogo>();
            using (var c = CatalogoDatos.Abrir())
            using (var cmd = CatalogoDatos.Comando(c, @"SELECT * FROM EC_Productos WHERE (@todos=1 OR Activo=1)
                AND (@q='' OR CHARINDEX(@q,CONCAT(Nombre,' ',Descripcion,' ',Marca,' ',Categoria,' ',CodigoCatalogo,' ',CodigoLocal,' ',Tipo))>0)
                AND (@marca='' OR Marca=@marca) AND (@tipo='' OR Tipo=@tipo)",
                "@todos", inactivos, "@q", texto ?? "", "@marca", marca ?? "", "@tipo", tipo ?? ""))
            using (var r = cmd.ExecuteReader()) { while (r.Read()) lista.Add(Leer(r)); }
            // El catálogo inicial es pequeño. Ordenarlo aquí evita que SQL Express
            // deba reservar memoria para ordenar filas anchas (RESOURCE_SEMAPHORE).
            var orden = StringComparer.Create(CultureInfo.GetCultureInfo("es-AR"), true);
            lista = lista.OrderBy(x => x.Nombre, orden).ThenBy(x => x.Marca.Nombre, orden)
                .ThenBy(x => x.CodigoCatalogo, orden).ThenBy(x => x.Id).ToList();
            CargarColores(lista);
            return lista;
        }
        public ArticuloCatalogo Obtener(int id)
        {
            using (var c = CatalogoDatos.Abrir())
            using (var cmd = CatalogoDatos.Comando(c, "SELECT * FROM EC_Productos WHERE Id=@id", "@id", id))
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                var producto = Leer(r);
                r.Close();
                CargarColores(new List<ArticuloCatalogo> { producto }, c);
                return producto;
            }
        }
        private static void CargarColores(List<ArticuloCatalogo> productos, SqlConnection conexion = null)
        {
            if (productos == null || productos.Count == 0) return;
            var porId = productos.ToDictionary(p => p.Id);
            var propia = conexion == null;
            var c = conexion ?? CatalogoDatos.Abrir();
            try
            {
                using var cmd = CatalogoDatos.Comando(c, @"SELECT pc.ProductoId,c.Id,c.Nombre,c.CodigoHex,c.Activo,c.Orden
                    FROM EC_ProductoColores pc JOIN EC_Colores c ON c.Id=pc.ColorId
                    WHERE c.Activo=1 ORDER BY c.Orden,c.Nombre");
                using var r = cmd.ExecuteReader();
                while (r.Read() && porId.Count > 0)
                    if (porId.TryGetValue((int)r["ProductoId"], out var producto))
                        producto.Colores.Add(new ColorCatalogo { Id=(int)r["Id"], Nombre=(string)r["Nombre"], CodigoHex=(string)r["CodigoHex"], Activo=(bool)r["Activo"], Orden=(int)r["Orden"] });
            }
            finally { if (propia) c.Dispose(); }
        }
        public ColorCatalogo ResolverColor(ArticuloCatalogo producto, int colorId)
        {
            if (producto == null || !producto.Activo) throw new ArgumentException("El producto no está disponible en el catálogo.");
            if (!producto.RequiereColor)
            {
                if (colorId > 0) throw new ArgumentException("Este producto no admite selección de color.");
                return null;
            }
            if (colorId <= 0) throw new ArgumentException("Elegí un color para el cable unipolar.");
            return producto.Colores.FirstOrDefault(c => c.Id == colorId && c.Activo)
                ?? throw new ArgumentException("El color elegido no está disponible para este cable.");
        }
        private ArticuloCatalogo Leer(SqlDataReader r)
        {
            return new ArticuloCatalogo { Id=(int)r["Id"], Nombre=(string)r["Nombre"], Descripcion=(string)r["Descripcion"],
                Marca=new Marca { Nombre=(string)r["Marca"] }, Categoria=new Categoria { Nombre=(string)r["Categoria"] },
                CodigoCatalogo=(string)r["CodigoCatalogo"], CodigoLocal=(string)r["CodigoLocal"], Tipo=(string)r["Tipo"],
                Unidad=(string)r["Unidad"], PrecioEstimado=CatalogoDatos.Precio(r,"PrecioEstimado"), Imagen=CatalogoDatos.Texto(r,"Imagen"), Activo=(bool)r["Activo"], Origen=(string)r["Origen"] };
        }
        public int Guardar(ArticuloCatalogo p)
        {
            if (string.IsNullOrWhiteSpace(p.Nombre) || string.IsNullOrWhiteSpace(p.Marca.Nombre)) throw new ArgumentException("Completá producto y marca.");
            if (p.PrecioEstimado < 0 || p.PrecioEstimado > 1000000000m) throw new ArgumentException("Precio fuera del rango permitido.");
            using (var c = CatalogoDatos.Abrir())
            using (var cmd = CatalogoDatos.Comando(c, p.Id == 0 ? @"INSERT EC_Productos (Nombre,Descripcion,Marca,Categoria,CodigoCatalogo,CodigoLocal,Tipo,Unidad,PrecioEstimado,Imagen,Activo,Origen)
                OUTPUT INSERTED.Id VALUES (@n,@d,@m,@cat,@cod,@local,@tipo,@u,@p,@img,@a,N'Carga manual')" : @"UPDATE EC_Productos SET Nombre=@n,Descripcion=@d,Marca=@m,Categoria=@cat,CodigoCatalogo=@cod,CodigoLocal=@local,Tipo=@tipo,Unidad=@u,PrecioEstimado=@p,Imagen=@img,Activo=@a OUTPUT INSERTED.Id WHERE Id=@id",
                "@id",p.Id,"@n",p.Nombre,"@d",p.Descripcion,"@m",p.Marca.Nombre,"@cat",p.Categoria.Nombre,"@cod",p.CodigoCatalogo,"@local",p.CodigoLocal,"@tipo",p.Tipo,"@u",p.Unidad,"@p",p.PrecioEstimado,"@img",p.Imagen ?? "","@a",p.Activo))
            { var id = cmd.ExecuteScalar(); if(id==null) throw new ArgumentException("Producto inexistente."); return Convert.ToInt32(id); }
        }
    }
}
