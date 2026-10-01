# Migraciones manuales de la base local

Los scripts de esta carpeta se ejecutan manualmente en SQL Server Management Studio o Azure Data Studio, siempre sobre la base local correcta. La aplicación no modifica la estructura de la base durante el arranque.

## Orden para una base existente

1. `Agregar-Fotos-Productos.sql`
2. `Agregar-Colores-Cables.sql`
3. `Agregar-Carritos-Persistentes.sql`
4. `Agregar-Estado-Solicitudes.sql`

Los dos primeros ya fueron aplicados y probados en la base local durante las etapas anteriores. El tercero es nuevo y habilita la recuperación del carrito después de expirar la sesión o reiniciar el sitio.

## Comprobación del carrito

Después de ejecutar `Agregar-Carritos-Persistentes.sql`:

```sql
SELECT UsuarioId, Actualizado
FROM dbo.EC_Carritos
ORDER BY Actualizado DESC;
```

La tabla puede estar vacía hasta que un usuario agregue o modifique un producto en su lista.

## Estado de las solicitudes

`Agregar-Estado-Solicitudes.sql` agrega el estado `Preparada` o `Compartida`, la fecha de la última acción y el canal elegido. Después de ejecutarlo se puede comprobar con:

```sql
SELECT Id, Nombre, EstadoSolicitud, CompartidaFecha, CanalCompartido
FROM dbo.EC_Presupuestos
ORDER BY Id DESC;
```

La aplicación funciona sin esta migración; en ese caso muestra todas las solicitudes como preparadas y no persiste el cambio de estado.

## Compatibilidad

Si `EC_Carritos` todavía no existe, el programa continúa utilizando la sesión. Si las columnas de estado todavía no existen, el envío también sigue funcionando y solo omite el registro del estado. Esto permite probar primero el código y aplicar las migraciones cuando se decida, sin interrumpir el uso local.
