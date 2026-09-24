# Ejecución manual del SQL de imágenes

Archivo: `Actualizar-Imagenes-Catalogador.sql`

El script contiene 969 claves `CodigoCatalogo + Marca` clasificadas como seguras en el Excel. La cantidad final de productos actualizables puede ser menor si una clave no existe en la base, está duplicada o ya tiene una imagen.

## Antes de ejecutar

1. Realizar un respaldo de la base de datos.
2. Subir los archivos `catalogador-v2-*` a `wwwroot/uploads/productos` del sitio en MonsterASP.NET.
3. Seleccionar en SQL Server Management Studio la base que contiene `dbo.EC_Productos`.
4. Confirmar que la primera línea configurable permanezca como `DECLARE @Confirmar bit = 0`.

## Primera ejecución: simulación

Ejecutar el archivo completo con `@Confirmar = 0`.

El script mostrará:

- nombre de la base seleccionada;
- claves sin coincidencia;
- coincidencias múltiples;
- productos que ya tienen imagen;
- productos candidatos a actualizar.

En este modo no se ejecuta ningún `UPDATE`.

## Aplicación

Después de revisar la vista previa, cambiar solamente:

`DECLARE @Confirmar bit = 1`

Ejecutar nuevamente el archivo completo. La actualización se realiza dentro de una transacción y se revierte automáticamente si la cantidad actualizada no coincide con la vista previa.

El archivo puede ejecutarse varias veces en la misma pestaña de SQL Server Management Studio. Al comenzar y finalizar elimina sus propias tablas temporales.

El script no sobrescribe valores existentes en `EC_Productos.Imagen` y no incluye los 28 casos ambiguos del Excel.

## Verificación

Después de aplicar, comprobar algunos productos del resultado `VISTA_PREVIA` en el catálogo web. La URL esperada de cada archivo es:

`/uploads/productos/catalogador-v2-<hash>.png`

o:

`/uploads/productos/catalogador-v2-<hash>.jpg`
