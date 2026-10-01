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
    public static async Task<SqlConnection> AbrirAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CadenaConexion)) throw new InvalidOperationException("Falta configurar ConnectionStrings:Electricistas.");
        var conexion = new SqlConnection(CadenaConexion);
        try { await conexion.OpenAsync(cancellationToken); return conexion; }
        catch { await conexion.DisposeAsync(); throw; }
    }
    public static SqlCommand Comando(SqlConnection c, string sql, params object[] pares)
    {
        var cmd = new SqlCommand(sql, c) { CommandTimeout = 15 };
        for (int i = 0; i < pares.Length; i += 2)
            AgregarParametro(cmd, (string)pares[i], pares[i + 1]);
        return cmd;
    }
    private static void AgregarParametro(SqlCommand cmd, string nombre, object valor)
    {
        if (valor == null)
        {
            cmd.Parameters.AddWithValue(nombre, DBNull.Value);
            return;
        }
        var parametro = valor switch
        {
            int n => new SqlParameter(nombre, SqlDbType.Int) { Value = n },
            long n => new SqlParameter(nombre, SqlDbType.BigInt) { Value = n },
            bool b => new SqlParameter(nombre, SqlDbType.Bit) { Value = b },
            DateTime fecha => new SqlParameter(nombre, SqlDbType.DateTime2) { Value = fecha },
            decimal numero => new SqlParameter(nombre, SqlDbType.Decimal) { Precision = 18, Scale = 3, Value = numero },
            string texto => new SqlParameter(nombre, SqlDbType.NVarChar, Math.Clamp(texto.Length, 1, 4000)) { Value = texto },
            _ => new SqlParameter(nombre, valor)
        };
        cmd.Parameters.Add(parametro);
    }
    public static string Texto(IDataRecord r, string campo) => r[campo] == DBNull.Value ? "" : Convert.ToString(r[campo]);
    public static decimal? Precio(IDataRecord r, string campo) => r[campo] == DBNull.Value ? null : Convert.ToDecimal(r[campo]);
}
