using Dominio;
using System.Text.Json;

namespace Negocio;

// Exclusión entre procesos y contador durable para la instalación local.
public sealed class BorradorComercialArchivo(string carpeta)
{
    private string Archivo(int usuario) => Path.Combine(carpeta, usuario + ".json");
    private FileStream Bloquear()
    {
        Directory.CreateDirectory(carpeta);
        for(var i=0;;i++)
        {
            try{return new FileStream(Path.Combine(carpeta,"numeracion.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException) when(i<100){Thread.Sleep(25);}
        }
    }
    private static void Escribir(string archivo,string contenido)
    {
        var temporal=archivo+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporal,contenido);File.Move(temporal,archivo,true);}
        finally{if(File.Exists(temporal))File.Delete(temporal);}
    }
    private PresupuestoComercial Leer(int usuario) => JsonSerializer.Deserialize<PresupuestoComercial>(File.ReadAllText(Archivo(usuario))) ?? throw new InvalidDataException("No se pudo leer el borrador guardado.");
    private PresupuestoComercial Crear(int usuario)
    {
        var secuencia=Path.Combine(carpeta,"secuencia.txt");
        long ultimo=File.Exists(secuencia)?long.Parse(File.ReadAllText(secuencia)):0;
        // Incorporar numeración previa sin sobrescribir borradores existentes.
        foreach(var archivo in Directory.EnumerateFiles(carpeta,"*.json"))
        {
            var previo=JsonSerializer.Deserialize<PresupuestoComercial>(File.ReadAllText(archivo));
            var numero=previo?.Numero ?? "";
            if(numero.StartsWith("0001-") && long.TryParse(numero[5..],out var n))ultimo=Math.Max(ultimo,n);
        }
        if(ultimo>=9999999)throw new InvalidOperationException("Se alcanzó el límite de numeración de presupuestos.");
        var nuevo=PresupuestoComercial.Plantilla();nuevo.Numero=$"0001-{ultimo+1:D7}";
        // Reservar antes de guardar: un fallo puede dejar un salto, nunca reutilizar un número.
        Escribir(secuencia,(ultimo+1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Escribir(Archivo(usuario),JsonSerializer.Serialize(nuevo));return nuevo;
    }
    public PresupuestoComercial Obtener(int usuario)
    {
        using var bloqueo=Bloquear();
        return File.Exists(Archivo(usuario))?Leer(usuario):Crear(usuario);
    }
    public PresupuestoComercial Nuevo(int usuario)
    {
        using var bloqueo=Bloquear();
        if(File.Exists(Archivo(usuario)))
        {
            var archivo=Archivo(usuario);var historial=Path.Combine(carpeta,"anteriores");Directory.CreateDirectory(historial);
            File.Copy(archivo,Path.Combine(historial,$"{usuario}-{Guid.NewGuid():N}.json"));
        }
        return Crear(usuario);
    }
    public void ValidarNumero(int usuario,PresupuestoComercial p)
    {
        if(!File.Exists(Archivo(usuario)) || Leer(usuario).Numero!=p.Numero)
            throw new ArgumentException("Este presupuesto ya no es el borrador actual. Recargá la página.");
    }
    public void Guardar(int usuario,PresupuestoComercial p)
    {
        p.Validar();using var bloqueo=Bloquear();ValidarNumero(usuario,p);
        Escribir(Archivo(usuario),JsonSerializer.Serialize(p));
    }
    public void Comprobar(int usuario,PresupuestoComercial p)
    {using var bloqueo=Bloquear();ValidarNumero(usuario,p);}
}
