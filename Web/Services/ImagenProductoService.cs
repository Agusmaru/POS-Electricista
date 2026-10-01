namespace Flux.Web.Services;

public sealed class ImagenProductoService
{
    private readonly string carpeta;

    public ImagenProductoService(IWebHostEnvironment entorno)
    {
        carpeta = Path.Combine(entorno.WebRootPath, "uploads", "productos");
    }

    public bool Disponible(string nombre)
    {
        var seguro = Path.GetFileName(nombre ?? "");
        return seguro != "" && File.Exists(Path.Combine(carpeta, seguro));
    }

    public async Task<string> GuardarAsync(IFormFile archivo, CancellationToken cancellationToken = default)
    {
        if (archivo.Length > 5 * 1024 * 1024) throw new ArgumentException("La imagen no puede superar 5 MB.");
        byte[] cabecera = new byte[12];
        await using var lectura = archivo.OpenReadStream();
        int leidos = await lectura.ReadAsync(cabecera, cancellationToken);
        string extension = leidos >= 4 && cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4e && cabecera[3] == 0x47 ? ".png"
            : leidos >= 3 && cabecera[0] == 0xff && cabecera[1] == 0xd8 && cabecera[2] == 0xff ? ".jpg"
            : leidos >= 12 && System.Text.Encoding.ASCII.GetString(cabecera, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(cabecera, 8, 4) == "WEBP" ? ".webp"
            : throw new ArgumentException("La imagen debe ser JPG, PNG o WebP.");
        Directory.CreateDirectory(carpeta);
        string nombre = Guid.NewGuid().ToString("N") + extension;
        lectura.Position = 0;
        await using var destino = File.Create(Path.Combine(carpeta, nombre));
        await lectura.CopyToAsync(destino, cancellationToken);
        return nombre;
    }

    public void EliminarArchivoNuevo(string nombre)
    {
        var seguro = Path.GetFileName(nombre ?? "");
        if (seguro == "") return;
        var ruta = Path.Combine(carpeta, seguro);
        if (File.Exists(ruta)) File.Delete(ruta);
    }
}
