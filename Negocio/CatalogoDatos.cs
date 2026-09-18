using Microsoft.Data.SqlClient;
using System.Data;

namespace Negocio;

public static class CatalogoDatos
{
    public static string CadenaConexion { private get; set; }
    public static SqlConnection Abrir()
    {
        if (string.IsNullOrWhiteSpace(CadenaConexion)) throw new InvalidOperationException("Falta configurar ConnectionStrings:Electricistas.");
        var conexion = new SqlConnection(CadenaConexion);
        try { conexion.Open(); return conexion; }
        catch { conexion.Dispose(); throw; }
    }
    public static SqlCommand Comando(SqlConnection c, string sql, params object[] pares)
    {
        var cmd = new SqlCommand(sql, c) { CommandTimeout = 15 };
        for (int i = 0; i < pares.Length; i += 2)
            cmd.Parameters.AddWithValue((string)pares[i], pares[i + 1] ?? DBNull.Value);
        return cmd;
    }
    public static string Texto(IDataRecord r, string campo) => r[campo] == DBNull.Value ? "" : Convert.ToString(r[campo]);
    public static decimal? Precio(IDataRecord r, string campo) => r[campo] == DBNull.Value ? null : Convert.ToDecimal(r[campo]);
}
