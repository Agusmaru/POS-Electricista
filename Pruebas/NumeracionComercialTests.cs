using Dominio;
using Negocio;

namespace Pruebas;

public class NumeracionComercialTests
{
    [Fact]
    public void ReservaCorrelativosSinDuplicarAlRecargarYConservaFechaEditada()
    {
        var carpeta=Path.Combine(Path.GetTempPath(),"furnarius-test-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new BorradorComercialArchivo(carpeta);
            var primero=store.Obtener(1);
            Assert.Equal("0001-0000001",primero.Numero);
            Assert.All(new[]{primero.Cliente,primero.Cuit,primero.Domicilio,primero.Localidad,primero.Asunto},s=>Assert.Equal("",s));
            Assert.Empty(primero.Items);
            Assert.Equal(DateTime.UtcNow.AddHours(-3).Date,primero.Fecha);
            Assert.Equal(primero.Numero,new BorradorComercialArchivo(carpeta).Obtener(1).Numero);
            primero.Cliente="Cliente";primero.Fecha=new DateTime(2025,3,2);primero.Asunto="Prueba de orden";
            primero.Items=[new(){Articulo="Primero"},new(){Articulo="Segundo"}];
            primero.Items.Reverse();store.Guardar(1,primero);
            Assert.Equal(new[]{"Segundo","Primero"},store.Obtener(1).Items.Select(x=>x.Articulo));
            Assert.Equal(primero.Fecha,store.Obtener(1).Fecha);
            var segundo=store.Nuevo(1);Assert.Equal("0001-0000002",segundo.Numero);
            Assert.Throws<ArgumentException>(()=>store.Guardar(1,primero));
            Assert.Single(Directory.GetFiles(Path.Combine(carpeta,"anteriores")));
            var numeros=new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.For(2,12,id=>numeros.Add(new BorradorComercialArchivo(carpeta).Obtener(id).Numero));
            Assert.Equal(10,numeros.Distinct().Count());
            Assert.Equal("0001-0000013",store.Nuevo(1).Numero);
        }
        finally {if(Directory.Exists(carpeta))Directory.Delete(carpeta,true);}
    }
}
