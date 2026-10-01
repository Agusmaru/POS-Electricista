using Dominio;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Negocio
{
    public sealed class PaginaPresupuestos
    {
        public List<Presupuesto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Normales { get; set; }
        public int Pruebas { get; set; }
    }

    public class PresupuestoNegocio
    {
        public PaginaPresupuestos Listar(UsuarioCatalogo usuario,string texto="",string tipo="",string orden="recientes",int pagina=1,int tamano=20)
        {
            var resultado=new PaginaPresupuestos();
            texto=(texto??"").Trim();pagina=Math.Max(1,pagina);tamano=Math.Clamp(tamano,1,100);
            var where=@" FROM EC_Presupuestos p JOIN EC_Usuarios u ON u.Id=p.UsuarioId
                WHERE p.Activo=1 AND (p.UsuarioId=@u OR @admin=1)
                AND (@q='' OR CHARINDEX(@q,CONCAT(RIGHT('000000'+CONVERT(varchar(12),p.Id),6),' ',p.Nombre,' ',u.Nombre))>0)
                AND (@tipo='' OR p.EsPrueba=CASE WHEN @tipo='prueba' THEN 1 ELSE 0 END)";
            using var c=CatalogoDatos.Abrir();
            using(var count=CatalogoDatos.Comando(c,"SELECT COUNT(*),SUM(CASE WHEN p.EsPrueba=0 THEN 1 ELSE 0 END),SUM(CASE WHEN p.EsPrueba=1 THEN 1 ELSE 0 END)"+where,"@u",usuario.Id,"@admin",usuario.Admin,"@q",texto,"@tipo",tipo))
            using(var r=count.ExecuteReader())if(r.Read()){resultado.Total=Convert.ToInt32(r[0]);resultado.Normales=r[1]==DBNull.Value?0:Convert.ToInt32(r[1]);resultado.Pruebas=r[2]==DBNull.Value?0:Convert.ToInt32(r[2]);}
            var order=orden switch{"antiguos"=>"p.Id ASC","nombre"=>"p.Nombre ASC,p.Id DESC",_=>"p.Id DESC"};
            using(var cmd=CatalogoDatos.Comando(c,"SELECT p.*,u.Nombre Autor,u.Email SolicitanteEmail"+where+" ORDER BY "+order+" OFFSET @offset ROWS FETCH NEXT @tamano ROWS ONLY","@u",usuario.Id,"@admin",usuario.Admin,"@q",texto,"@tipo",tipo,"@offset",(pagina-1)*tamano,"@tamano",tamano))
            using(var r=cmd.ExecuteReader())while(r.Read())resultado.Items.Add(Leer(r));
            return resultado;
        }
        private Presupuesto Leer(SqlDataReader r)
        {
            var tieneEstado = TieneColumna(r,"EstadoSolicitud");
            return new Presupuesto{Id=(int)r["Id"],UsuarioId=(int)r["UsuarioId"],Autor=(string)r["Autor"],SolicitanteEmail=CatalogoDatos.Texto(r,"SolicitanteEmail"),Nombre=(string)r["Nombre"],Local=(string)r["Local"],Observaciones=(string)r["Observaciones"],Fecha=(DateTime)r["Fecha"],EsPrueba=(bool)r["EsPrueba"],Revision=(int)r["Revision"],EstadoSolicitud=tieneEstado?CatalogoDatos.Texto(r,"EstadoSolicitud"):"Preparada",CompartidaFecha=tieneEstado&&r["CompartidaFecha"]!=DBNull.Value?(DateTime?)Convert.ToDateTime(r["CompartidaFecha"]):null,CanalCompartido=tieneEstado?CatalogoDatos.Texto(r,"CanalCompartido"):""};
        }
        public Presupuesto Obtener(int id,UsuarioCatalogo usuario)
        {
            Presupuesto p;
            using(var c=CatalogoDatos.Abrir())
            {
                using(var cmd=CatalogoDatos.Comando(c,@"SELECT p.*,u.Nombre Autor,u.Email SolicitanteEmail FROM EC_Presupuestos p JOIN EC_Usuarios u ON u.Id=p.UsuarioId WHERE p.Id=@id AND p.Activo=1 AND (p.UsuarioId=@u OR @admin=1)","@id",id,"@u",usuario.Id,"@admin",usuario.Admin))
                using(var r=cmd.ExecuteReader()){if(!r.Read())return null;p=Leer(r);}
                using(var cmd=CatalogoDatos.Comando(c,"SELECT * FROM EC_Items WHERE PresupuestoId=@id ORDER BY Id","@id",id))
                using(var r=cmd.ExecuteReader())while(r.Read())p.Items.Add(new ItemPresupuesto{Id=(int)r["Id"],ProductoId=(int)r["ProductoId"],Nombre=(string)r["Nombre"],Descripcion=(string)r["Descripcion"],Marca=(string)r["Marca"],CodigoCatalogo=(string)r["CodigoCatalogo"],CodigoLocal=(string)r["CodigoLocal"],Tipo=(string)r["Tipo"],Unidad=(string)r["Unidad"],Cantidad=(decimal)r["Cantidad"],ColorId=r["ColorId"]==DBNull.Value?null:Convert.ToInt32(r["ColorId"]),ColorNombre=CatalogoDatos.Texto(r,"ColorNombre"),Observaciones=(string)r["Observaciones"]});
            }
            return p;
        }
        public static ItemPresupuesto DesdeProducto(ArticuloCatalogo a,decimal cantidad,string observaciones,ColorCatalogo color=null)
        {
            if(a==null||!a.Activo)throw new ArgumentException("El producto no está disponible en el catálogo.");
            if(cantidad<1||cantidad>1000000||cantidad!=decimal.Truncate(cantidad))throw new ArgumentException("La cantidad debe ser un número entero entre 1 y 1.000.000.");
            return new ItemPresupuesto{ProductoId=a.Id,Nombre=a.Nombre,Descripcion=a.Descripcion,Marca=a.Marca.Nombre,CodigoCatalogo=a.CodigoCatalogo,CodigoLocal=a.CodigoLocal,Tipo=a.Tipo,Unidad=a.Unidad,Imagen=a.Imagen??"",Cantidad=cantidad,ColorId=color?.Id,ColorNombre=color?.Nombre??"",Observaciones=observaciones??""};
        }
        public int Guardar(Presupuesto p,UsuarioCatalogo usuario)
        {
            if(string.IsNullOrWhiteSpace(p.Nombre)||p.Nombre.Length>160)throw new ArgumentException("Ingresá un nombre de hasta 160 caracteres.");
            if(p.Items.Count>300)throw new ArgumentException("Se permiten hasta 300 renglones por solicitud.");
            foreach(var x in p.Items)
            { if(x.Cantidad<=0||x.Cantidad>1000000||x.Cantidad!=decimal.Truncate(x.Cantidad))throw new ArgumentException("La cantidad debe ser un número entero entre 1 y 1.000.000.");
              if((x.ColorNombre??"").Length>60)throw new ArgumentException("El color seleccionado no es válido."); }
            using(var c=CatalogoDatos.Abrir()) using(var tx=c.BeginTransaction())
            {
                try
                {
                    string sql=p.Id==0?@"INSERT EC_Presupuestos(UsuarioId,Nombre,Local,Observaciones,EsPrueba) OUTPUT INSERTED.Id VALUES(@u,@n,@l,@o,@test)":@"UPDATE EC_Presupuestos SET Nombre=@n,Local=@l,Observaciones=@o,Revision=Revision+1 OUTPUT INSERTED.Id WHERE Id=@id AND Activo=1 AND Revision=@rev AND (UsuarioId=@u OR @admin=1)";
                    using(var cmd=CatalogoDatos.Comando(c,sql,"@u",usuario.Id,"@admin",usuario.Admin,"@n",p.Nombre,"@l",p.Local??"","@o",p.Observaciones??"","@test",p.EsPrueba,"@id",p.Id,"@rev",p.Revision))
                    {cmd.Transaction=tx;var id=cmd.ExecuteScalar();if(id==null)throw new ArgumentException("La solicitud cambió o no tenés acceso. Recargá la página antes de editar.");p.Id=Convert.ToInt32(id);}
                    using(var cmd=CatalogoDatos.Comando(c,"DELETE EC_Items WHERE PresupuestoId=@id","@id",p.Id)){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
                    foreach(var x in p.Items)
                    using(var cmd=CatalogoDatos.Comando(c,@"INSERT EC_Items(PresupuestoId,ProductoId,Nombre,Descripcion,Marca,CodigoCatalogo,CodigoLocal,Tipo,Unidad,Cantidad,ColorId,ColorNombre,PrecioUnitario,Observaciones) VALUES(@id,@p,@n,@d,@m,@c,@l,@t,@u,@q,@colorId,@colorNombre,NULL,@o)",
                        "@id",p.Id,"@p",x.ProductoId,"@n",x.Nombre,"@d",x.Descripcion,"@m",x.Marca,"@c",x.CodigoCatalogo,"@l",x.CodigoLocal,"@t",x.Tipo,"@u",x.Unidad,"@q",x.Cantidad,"@colorId",x.ColorId,"@colorNombre",x.ColorNombre??"","@o",x.Observaciones??""))
                    {cmd.Transaction=tx;cmd.ExecuteNonQuery();}
                    tx.Commit();return p.Id;
                }
                catch{tx.Rollback();throw;}
            }
        }
        public int Duplicar(int id,UsuarioCatalogo usuario)
        {var p=Obtener(id,usuario);if(p==null)throw new ArgumentException("Solicitud no disponible.");p.Id=0;p.Nombre=(p.Nombre.Length>145?p.Nombre.Substring(0,145):p.Nombre)+" (copia)";return Guardar(p,usuario);}
        public void Eliminar(int id,UsuarioCatalogo usuario,int revision)
        {
            using(var c=CatalogoDatos.Abrir())using(var cmd=CatalogoDatos.Comando(c,"UPDATE EC_Presupuestos SET Activo=0,Revision=Revision+1 WHERE Id=@id AND Revision=@r AND (UsuarioId=@u OR @a=1)","@id",id,"@r",revision,"@u",usuario.Id,"@a",usuario.Admin))
                if(cmd.ExecuteNonQuery()!=1)throw new ArgumentException("La solicitud cambió o no tenés acceso. Recargá la página.");
        }
        public bool RegistrarCompartida(int id,UsuarioCatalogo usuario,string canal)
        {
            canal=(canal??"").Trim().ToLowerInvariant();
            if(canal is not ("whatsapp" or "email" or "compartir"))throw new ArgumentException("El canal de envío no es válido.");
            using var c=CatalogoDatos.Abrir();
            using(var disponible=CatalogoDatos.Comando(c,"SELECT CASE WHEN COL_LENGTH('dbo.EC_Presupuestos','EstadoSolicitud') IS NULL THEN 0 ELSE 1 END"))
                if(Convert.ToInt32(disponible.ExecuteScalar())!=1)return false;
            using var cmd=CatalogoDatos.Comando(c,@"UPDATE EC_Presupuestos SET EstadoSolicitud='Compartida',CompartidaFecha=SYSUTCDATETIME(),CanalCompartido=@canal WHERE Id=@id AND Activo=1 AND (UsuarioId=@u OR @admin=1)","@canal",canal,"@id",id,"@u",usuario.Id,"@admin",usuario.Admin);
            if(cmd.ExecuteNonQuery()!=1)throw new ArgumentException("La solicitud no está disponible o no tenés acceso.");
            return true;
        }
        private static bool TieneColumna(SqlDataReader r,string nombre)
        {
            for(var i=0;i<r.FieldCount;i++)if(string.Equals(r.GetName(i),nombre,StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }
    }
}
