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
            return lista.OrderBy(x => x.Nombre, orden).ThenBy(x => x.Marca.Nombre, orden)
                .ThenBy(x => x.CodigoCatalogo, orden).ThenBy(x => x.Id).ToList();
        }
        public ArticuloCatalogo Obtener(int id)
        {
            using (var c = CatalogoDatos.Abrir())
            using (var cmd = CatalogoDatos.Comando(c, "SELECT * FROM EC_Productos WHERE Id=@id", "@id", id))
            using (var r = cmd.ExecuteReader()) { return r.Read() ? Leer(r) : null; }
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
