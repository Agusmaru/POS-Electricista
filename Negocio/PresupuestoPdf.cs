using Dominio;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Negocio
{
    // PDF vectorial independiente de impresoras, navegadores o servicios externos.
    // Helvetica WinAnsi cubre el español; se normalizan símbolos del catálogo.
    public class PresupuestoPdf
    {
        private readonly List<StringBuilder> paginas=new List<StringBuilder>();
        private StringBuilder pagina;
        private float y;
        private readonly CultureInfo cultura=CultureInfo.GetCultureInfo("es-AR");
        private static string N(float x){return x.ToString("0.##",CultureInfo.InvariantCulture);}
        private static string Limpiar(string s){return (s??"").Replace("\r"," ").Replace("\n"," ").Replace("\t"," ").Replace('–','-').Replace('—','-').Replace('“','"').Replace('”','"').Replace('′','\'').Replace('≥','>').Replace('≤','<');}
        private void Texto(float x,float top,string text,float size=9,bool bold=false,string color="0.12 0.18 0.24")
        {var t=Limpiar(text).Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)");pagina.AppendFormat(CultureInfo.InvariantCulture,"BT /{0} {1} Tf {2} rg 1 0 0 1 {3} {4} Tm ({5}) Tj ET\n",bold?"F2":"F1",N(size),color,N(x),N(595-top),t);}
        private void Rect(float x,float top,float w,float h,string color)
        {pagina.AppendFormat(CultureInfo.InvariantCulture,"{0} rg {1} {2} {3} {4} re f\n",color,N(x),N(595-top-h),N(w),N(h));}
        private static double Width(string text,float size)
        {
            double sum=0; foreach(char c in Limpiar(text))
                sum+= "ilI.,:;'!| ".Contains(c)?0.28:"MW@%".Contains(c)?0.9:char.IsUpper(c)?0.7:0.56;
            return sum*size;
        }
        private List<string> Wrap(string s,float width,float size)
        {
            var lines=new List<string>();string line="";
            foreach(var word in Limpiar(s).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))
            {
                if(Width(line.Length==0?word:line+" "+word,size)<=width){line+=(line.Length==0?"":" ")+word;continue;}
                if(line.Length>0){lines.Add(line);line="";}
                foreach(char ch in word){if(Width(line+ch,size)>width&&line.Length>0){lines.Add(line);line="";}line+=ch;}
            }
            if(line.Length>0)lines.Add(line);if(lines.Count==0)lines.Add("");return lines;
        }
        private void Derecha(float right,float top,string text,float size=9,bool bold=false){Texto(right-(float)Width(text,size),top,text,size,bold);}
        private void Nueva(Presupuesto p,bool tabla=true)
        {
            pagina=new StringBuilder();paginas.Add(pagina);
            Rect(0,0,842,7,"0.08 0.17 0.26");
            Texto(32,35,"ELECTRICIDAD | CATÁLOGO PROFESIONAL",11,true,"0.08 0.17 0.26");
            Texto(32,62,"Orden de materiales",20,true);
            var title=Wrap(p.Nombre,760,11);float ty=82;foreach(var line in title){Texto(32,ty,line,11,true);ty+=14;}
            Texto(32,ty+3,"Nº "+p.Id.ToString("D6")+"   |   "+p.Fecha.AddHours(-3).ToString("dd/MM/yyyy"),9);
            ty+=20;
            foreach(var line in Wrap("Preparó: "+p.Autor,770,9)){Texto(32,ty,line);ty+=12;}
            if(p.EsPrueba){Rect(32,ty-1,778,23,"1 0.94 0.79");Texto(40,ty+14,"DOCUMENTO DE PRUEBA - No usar como orden real.",9,true);ty+=32;}
            y=ty+5;
            if(tabla)Cabecera();
        }
        private void Cabecera()
        {Rect(32,y,778,24,"0.08 0.17 0.26");Texto(40,y+16,"PRODUCTO / DESCRIPCIÓN",9,true,"1 1 1");Texto(470,y+16,"CÓDIGOS",9,true,"1 1 1");Texto(680,y+16,"CANTIDAD / UNIDAD",9,true,"1 1 1");y+=24;}
        public byte[] Generar(Presupuesto p)
        {
            if(p==null||p.Items.Count==0)throw new ArgumentException("Agregá al menos un material antes de descargar el PDF.");
            paginas.Clear();Nueva(p);int indice=0;
            foreach(var item in p.Items)
            {
                var material=Wrap(item.Nombre,410,9);material.AddRange(Wrap(item.Descripcion,410,8));material.AddRange(Wrap(item.Marca+" | "+item.Tipo,410,8));
                if(!string.IsNullOrWhiteSpace(item.ColorNombre))material.AddRange(Wrap("Color: "+item.ColorNombre,410,8));
                if(!string.IsNullOrWhiteSpace(item.Observaciones))material.AddRange(Wrap("Nota: "+item.Observaciones,410,8));
                var codes=Wrap("Cat.: "+(string.IsNullOrEmpty(item.CodigoCatalogo)?"A consultar":item.CodigoCatalogo),190,8);
                codes.AddRange(Wrap("Interno: "+(string.IsNullOrEmpty(item.CodigoLocal)?"A consultar":item.CodigoLocal),190,8));
                var units=Wrap(item.Cantidad.ToString("0.###",cultura)+" "+item.Unidad,115,8);
                int max=Math.Max(material.Count,Math.Max(codes.Count,units.Count));
                for(int offset=0;offset<max;)
                {
                    if(y+40>507)Nueva(p);
                    int take=Math.Min(max-offset,Math.Max(1,(int)((507-y-14)/12)));
                    float h=take*12+14;
                    Rect(32,y,778,h,indice%2==0?"0.96 0.98 0.98":"1 1 1");
                    for(int k=0;k<take;k++)
                    {int i=offset+k;if(i<material.Count)Texto(40,y+15+k*12,material[i],i==0?9:8,i==0);
                     if(i<codes.Count)Texto(470,y+15+k*12,codes[i],8);
                     if(i<units.Count)Texto(680,y+15+k*12,units[i],8);}
                    y+=h;offset+=take;if(offset<max)Nueva(p);
                }
                indice++;
            }
            var notes=Wrap(p.Observaciones,758,9);
            if(!string.IsNullOrWhiteSpace(p.Observaciones))
            {
                if(y+42>505)Nueva(p,false);
                y+=16;Texto(32,y,"OBSERVACIONES",9,true);y+=15;
                foreach(var line in notes){if(y>500)Nueva(p,false);Texto(32,y,line,9);y+=13;}
            }
            for(int i=0;i<paginas.Count;i++)
            {pagina=paginas[i];Rect(32,533,778,1,"0.8 0.85 0.85");float fy=548;foreach(var line in Wrap(Presupuesto.Aviso,714,8)){Texto(32,fy,line,8);fy+=11;}Texto(761,568,(i+1)+" / "+paginas.Count,8);}
            return Serializar();
        }
        private byte[] Serializar()
        {
            var enc=Encoding.GetEncoding(1252);var objects=new List<byte[]>();
            Action<string> add=s=>objects.Add(enc.GetBytes(s));
            add("<< /Type /Catalog /Pages 2 0 R >>");
            add("<< /Type /Pages /Count "+paginas.Count+" /Kids ["+string.Join(" ",Enumerable.Range(0,paginas.Count).Select(i=>(5+i*2)+" 0 R"))+"] >>");
            add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            for(int i=0;i<paginas.Count;i++)
            {add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents "+(6+i*2)+" 0 R >>");var data=enc.GetBytes(paginas[i].ToString());add("<< /Length "+data.Length+" >>\nstream\n"+paginas[i]+"endstream");}
            using(var output=new MemoryStream())
            {
                Action<string> write=s=>{var b=enc.GetBytes(s);output.Write(b,0,b.Length);};write("%PDF-1.4\n%âãÏÓ\n");var offsets=new List<long>{0};
                for(int i=0;i<objects.Count;i++){offsets.Add(output.Position);write((i+1)+" 0 obj\n");output.Write(objects[i],0,objects[i].Length);write("\nendobj\n");}
                long xref=output.Position;write("xref\n0 "+(objects.Count+1)+"\n0000000000 65535 f \n");foreach(var off in offsets.Skip(1))write(off.ToString("D10")+" 00000 n \n");
                write("trailer\n<< /Size "+(objects.Count+1)+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF\n");return output.ToArray();
            }
        }
    }
}
