using Dominio;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;

namespace Negocio
{
    public class SeguridadCatalogo
    {
        public static string Hash(string password)
        {
            if (password == null || password.Length < 12 || password.Length > 128) throw new ArgumentException("La contraseña debe tener entre 12 y 128 caracteres.");
            byte[] salt=new byte[16]; using(var rng=RandomNumberGenerator.Create()) rng.GetBytes(salt);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password,salt,210000,HashAlgorithmName.SHA256,32);
            return "210000:"+Convert.ToBase64String(salt)+":"+Convert.ToBase64String(hash);
        }
        public static bool Verificar(string password,string hash)
        {
            if(password==null || password.Length>128) return false;
            var p=hash.Split(':'); if(p.Length!=3) return false;
            try {
                var a=Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(p[1]),int.Parse(p[0]),HashAlgorithmName.SHA256,32);
                return CryptographicOperations.FixedTimeEquals(a,Convert.FromBase64String(p[2]));
            } catch(FormatException) { return false; }
        }
        public UsuarioCatalogo Obtener(int id)
        {
            using(var c=CatalogoDatos.Abrir()) using(var cmd=CatalogoDatos.Comando(c,"SELECT Id,Nombre,Email,EsAdmin,Activo FROM EC_Usuarios WHERE Id=@id AND Activo=1","@id",id))
            using(var r=cmd.ExecuteReader()) return r.Read()?new UsuarioCatalogo{Id=(int)r["Id"],Nombre=(string)r["Nombre"],Email=(string)r["Email"],Admin=(bool)r["EsAdmin"],Activo=(bool)r["Activo"]}:null;
        }
        public UsuarioCatalogo ObtenerGestion(int id)
        {
            using var c=CatalogoDatos.Abrir(); using var cmd=CatalogoDatos.Comando(c,"SELECT Id,Nombre,Email,EsAdmin,Activo FROM EC_Usuarios WHERE Id=@id","@id",id);
            using var r=cmd.ExecuteReader(); return r.Read()?Leer(r):null;
        }
        public List<UsuarioCatalogo> Listar(string texto="",string rol="",string estado="")
        {
            var lista=new List<UsuarioCatalogo>();
            using var c=CatalogoDatos.Abrir(); using var cmd=CatalogoDatos.Comando(c,@"SELECT Id,Nombre,Email,EsAdmin,Activo FROM EC_Usuarios
                WHERE (@q='' OR CHARINDEX(@q,Nombre+' '+Email)>0) AND (@rol='' OR EsAdmin=CASE WHEN @rol='admin' THEN 1 ELSE 0 END)
                AND (@estado='' OR Activo=CASE WHEN @estado='activo' THEN 1 ELSE 0 END) ORDER BY Nombre,Email","@q",texto??"","@rol",rol??"","@estado",estado??"");
            using var r=cmd.ExecuteReader(); while(r.Read())lista.Add(Leer(r)); return lista;
        }
        public UsuarioCatalogo Login(string email,string password)
        {
            using(var c=CatalogoDatos.Abrir())
            {
                int id; string hash;
                using(var cmd=CatalogoDatos.Comando(c,"SELECT Id,PasswordHash FROM EC_Usuarios WHERE Email=@e AND Activo=1 AND (BloqueadoHasta IS NULL OR BloqueadoHasta<SYSUTCDATETIME())","@e",email))
                using(var r=cmd.ExecuteReader()){if(!r.Read())return null;id=(int)r["Id"];hash=(string)r["PasswordHash"];}
                bool ok=Verificar(password,hash);
                using(var cmd=CatalogoDatos.Comando(c,ok?"UPDATE EC_Usuarios SET Fallos=0,BloqueadoHasta=NULL WHERE Id=@id":"UPDATE EC_Usuarios SET Fallos=CASE WHEN Fallos>=4 THEN 0 ELSE Fallos+1 END,BloqueadoHasta=CASE WHEN Fallos>=4 THEN DATEADD(minute,15,SYSUTCDATETIME()) ELSE NULL END WHERE Id=@id","@id",id))cmd.ExecuteNonQuery();
                return ok?Obtener(id):null;
            }
        }
        public void Crear(string nombre,string email,string password,bool admin)
        {
            if(string.IsNullOrWhiteSpace(nombre)||string.IsNullOrWhiteSpace(email)||!email.Contains("@"))throw new ArgumentException("Completá nombre y un email válido.");
            using(var c=CatalogoDatos.Abrir()) using(var cmd=CatalogoDatos.Comando(c,"INSERT EC_Usuarios(Nombre,Email,PasswordHash,EsAdmin) VALUES(@n,@e,@p,@a)","@n",nombre,"@e",email,"@p",Hash(password),"@a",admin))cmd.ExecuteNonQuery();
        }
        public int GuardarGestion(int id,string nombre,string email,bool admin,bool activo,string nuevaPassword,int actorId)
        {
            nombre=(nombre??"").Trim(); email=(email??"").Trim().ToLowerInvariant();
            if(string.IsNullOrWhiteSpace(nombre)||string.IsNullOrWhiteSpace(email)||!email.Contains('@')||email.StartsWith('@')||email.EndsWith('@'))throw new ArgumentException("Completá nombre y un email válido.");
            if(id==0 && string.IsNullOrEmpty(nuevaPassword))throw new ArgumentException("La contraseña es obligatoria para un usuario nuevo.");
            if(id==actorId && (!admin||!activo))throw new ArgumentException("No podés quitarte el rol administrador ni desactivar tu propia cuenta.");
            using var c=CatalogoDatos.Abrir(); using var tx=c.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                using(var dup=CatalogoDatos.Comando(c,"SELECT COUNT(*) FROM EC_Usuarios WHERE Email=@e AND Id<>@id","@e",email,"@id",id)){dup.Transaction=tx;if(Convert.ToInt32(dup.ExecuteScalar())>0)throw new ArgumentException("Ya existe un usuario con ese email.");}
                if(id==0)
                {
                    using var cmd=CatalogoDatos.Comando(c,"INSERT EC_Usuarios(Nombre,Email,PasswordHash,EsAdmin,Activo) OUTPUT INSERTED.Id VALUES(@n,@e,@p,@a,@activo)","@n",nombre,"@e",email,"@p",Hash(nuevaPassword),"@a",admin,"@activo",activo);cmd.Transaction=tx;id=Convert.ToInt32(cmd.ExecuteScalar());
                }
                else
                {
                    bool eraAdmin; using(var read=CatalogoDatos.Comando(c,"SELECT EsAdmin FROM EC_Usuarios WHERE Id=@id","@id",id)){read.Transaction=tx;var value=read.ExecuteScalar();if(value==null)throw new ArgumentException("El usuario ya no existe.");eraAdmin=(bool)value;}
                    if(eraAdmin&&(!admin||!activo)){using var count=CatalogoDatos.Comando(c,"SELECT COUNT(*) FROM EC_Usuarios WHERE EsAdmin=1 AND Activo=1","@id",id);count.Transaction=tx;if(Convert.ToInt32(count.ExecuteScalar())<=1)throw new ArgumentException("Debe quedar al menos un administrador activo.");}
                    string sql=string.IsNullOrEmpty(nuevaPassword)?"UPDATE EC_Usuarios SET Nombre=@n,Email=@e,EsAdmin=@a,Activo=@activo WHERE Id=@id":"UPDATE EC_Usuarios SET Nombre=@n,Email=@e,EsAdmin=@a,Activo=@activo,PasswordHash=@p,Fallos=0,BloqueadoHasta=NULL WHERE Id=@id";
                    using var cmd=CatalogoDatos.Comando(c,sql,"@n",nombre,"@e",email,"@a",admin,"@activo",activo,"@p",string.IsNullOrEmpty(nuevaPassword)?null:Hash(nuevaPassword),"@id",id);cmd.Transaction=tx;if(cmd.ExecuteNonQuery()!=1)throw new ArgumentException("El usuario ya no existe.");
                }
                tx.Commit();return id;
            }
            catch{tx.Rollback();throw;}
        }
        public void CambiarPassword(int id,string actual,string nueva)
        {
            using(var c=CatalogoDatos.Abrir())
            {
                string hash; using(var cmd=CatalogoDatos.Comando(c,"SELECT PasswordHash FROM EC_Usuarios WHERE Id=@id AND Activo=1","@id",id)) hash=(string)cmd.ExecuteScalar();
                if(hash==null || !Verificar(actual,hash))throw new ArgumentException("La contraseña actual no es correcta.");
                using(var cmd=CatalogoDatos.Comando(c,"UPDATE EC_Usuarios SET PasswordHash=@p WHERE Id=@id","@p",Hash(nueva),"@id",id))cmd.ExecuteNonQuery();
            }
        }
        private static UsuarioCatalogo Leer(System.Data.IDataRecord r)=>new(){Id=(int)r["Id"],Nombre=(string)r["Nombre"],Email=(string)r["Email"],Admin=(bool)r["EsAdmin"],Activo=(bool)r["Activo"]};
    }
}
