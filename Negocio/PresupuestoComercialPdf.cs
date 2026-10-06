using Dominio;
using System.Globalization;
using System.Text;

namespace Negocio;

public sealed class PresupuestoComercialPdf
{
    private readonly List<StringBuilder> pages = [];
    private StringBuilder page;
    private float y;
    private PresupuestoComercial model;
    private readonly CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
    private static string N(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Clean(string s) => (s ?? "").Replace('–','-').Replace('—','-').Replace('“','"').Replace('”','"').Replace('\r',' ').Replace('\t',' ');
    private string Money(decimal v) => "U$S " + v.ToString("N2", culture);
    private void Text(float x, float top, string s, float size = 8, bool bold = false, bool white = false)
    {
        s = Clean(s).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace('\n',' ');
        page.Append($"BT /{(bold ? "F2" : "F1")} {N(size)} Tf {(white ? "1 1 1" : "0.12 0.12 0.12")} rg 1 0 0 1 {N(x)} {N(842-top)} Tm ({s}) Tj ET\n");
    }
    private static float Width(string s, float size) => s.Sum(c => " ilI.,:;'!|".Contains(c) ? .28f : "MW@%".Contains(c) ? .9f : char.IsUpper(c) ? .68f : .53f) * size;
    private void Right(float x,float top,string s,float size=7,bool bold=false,bool white=false) => Text(x-Width(s,size),top,s,size,bold,white);
    private void Box(float x,float top,float w,float h,string color="0.24 0.24 0.24") => page.Append($"{color} rg {N(x)} {N(842-top-h)} {N(w)} {N(h)} re f\n");
    private void Image(string name,float x,float top,float w,float h) => page.Append($"q {N(w)} 0 0 {N(h)} {N(x)} {N(842-top-h)} cm /{name} Do Q\n");
    private static List<string> Wrap(string s,float width,float size)
    {
        var result=new List<string>();
        foreach(var paragraph in Clean(s).Split('\n'))
        {
            var line="";
            foreach(var word in paragraph.Split(' ',StringSplitOptions.RemoveEmptyEntries))
            {
                if(line.Length>0 && Width(line+" "+word,size)>width){result.Add(line);line="";}
                foreach(var ch in (line.Length>0?" ":"")+word){if(Width(line+ch,size)>width && line.Length>0){result.Add(line);line="";}line+=ch;}
            }
            result.Add(line);
        }
        return result;
    }
    private void Lines(float x,ref float top,string text,float width,float size=8,bool bold=false)
    {foreach(var line in Wrap(text,width,size)){Text(x,top,line,size,bold);top+=size+3;}}
    private void NewPage(bool table)
    {
        page=new();pages.Add(page);
        Image("Logo",30,25,205,59.6f);
        Box(273,34,40,43,"0 0 0");Box(276,37,34,37,"1 1 1");Text(282,66,"X",30,true);
        float heading=40;
        Lines(355,ref heading,"Presupuesto "+model.Numero,207,10,true);
        Lines(355,ref heading,"Fecha: "+model.Fecha.ToString("dd/MM/yyyy"),207,9);
        float left=110;
        Lines(30,ref left,"Cliente: "+model.Cliente,265,10,true);
        Lines(30,ref left,"CUIT: "+model.Cuit+"\nDomicilio: "+model.Domicilio+"\nLocalidad: "+model.Localidad,265,8);
        float right=92;
        Lines(350,ref right,"Furnarius Energy\nBuenos Aires, Argentina",215,8,true);
        Lines(350,ref right,model.Contactos,215,7);
        Lines(350,ref right,"@furnariusenergy\nwww.furnariusenergy.com",215,8,true);
        y=Math.Max(left,right)+12;
        var subject=Wrap(model.Asunto,505,8);
        Box(30,y,535,subject.Count*11+19);Box(30,y,4,subject.Count*11+19,"1 0.43 0.08");
        Text(39,y+11,"Asunto",7,true,true);
        foreach(var line in subject){Text(39,y+23,line,8,false,true);y+=11;}
        y+=30;
        if(table)TableHeader();
    }
    private void TableHeader()
    {
        Box(30,y,535,23);
        string[] labels=["Artículo","Descripción","Cant.","Precio USD","IVA %","Subtotal USD","IVA USD","Total USD"];
        float[] x=[32,104,252,291,356,393,454,508];
        for(int i=0;i<labels.Length;i++)Text(x[i],y+15,labels[i],6.7f,false,true);
        y+=23;
    }
    private void Ensure(float height,bool table=false){if(y+height>793)NewPage(table);}
    public byte[] Generar(PresupuestoComercial p,byte[] logo,byte[] footer)
    {
        p.Validar();model=p;pages.Clear();NewPage(true);
        foreach(var item in p.Items)
        {
            var columns=new[]{Wrap(item.Articulo,67,6.3f),Wrap(item.Descripcion,130,6.3f),Wrap(item.Cantidad.ToString("0.###",culture),34,7),Wrap(Money(item.Precio),60,6.5f),Wrap(item.Alicuota.ToString("0.##",culture),27,7),Wrap(Money(item.Neto),56,6.5f),Wrap(Money(item.Iva),51,6.5f),Wrap(Money(item.Total),55,6.5f)};
            float h=columns.Max(c=>c.Count)*8+4;Ensure(h,true);
            float[] x=[32,104,252,291,356,393,454,508];
            for(int col=0;col<8;col++)for(int l=0;l<columns[col].Count;l++)Text(x[col],y+9+l*8,columns[col][l],col is 3 or >=5 ? 6.5f:6.3f);
            y+=h;Box(30,y,535,.3f,"0.86 0.86 0.86");
        }
        Ensure(90);Box(30,y+5,535,24);Text(272,y+20,"Subtotales",8,true,true);
        Right(449,y+20,Money(p.Neto),7,false,true);Right(502,y+20,Money(p.Iva),7,false,true);Right(562,y+20,Money(p.Neto+p.Iva),7,false,true);y+=45;
        Text(32,y,"Mano de obra",8,true);float laborTop=y;Lines(105,ref laborTop,p.ManoObraDescripcion,310,8);Right(562,y,Money(p.ManoObra),9);y=laborTop+10;
        Ensure(45);Box(30,y,535,2,"1 0.43 0.08");y+=12;Box(30,y,260,27);Text(38,y+18,"Total",11,true,true);Right(281,y+18,Money(p.Total),12,true,true);y+=42;
        foreach(var line in Wrap(p.Condiciones,520,7)){Ensure(12);Box(30,y-8,535,12);Text(36,y,line,7,false,true);y+=11;}
        y+=16;
        foreach(var line in Wrap(p.Nota,530,8)){Ensure(12);Text(32,y,line,8);y+=11;}
        Ensure(115);Image("Pie",30,y+10,535,94);y+=110;Box(30,y,535,4);
        for(int i=0;i<pages.Count;i++){page=pages[i];Text(30,823,"Presupuesto comercial - No válido como factura",7);Right(565,823,$"{i+1} / {pages.Count}",7);}
        return Serialize(logo,footer);
    }
    private byte[] Serialize(byte[] logo,byte[] footer)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);var enc=Encoding.GetEncoding(1252);var objects=new List<byte[]>();
        void Add(string s)=>objects.Add(enc.GetBytes(s));
        void AddImage(byte[] bytes,int w,int h){using var ms=new MemoryStream();ms.Write(enc.GetBytes($"<< /Type /XObject /Subtype /Image /Width {w} /Height {h} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {bytes.Length} >>\nstream\n"));ms.Write(bytes);ms.Write(enc.GetBytes("\nendstream"));objects.Add(ms.ToArray());}
        Add("<< /Type /Catalog /Pages 2 0 R >>");
        Add("<< /Type /Pages /Count "+pages.Count+" /Kids ["+string.Join(" ",Enumerable.Range(0,pages.Count).Select(i=>$"{7+i*2} 0 R"))+"] >>");
        Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        AddImage(logo,685,199);AddImage(footer,1500,264);
        for(int i=0;i<pages.Count;i++)
        {
            Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> /XObject << /Logo 5 0 R /Pie 6 0 R >> >> /Contents {8+i*2} 0 R >>");
            Add("<< /Length "+enc.GetByteCount(pages[i].ToString())+" >>\nstream\n"+pages[i]+"endstream");
        }
        using var output=new MemoryStream();void Write(string s)=>output.Write(enc.GetBytes(s));Write("%PDF-1.4\n%âãÏÓ\n");var offsets=new List<long>();
        for(int i=0;i<objects.Count;i++){offsets.Add(output.Position);Write($"{i+1} 0 obj\n");output.Write(objects[i]);Write("\nendobj\n");}
        long xref=output.Position;Write($"xref\n0 {objects.Count+1}\n0000000000 65535 f \n");foreach(var offset in offsets)Write(offset.ToString("D10")+" 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");return output.ToArray();
    }
}
