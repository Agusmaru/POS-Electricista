using Dominio;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Negocio
{
    public class PresupuestoNegocio
    {
        public List<Presupuesto> Listar(UsuarioCatalogo usuario)
        {
            var lista=new List<Presupuesto>();
            using(var c=CatalogoDatos.Abrir()) using(var cmd=CatalogoDatos.Comando(c,@"SELECT p.*,u.Nombre Autor FROM EC_Presupuestos p JOIN EC_Usuarios u ON u.Id=p.UsuarioId WHERE p.Activo=1 AND (p.UsuarioId=@u OR @admin=1) ORDER BY p.Id DESC","@u",usuario.Id,"@admin",usuario.Admin))
            using(var r=cmd.ExecuteReader()) while(r.Read())lista.Add(Leer(r));
            return lista;
        }
        private Presupuesto Leer(SqlDataReader r)
        { return new Presupuesto{Id=(int)r["Id"],UsuarioId=(int)r["UsuarioId"],Autor=(string)r["Autor"],Nombre=(string)r["Nombre"],Local=(string)r["Local"],Observaciones=(string)r["Observaciones"],Fecha=(DateTime)r["Fecha"],EsPrueba=(bool)r["EsPrueba"],Revision=(int)r["Revision"]}; }
        public Presupuesto Obtener(int id,UsuarioCatalogo usuario)
        {
            Presupuesto p;
            using(var c=CatalogoDatos.Abrir())
            {
                using(var cmd=CatalogoDatos.Comando(c,@"SELECT p.*,u.Nombre Autor FROM EC_Presupuestos p JOIN EC_Usuarios u ON u.Id=p.UsuarioId WHERE p.Id=@id AND p.Activo=1 AND (p.UsuarioId=@u OR @admin=1)","@id",id,"@u",usuario.Id,"@admin",usuario.Admin))
                using(var r=cmd.ExecuteReader()){if(!r.Read())return null;p=Leer(r);}
                using(var cmd=CatalogoDatos.Comando(c,"SELECT * FROM EC_Items WHERE PresupuestoId=@id ORDER BY Id","@id",id))
                using(var r=cmd.ExecuteReader())while(r.Read())p.Items.Add(new ItemPresupuesto{Id=(int)r["Id"],ProductoId=(int)r["ProductoId"],Nombre=(string)r["Nombre"],Descripcion=(string)r["Descripcion"],Marca=(string)r["Marca"],CodigoCatalogo=(string)r["CodigoCatalogo"],CodigoLocal=(string)r["CodigoLocal"],Tipo=(string)r["Tipo"],Unidad=(string)r["Unidad"],Cantidad=(decimal)r["Cantidad"],PrecioUnitario=CatalogoDatos.Precio(r,"PrecioUnitario"),Observaciones=(string)r["Observaciones"]});
            }
            return p;
        }
        public static ItemPresupuesto DesdeProducto(ArticuloCatalogo a,decimal cantidad,decimal? precio,string observaciones)
        {
            if(a==null||!a.Activo)throw new ArgumentException("El producto no está disponible en el catálogo.");
            return new ItemPresupuesto{ProductoId=a.Id,Nombre=a.Nombre,Descripcion=a.Descripcion,Marca=a.Marca.Nombre,CodigoCatalogo=a.CodigoCatalogo,CodigoLocal=a.CodigoLocal,Tipo=a.Tipo,Unidad=a.Unidad,Imagen=a.Imagen??"",Cantidad=cantidad,PrecioUnitario=precio,Observaciones=observaciones??""};
        }
        public int Guardar(Presupuesto p,UsuarioCatalogo usuario)
        {
            if(string.IsNullOrWhiteSpace(p.Nombre)||p.Nombre.Length>160)throw new ArgumentException("Ingresá un nombre de hasta 160 caracteres.");
            if(p.Items.Count>300)throw new ArgumentException("Se permiten hasta 300 renglones por presupuesto.");
            foreach(var x in p.Items)
            { if(x.Cantidad<=0||x.Cantidad>1000000||decimal.Round(x.Cantidad,3)!=x.Cantidad)throw new ArgumentException("La cantidad debe ser mayor a cero, hasta 1.000.000 y con hasta 3 decimales.");
              if(x.PrecioUnitario<0||x.PrecioUnitario>1000000000|| (x.PrecioUnitario.HasValue && decimal.Round(x.PrecioUnitario.Value,2)!=x.PrecioUnitario.Value))throw new ArgumentException("Precio inválido: usá un valor positivo o cero, con hasta dos decimales."); }
            using(var c=CatalogoDatos.Abrir()) using(var tx=c.BeginTransaction())
            {
                try
                {
                    string sql=p.Id==0?@"INSERT EC_Presupuestos(UsuarioId,Nombre,Local,Observaciones,EsPrueba) OUTPUT INSERTED.Id VALUES(@u,@n,@l,@o,@test)":@"UPDATE EC_Presupuestos SET Nombre=@n,Local=@l,Observaciones=@o,Revision=Revision+1 OUTPUT INSERTED.Id WHERE Id=@id AND Activo=1 AND Revision=@rev AND (UsuarioId=@u OR @admin=1)";
                    using(var cmd=CatalogoDatos.Comando(c,sql,"@u",usuario.Id,"@admin",usuario.Admin,"@n",p.Nombre,"@l",p.Local??"","@o",p.Observaciones??"","@test",p.EsPrueba,"@id",p.Id,"@rev",p.Revision))
                    {cmd.Transaction=tx;var id=cmd.ExecuteScalar();if(id==null)throw new ArgumentException("El presupuesto cambió o no tenés acceso. Recargá la página antes de editar.");p.Id=Convert.ToInt32(id);}
                    using(var cmd=CatalogoDatos.Comando(c,"DELETE EC_Items WHERE PresupuestoId=@id","@id",p.Id)){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
                    foreach(var x in p.Items)
                    using(var cmd=CatalogoDatos.Comando(c,@"INSERT EC_Items(PresupuestoId,ProductoId,Nombre,Descripcion,Marca,CodigoCatalogo,CodigoLocal,Tipo,Unidad,Cantidad,PrecioUnitario,Observaciones) VALUES(@id,@p,@n,@d,@m,@c,@l,@t,@u,@q,@v,@o)",
                        "@id",p.Id,"@p",x.ProductoId,"@n",x.Nombre,"@d",x.Descripcion,"@m",x.Marca,"@c",x.CodigoCatalogo,"@l",x.CodigoLocal,"@t",x.Tipo,"@u",x.Unidad,"@q",x.Cantidad,"@v",x.PrecioUnitario,"@o",x.Observaciones??""))
                    {cmd.Transaction=tx;cmd.ExecuteNonQuery();}
                    tx.Commit();return p.Id;
                }
                catch{tx.Rollback();throw;}
            }
        }
        public int Duplicar(int id,UsuarioCatalogo usuario)
        {var p=Obtener(id,usuario);if(p==null)throw new ArgumentException("Presupuesto no disponible.");p.Id=0;p.Nombre=(p.Nombre.Length>145?p.Nombre.Substring(0,145):p.Nombre)+" (copia)";return Guardar(p,usuario);}
        public void Eliminar(int id,UsuarioCatalogo usuario,int revision)
        {
            using(var c=CatalogoDatos.Abrir())using(var cmd=CatalogoDatos.Comando(c,"UPDATE EC_Presupuestos SET Activo=0,Revision=Revision+1 WHERE Id=@id AND Revision=@r AND (UsuarioId=@u OR @a=1)","@id",id,"@r",revision,"@u",usuario.Id,"@a",usuario.Admin))
                if(cmd.ExecuteNonQuery()!=1)throw new ArgumentException("El presupuesto cambió o no tenés acceso. Recargá la página.");
        }
    }
}
