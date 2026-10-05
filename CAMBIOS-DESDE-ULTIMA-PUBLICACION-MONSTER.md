# Cambios desde la última publicación en Monster

Fecha del informe: 2 de octubre de 2026

Proyecto: `Solucion.Net10.Core`
Repositorio: `Agusmaru/POS-Electricista`

## Punto de comparación confirmado

La última publicación identificable en Monster corresponde al 25 de septiembre de 2026:

- Carpeta publicada: `publicar-monster-20260925`, generada el 25/09/2026 a las 14:51.
- Commit asociado: `634a0f5` — `Configura dominio de produccion en Monster`, creado el 25/09/2026 a las 14:52.
- Versión actual de GitHub: `0441061` — `Mejora flujo de solicitudes y persistencia local`.

Por lo tanto, para recopilar lo pendiente de publicar no alcanza con revisar solamente el último commit. El rango correcto es:

```text
634a0f5..0441061
```

Ese rango contiene dos commits:

1. `cf11c91` — Mejora gestión de productos e imágenes.
2. `0441061` — Mejora flujo de solicitudes y persistencia local.

## Cambios funcionales pendientes de publicar

### Catálogo y productos

- Los administradores pueden activar y desactivar productos desde el catálogo.
- El filtro de estado permite ver productos activos, inactivos o todos.
- El filtro seleccionado se conserva después de editar o cambiar el estado de un producto.
- Se retiró la visualización y el manejo de precios del catálogo.
- Se mantienen los modos de lista y tarjetas, la búsqueda avanzada y la paginación de 30 productos.
- La comprobación de archivos evita mostrar imágenes inexistentes.

### Galería de fotografías

- Cada producto puede tener varias fotografías asociadas.
- Una misma fotografía puede utilizarse en varios productos.
- Cada producto tiene una única imagen principal.
- El administrador puede cargar hasta 8 archivos por operación y asociar hasta 12 fotos a un producto.
- Se aceptan archivos JPG, PNG y WebP de hasta 5 MB cada uno.
- Desvincular una fotografía no elimina el archivo físico, evitando romper otros productos que la compartan.
- Se corrigieron la carga de imágenes compartidas y el cambio de foto principal.
- La administración física de archivos quedó centralizada en `ImagenProductoService`.

### Lista de materiales

- La interfaz dejó de presentar la operación como una compra o carrito y ahora utiliza `Lista de materiales`.
- Todas las cantidades deben ser números enteros entre 1 y 1.000.000.
- Antes de generar la solicitud se vuelven a consultar los productos y colores en SQL Server.
- No se genera una solicitud si un producto fue desactivado, eliminado o perdió el color seleccionado.
- Los datos descriptivos se actualizan con la versión vigente del catálogo antes de guardar.
- La lista puede persistirse por usuario en SQL Server y recuperarse después de reiniciar el sitio o expirar la sesión.

### Solicitudes de cotización

- La interfaz e historial ahora se presentan como `Solicitudes`.
- Después de generar una solicitud aparece una confirmación con tres acciones independientes:
  - Descargar PDF.
  - Enviar por WhatsApp.
  - Enviar por correo.
- WhatsApp y correo reciben un mensaje preparado con el detalle de materiales.
- El PDF se descarga antes de abrir el medio elegido para que el usuario pueda adjuntarlo manualmente.
- Se registra el estado `Preparada` o `Compartida`, la fecha de la última acción y el canal elegido.
- El historial filtra, ordena y pagina directamente en SQL Server en bloques de 20.
- Se retiraron precios, subtotales y totales de la solicitud y del PDF.

### PDF

- Nuevo título: `Solicitud de cotización de materiales`.
- Usa la identidad de Furnarius Energy.
- Incluye número, fecha, nombre de obra, solicitante, correo y materiales.
- Incluye la leyenda `Por favor, cotizar los materiales detallados.`.
- No contiene precios ni totales.

### Seguridad, estabilidad y operación

- El inicio de sesión limita a ocho intentos por IP cada cinco minutos.
- Se realiza una verificación criptográfica ficticia para emails inexistentes, reduciendo diferencias de tiempo observables.
- La aplicación dejó de modificar automáticamente la estructura de SQL Server durante el arranque.
- Se agregaron parámetros SQL tipados para los valores habituales.
- El caché devuelve copias de los productos y ya no expone objetos compartidos modificables.
- La recarga del catálogo utiliza exclusión separada, conserva la versión disponible y reintenta una vez ante timeout.
- Se agregó `/disponibilidad` para comprobar SQL Server y la carpeta de imágenes, manteniendo `/salud` para el proceso web.
- El registro utiliza consola y depuración, evitando depender del registro de eventos de Windows.

### Calidad y documentación

- Se incorporó el proyecto automático `Pruebas`.
- Hay pruebas para cantidades enteras, copia de producto/color, contenido del PDF y ausencia de importes en el dominio.
- Se actualizaron las pruebas web del historial y de la pantalla de solicitud.
- La solución fue validada con 0 errores, 0 advertencias, 4 pruebas .NET aprobadas y 22 verificaciones web aprobadas.

## Cambios necesarios en la base de Monster

Ejecutar, sobre la base de datos del sitio y antes de publicar el código:

```text
scripts/Actualizar-Monster-Desde-2026-09-25.sql
```

El archivo es idempotente y reúne:

1. `EC_Fotos` y `EC_ProductosFotos`, incluyendo la migración de `EC_Productos.Imagen`.
2. `EC_Carritos`, para conservar listas por usuario.
3. `EstadoSolicitud`, `CompartidaFecha` y `CanalCompartido` en `EC_Presupuestos`.

No incluye `Agregar-Colores-Cables.sql` porque esa estructura ya formaba parte de la versión publicada el 25/09/2026. Tampoco elimina las columnas históricas de precios: la aplicación actual las ignora y conservarlas reduce el riesgo de la actualización.

El SQL migra las imágenes que ya estén informadas en `EC_Productos.Imagen` dentro de Monster. No copia archivos físicos ni fotografías nuevas cargadas únicamente en la base local. Si existen imágenes locales que también deban pasar a producción, habrá que subir sus archivos a `wwwroot/uploads/productos` y migrar sus asociaciones por separado.

## Configuración requerida antes de publicar

Completar la sección `Proveedor` en la configuración de producción:

```json
"Proveedor": {
  "Nombre": "Furnarius Energy",
  "WhatsApp": "NUMERO_CON_CODIGO_DE_PAIS",
  "Email": "CORREO_DEL_PROVEEDOR"
}
```

La cadena de conexión debe continuar configurada mediante `ELECTRICISTAS_CORE10_CONNECTION_STRING`.

## Orden recomendado de actualización

1. Respaldar la base de datos de Monster.
2. Detener temporalmente el sitio o colocar `app_offline.htm`.
3. Seleccionar la base correcta en WebMSSQL o SSMS.
4. Ejecutar `scripts/Actualizar-Monster-Desde-2026-09-25.sql` completo.
5. Verificar el resultado final del script.
6. Publicar el código correspondiente al commit `0441061` o posterior.
7. Conservar sin reemplazar:
   - `App_Data/keys`.
   - `wwwroot/uploads/productos`.
8. Configurar nombre, WhatsApp y correo del proveedor.
9. Reiniciar el sitio.
10. Comprobar `/salud`, `/disponibilidad`, inicio de sesión, catálogo, imágenes, lista, generación de solicitud y PDF.

## Alcance

Este documento y el SQL fueron preparados sin modificar ni publicar nada en Monster. La ejecución sobre la base remota y el despliegue continúan siendo pasos manuales.
