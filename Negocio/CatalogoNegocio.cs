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
            CargarFotos(lista);
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
                CargarFotos(new List<ArticuloCatalogo> { producto }, c);
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
        private static void CargarFotos(List<ArticuloCatalogo> productos, SqlConnection conexion = null)
        {
            if (productos == null || productos.Count == 0) return;
            var porId = productos.ToDictionary(p => p.Id);
            var propia = conexion == null;
            var c = conexion ?? CatalogoDatos.Abrir();
            try
            {
                using var cmd = CatalogoDatos.Comando(c, @"SELECT pf.ProductoId,f.Id,f.NombreArchivo,pf.Orden,pf.EsPrincipal
                    FROM EC_ProductosFotos pf JOIN EC_Fotos f ON f.Id=pf.FotoId
                    ORDER BY pf.ProductoId,pf.EsPrincipal DESC,pf.Orden,f.Id");
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    if (porId.TryGetValue((int)r["ProductoId"], out var producto))
                        producto.Fotos.Add(new FotoProducto { Id=(int)r["Id"], NombreArchivo=(string)r["NombreArchivo"], Orden=(int)r["Orden"], EsPrincipal=(bool)r["EsPrincipal"] });
                foreach (var producto in productos)
                    if (producto.Fotos.Count > 0)
                        producto.Imagen = producto.Fotos.OrderByDescending(f => f.EsPrincipal).ThenBy(f => f.Orden).ThenBy(f => f.Id).First().NombreArchivo;
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
                Unidad=(string)r["Unidad"], Imagen=CatalogoDatos.Texto(r,"Imagen"), Activo=(bool)r["Activo"], Origen=(string)r["Origen"] };
        }
        public int Guardar(ArticuloCatalogo p)
        {
            if (string.IsNullOrWhiteSpace(p.Nombre) || string.IsNullOrWhiteSpace(p.Marca.Nombre)) throw new ArgumentException("Completá producto y marca.");
            using (var c = CatalogoDatos.Abrir())
            using (var cmd = CatalogoDatos.Comando(c, p.Id == 0 ? @"INSERT EC_Productos (Nombre,Descripcion,Marca,Categoria,CodigoCatalogo,CodigoLocal,Tipo,Unidad,PrecioEstimado,Imagen,Activo,Origen)
                OUTPUT INSERTED.Id VALUES (@n,@d,@m,@cat,@cod,@local,@tipo,@u,0,@img,@a,N'Carga manual')" : @"UPDATE EC_Productos SET Nombre=@n,Descripcion=@d,Marca=@m,Categoria=@cat,CodigoCatalogo=@cod,CodigoLocal=@local,Tipo=@tipo,Unidad=@u,Imagen=@img,Activo=@a OUTPUT INSERTED.Id WHERE Id=@id",
                "@id",p.Id,"@n",p.Nombre,"@d",p.Descripcion,"@m",p.Marca.Nombre,"@cat",p.Categoria.Nombre,"@cod",p.CodigoCatalogo,"@local",p.CodigoLocal,"@tipo",p.Tipo,"@u",p.Unidad,"@img",p.Imagen ?? "","@a",p.Activo))
            { var id = cmd.ExecuteScalar(); if(id==null) throw new ArgumentException("Producto inexistente."); return Convert.ToInt32(id); }
        }
        public void CambiarEstado(int productoId, bool activo)
        {
            using var c = CatalogoDatos.Abrir();
            using var cmd = CatalogoDatos.Comando(c,
                "UPDATE EC_Productos SET Activo=@activo WHERE Id=@id",
                "@activo", activo, "@id", productoId);
            if (cmd.ExecuteNonQuery() != 1) throw new ArgumentException("El producto ya no existe.");
        }
        public void GuardarFotos(int productoId, IEnumerable<string> nuevas, IEnumerable<int> quitar, int? principalId)
        {
            using var c = CatalogoDatos.Abrir();
            using var tx = c.BeginTransaction();
            try
            {
                foreach (var fotoId in (quitar ?? Enumerable.Empty<int>()).Distinct())
                {
                    using var quitarCmd = CatalogoDatos.Comando(c,
                        "DELETE EC_ProductosFotos WHERE ProductoId=@producto AND FotoId=@foto",
                        "@producto", productoId, "@foto", fotoId);
                    quitarCmd.Transaction = tx;
                    quitarCmd.ExecuteNonQuery();
                }

                int orden;
                using (var ordenCmd = CatalogoDatos.Comando(c,
                    "SELECT ISNULL(MAX(Orden),0) FROM EC_ProductosFotos WHERE ProductoId=@producto",
                    "@producto", productoId))
                { ordenCmd.Transaction = tx; orden = Convert.ToInt32(ordenCmd.ExecuteScalar()); }

                foreach (var archivo in (nuevas ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    int fotoId;
                    using (var fotoCmd = CatalogoDatos.Comando(c,
                        @"IF NOT EXISTS(SELECT 1 FROM EC_Fotos WHERE NombreArchivo=@archivo)
                            INSERT EC_Fotos(NombreArchivo) VALUES(@archivo);
                          SELECT Id FROM EC_Fotos WHERE NombreArchivo=@archivo;",
                        "@archivo", archivo))
                    { fotoCmd.Transaction = tx; fotoId = Convert.ToInt32(fotoCmd.ExecuteScalar()); }
                    using var vincularCmd = CatalogoDatos.Comando(c,
                        @"IF NOT EXISTS(SELECT 1 FROM EC_ProductosFotos WHERE ProductoId=@producto AND FotoId=@foto)
                            INSERT EC_ProductosFotos(ProductoId,FotoId,Orden,EsPrincipal) VALUES(@producto,@foto,@orden,0);",
                        "@producto", productoId, "@foto", fotoId, "@orden", ++orden);
                    vincularCmd.Transaction = tx;
                    vincularCmd.ExecuteNonQuery();
                }

                using (var cantidadCmd = CatalogoDatos.Comando(c,
                    "SELECT COUNT(*) FROM EC_ProductosFotos WHERE ProductoId=@producto",
                    "@producto", productoId))
                {
                    cantidadCmd.Transaction = tx;
                    if (Convert.ToInt32(cantidadCmd.ExecuteScalar()) > 12)
                        throw new ArgumentException("Un producto puede tener hasta 12 fotos asociadas.");
                }

                using (var limpiarPrincipal = CatalogoDatos.Comando(c,
                    "UPDATE EC_ProductosFotos SET EsPrincipal=0 WHERE ProductoId=@producto",
                    "@producto", productoId))
                { limpiarPrincipal.Transaction = tx; limpiarPrincipal.ExecuteNonQuery(); }

                using (var establecerPrincipal = CatalogoDatos.Comando(c,
                    @"UPDATE EC_ProductosFotos SET EsPrincipal=1
                      WHERE ProductoId=@producto AND FotoId=COALESCE(
                        (SELECT FotoId FROM EC_ProductosFotos WHERE ProductoId=@producto AND FotoId=@principal),
                        (SELECT TOP 1 FotoId FROM EC_ProductosFotos WHERE ProductoId=@producto ORDER BY Orden,FotoId));",
                    "@producto", productoId, "@principal", principalId))
                { establecerPrincipal.Transaction = tx; establecerPrincipal.ExecuteNonQuery(); }

                using (var compatibilidad = CatalogoDatos.Comando(c,
                    @"UPDATE p SET Imagen=ISNULL((SELECT TOP 1 f.NombreArchivo
                        FROM EC_ProductosFotos pf JOIN EC_Fotos f ON f.Id=pf.FotoId
                        WHERE pf.ProductoId=p.Id ORDER BY pf.EsPrincipal DESC,pf.Orden,pf.FotoId),'')
                      FROM EC_Productos p WHERE p.Id=@producto;",
                    "@producto", productoId))
                { compatibilidad.Transaction = tx; compatibilidad.ExecuteNonQuery(); }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }
    }
}
