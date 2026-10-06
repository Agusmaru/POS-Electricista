using Dominio;

namespace Pruebas;

public class PresupuestoComercialTests
{
    [Fact]
    public void EjemploReconciliaTodasLasFilasYManoDeObra()
    {
        var p=Ejemplo();p.Cliente="Cliente de prueba";p.Numero="0001-0000001";p.Validar();
        Assert.Equal(15,p.Items.Count);
        Assert.Equal(35088.50m,p.Neto);
        Assert.Equal(6331.19m,p.Iva);
        Assert.Equal(47397.69m,p.Total);
    }
    [Fact]
    public void QuitarConceptoOpcionalActualizaElTotal()
    {
        var p=Ejemplo();p.Cliente="Cliente de prueba";p.Numero="0001-0000001";p.Items.RemoveAt(14);
        Assert.Equal(46066.69m,p.Total);
    }
    [Fact]
    public void RedondeaMedioCentavoAlejandoseDeCero()
    {
        var x=new RenglonComercial{Cantidad=1,Precio=.05m,Alicuota=10.5m};
        Assert.Equal(.01m,x.Iva);Assert.Equal(.06m,x.Total);
    }
    [Theory]
    [InlineData(-1,21)]
    [InlineData(1,99)]
    public void RechazaValoresInvalidos(decimal precio,decimal iva)
    {
        var p=Ejemplo();p.Cliente="Cliente de prueba";p.Numero="0001-0000001";p.Items[0].Precio=precio;p.Items[0].Alicuota=iva;
        Assert.Throws<ArgumentException>(p.Validar);
    }
    [Fact]
    public void RechazaListaVacia()
    {
        var p=Ejemplo();p.Cliente="Cliente de prueba";p.Numero="0001-0000001";p.Items.Clear();Assert.Throws<ArgumentException>(p.Validar);
    }
    [Fact]
    public void PrecioCeroEsAdmitidoPeroCantidadCeroNo()
    {
        var p=Ejemplo();p.Cliente="Cliente de prueba";p.Numero="0001-0000001";p.Items[0].Precio=0;p.Validar();
        p.Items[0].Cantidad=0;Assert.Throws<ArgumentException>(p.Validar);
    }
    private static PresupuestoComercial Ejemplo()
    {
        var p = new PresupuestoComercial { Asunto = "Ejemplo de prueba", ManoObra = 5978 };
        void F(string a, string d, decimal q, decimal precio, decimal iva = 21) => p.Items.Add(new() { Articulo = a, Descripcion = d, Cantidad = q, Precio = precio, Alicuota = iva });
        F("Paneles Solares", "Risen 585Wp Bifacial 144 celdas, Tecnología TOPCon",84,95,10.5m);
        F("On Grid","Inversor Growatt On-Grid MID-40KTL3-X2",1,2280);
        F("Materiales Eléctricos AC","Cableados, gabinete, canalizaciones, PAT",1,4846);
        F("Materiales Eléctricos DC","Cables y protecciones de corriente continua, METER",1,4100);
        F("Insumos de Ferretería","Bulones, remaches, etc.",1,650);
        F("Soportes","Soportes para 84 Paneles (U-45 de Tonka)",1,2100);
        F("Conexión WIFI","Datalogger Shine Wifi-X",2,36);
        F("Tijera","Alquiler tijera de 14m por 1 mes",1,1500);
        F("On Grid","Inversor Growatt On Grid MOD 10000TL3-XH trifásico",1,1320);
        F("BMS","Módulo BMS Growatt APX 98034-P2 (PM+Base)",1,1460);
        F("Baterías","Módulo Batería Growatt APX 5.0P-B1",2,1640);
        F("Paneles Solares","Risen 585Wp Bifacial 144 celdas, Tecnología TOPCon",20,95,10.5m);
        F("Soportes","Soportes para 20 Paneles Losa de Tonka",1,1875.50m);
        F("Backup Box","SYN 100 HX 30 (BACKUP BOX TRIFÁSICO)",1,625);
        F("MED BID (Opcional)","Trámite medidor Bidireccional",1,1100);
        return p;
    }
}
