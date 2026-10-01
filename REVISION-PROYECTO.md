# Revisión técnica del proyecto POS Electricista

Última actualización: 1 de octubre de 2026

Proyecto: `Solucion.Net10.Core`

Tecnología: ASP.NET Core 10 y SQL Server

## Estado general

La solución compila con 0 errores y 0 advertencias. Las cuatro pruebas automáticas actuales pasan correctamente. La navegación local fue verificada con SQL Server disponible en escritorio y en un viewport móvil de 390 × 844, tanto en tema claro como oscuro.

No se realizaron cambios, publicaciones ni configuraciones en MonsterASP.NET.

## Correcciones completadas

- Las fotos se guardan en `EC_Fotos` y se vinculan con los productos mediante `EC_ProductosFotos`.
- Un producto puede tener varias fotos y una sola foto principal.
- Desvincular una foto no elimina el archivo físico compartido.
- Se corrigió la selección de la foto principal y la lectura de productos que comparten imagen.
- Los administradores pueden activar y desactivar productos.
- El filtro del catálogo permite ver activos, inactivos o todos y conserva el estado al editar.
- La lista vuelve a validar producto, estado y color antes de generar una solicitud.
- Todas las cantidades de catálogo, carrito y presupuesto deben ser enteras entre 1 y 1.000.000.
- Se retiraron precio, subtotal y total de catálogo, carrito, presupuesto y PDF.
- La interfaz presenta el proceso como una solicitud de cotización: `Lista de materiales`, `Agregar a lista`, `Ver lista` y `Generar solicitud`.
- El historial se presenta como `Solicitudes` y filtra, ordena y pagina en SQL Server en bloques de 20.
- Después de generar una solicitud se muestran acciones separadas para descargar el PDF, enviarlo por WhatsApp o enviarlo por correo.
- El mensaje para compartir incluye los materiales y recuerda adjuntar el PDF cuando el navegador no permite compartir archivos directamente.
- El proveedor, su WhatsApp y su correo se configuran en la sección `Proveedor` de `Web/appsettings.json`.
- El estado opcional `Preparada` o `Compartida` se habilita con `scripts/Agregar-Estado-Solicitudes.sql`.
- El PDF se titula `Solicitud de cotización de materiales` e incluye número, fecha, obra, solicitante, email, materiales y pedido de cotización, sin precios.
- El inicio de sesión tiene límite de ocho intentos por IP cada cinco minutos y usa una verificación ficticia para emails inexistentes.
- El catálogo entrega copias defensivas de sus productos y recarga los datos sin mantener un bloqueo global durante la consulta SQL.
- Los parámetros SQL comunes declaran tipos explícitos para enteros, booleanos, fechas, decimales y textos.
- `/salud` comprueba que el proceso responda y `/disponibilidad` verifica SQL Server y la carpeta de imágenes.
- La gestión física de imágenes se separó en `ImagenProductoService`.
- El PDF utiliza la identidad `FURNARIUS ENERGY | ENERGÍA RENOVABLE`.
- Se corrigió la legibilidad de los indicadores y títulos de presupuestos en tema oscuro sin afectar el tema claro.
- La aplicación dejó de ejecutar cambios de estructura de base al arrancar.
- Se evitó el proveedor de registro de eventos de Windows para que la ejecución local no requiera permisos de administrador.

## Carritos persistentes

Se agregó soporte opcional para guardar el carrito por usuario en SQL Server. Hasta ejecutar el script, la aplicación conserva automáticamente el comportamiento anterior basado en sesión y no deja de funcionar.

Ejecutar manualmente:

`scripts/Agregar-Carritos-Persistentes.sql`

Después de crear `EC_Carritos`, cada cambio del carrito se guarda en sesión y en SQL. Al generar la orden se elimina el borrador persistido.

## Pruebas automáticas

El proyecto `Pruebas` cubre actualmente:

1. Rechazo de cantidades decimales.
2. Copia correcta de producto, cantidad, color y observaciones al presupuesto.
3. Datos de la solicitud, marca Furnarius y ausencia de precio y total en el PDF.
4. Ausencia de propiedades de importe en los modelos de dominio.

Comando:

```powershell
dotnet test FluxElectricistas.slnx
```

Resultado al 1 de octubre de 2026: 4 aprobadas, 0 fallidas.

## Verificación visual realizada

- Catálogo en modo lista: columnas Producto, Código, Marca, Descripción y Acciones visibles correctamente.
- Catálogo en modo tarjetas: 30 tarjetas por página, imágenes visibles y sin referencias a precios.
- Historial de solicitudes: registros leídos desde la base local, resumen y tarjetas legibles en ambos temas.
- Vista móvil: sin desplazamiento horizontal; ancho del documento 375 px y tarjetas de 343 px.
- Endpoint `/disponibilidad`: HTTP 200 con `sql: true` e `imagenes: true` al ejecutar con acceso normal al SQL Server local.

## Trabajo estructural futuro

Estas tareas no bloquean el funcionamiento actual y conviene hacerlas por módulos para reducir riesgo:

- Migrar gradualmente el resto de las consultas SQL a métodos asincrónicos. La apertura de conexión y el endpoint de disponibilidad ya son asincrónicos.
- Continuar separando `PortalController` en controladores de catálogo, carrito, presupuestos, productos y usuarios. La gestión de imágenes ya fue extraída.
- Activar análisis nullable por proyecto y corregir las advertencias en etapas pequeñas.
- Ampliar las pruebas con una base aislada para permisos, concurrencia, filtros avanzados y fotos compartidas.

## Archivos SQL manuales relevantes

- `scripts/Agregar-Fotos-Productos.sql`: galería y vínculos de fotos.
- `scripts/Agregar-Colores-Cables.sql`: colores asociados a productos.
- `scripts/Agregar-Carritos-Persistentes.sql`: borradores de lista por usuario.
- `scripts/Agregar-Estado-Solicitudes.sql`: estado y canal de las solicitudes compartidas.

La aplicación no ejecuta estos scripts automáticamente.
