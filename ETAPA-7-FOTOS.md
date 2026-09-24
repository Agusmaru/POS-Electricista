# Etapa 7 — Fotografías de productos

## Decisión de almacenamiento

Las imágenes se alojan dentro del sitio web en:

`Web/wwwroot/uploads/productos`

En MonsterASP.NET la aplicación crea y administra esta carpeta usando `IWebHostEnvironment.WebRootPath`. Los archivos quedan disponibles públicamente mediante rutas como:

`/uploads/productos/identificador.jpg`

La columna `EC_Productos.Imagen` conserva únicamente el nombre seguro generado por la aplicación. Esto evita acoplar los datos al dominio actual y permite cambiar el dominio sin modificar los productos.

## Funcionalidad disponible

- Carga desde el ABM de productos.
- Formatos JPG, PNG y WebP.
- Tamaño máximo de 5 MB.
- Nombre generado con GUID para evitar colisiones.
- Reemplazo y eliminación de la imagen anterior.
- Marcador “Sin imagen” cuando el archivo no existe.
- Visualización en catálogo y carrito.

## Publicación en MonsterASP.NET

La carpeta `wwwroot/uploads/productos` contiene datos generados en producción y debe conservarse entre publicaciones.

Antes de un despliegue que reemplace o reinicie el contenido del sitio:

1. Descargar un respaldo de `wwwroot/uploads/productos` mediante SFTP.
2. Publicar la nueva versión sin eliminar archivos adicionales del servidor.
3. Comprobar una imagen existente por su URL pública.
4. Restaurar el respaldo si el método de publicación reemplazó la carpeta.

Las imágenes entregadas por el cliente podrán cargarse desde el ABM o incorporarse masivamente cuando exista una relación código de catálogo → archivo.
