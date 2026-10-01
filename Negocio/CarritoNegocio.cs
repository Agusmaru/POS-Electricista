using System.Text.Json;
using Dominio;
using Microsoft.Data.SqlClient;

namespace Negocio;

public sealed class CarritoNegocio
{
    public Presupuesto Obtener(int usuarioId)
    {
        try
        {
            using var conexion = CatalogoDatos.Abrir();
            if (!TablaDisponible(conexion)) return null;
            using var comando = CatalogoDatos.Comando(conexion, "SELECT Contenido FROM EC_Carritos WHERE UsuarioId=@usuario", "@usuario", usuarioId);
            var contenido = comando.ExecuteScalar() as string;
            return string.IsNullOrWhiteSpace(contenido) ? null : JsonSerializer.Deserialize<Presupuesto>(contenido);
        }
        catch (SqlException) { return null; }
        catch (JsonException) { return null; }
    }

    public bool Guardar(int usuarioId, Presupuesto carrito)
    {
        try
        {
            using var conexion = CatalogoDatos.Abrir();
            if (!TablaDisponible(conexion)) return false;
            using var comando = CatalogoDatos.Comando(conexion, @"UPDATE EC_Carritos SET Contenido=@contenido,Actualizado=SYSUTCDATETIME() WHERE UsuarioId=@usuario;
IF @@ROWCOUNT=0 INSERT EC_Carritos(UsuarioId,Contenido) VALUES(@usuario,@contenido);",
                "@usuario", usuarioId, "@contenido", JsonSerializer.Serialize(carrito));
            comando.ExecuteNonQuery();
            return true;
        }
        catch (SqlException) { return false; }
    }

    public void Eliminar(int usuarioId)
    {
        try
        {
            using var conexion = CatalogoDatos.Abrir();
            if (!TablaDisponible(conexion)) return;
            using var comando = CatalogoDatos.Comando(conexion, "DELETE EC_Carritos WHERE UsuarioId=@usuario", "@usuario", usuarioId);
            comando.ExecuteNonQuery();
        }
        catch (SqlException) { }
    }

    private static bool TablaDisponible(SqlConnection conexion)
    {
        using var comando = CatalogoDatos.Comando(conexion, "SELECT CASE WHEN OBJECT_ID('dbo.EC_Carritos','U') IS NULL THEN 0 ELSE 1 END");
        return Convert.ToInt32(comando.ExecuteScalar()) == 1;
    }
}
