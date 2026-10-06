# Generar Presupuesto

Primera versión local, 5 de octubre de 2026. Acceso para usuarios autenticados desde el menú principal/hamburguesa: **Generar Presupuesto**, ruta `/GenerarPresupuesto`.

Alcance acordado: presupuesto comercial basado en el PDF de CLAUDIO. La integración fiscal con ARCA queda para otra etapa.

Incluye encabezado editable, las 15 filas del ejemplo, agregar/eliminar filas, cantidades con hasta 3 decimales, precios con hasta 2 decimales, IVA por fila, mano de obra, condiciones y nota editables, vista previa y descarga PDF. Logo y pie visual se extrajeron del PDF de referencia. El PDF identifica que no es una factura.

El primer ingreso usa los datos de ejemplo, con fecha actual. No hay numeración automática. Cambiar cliente, número, importes y condiciones antes de enviarlo. La mano de obra es un importe final manual como en el modelo, no un cálculo por potencia. Para discriminar su IVA, cargarla como un renglón y dejar el campo final en cero.

Guardar borrador conserva **un borrador por usuario**, reemplazando el anterior, en `Web/App_Data/presupuestos-comerciales/{usuarioId}.json`. No hay historial comercial todavía. Descarga y vista previa no guardan automáticamente el borrador. La carpeta debe conservarse y tener permiso de escritura al publicar. No se modificó SQL Server.

Las solicitudes existentes conservan su modelo y PDF sin precios. El nuevo dominio es `PresupuestoComercial`. El controlador se distribuye en archivos parciales para reutilizar la validación del usuario activo y el layout existentes.

Validación: compilación sin advertencias, 4 pruebas originales y 7 casos nuevos de cálculos/validación. La plantilla reconcilia neto USD 35.088,50, IVA USD 6.331,19 y total con mano de obra USD 47.397,69. Se corrigieron los subtotales inconsistentes del documento de origen. Prueba visual del PDF del ejemplo y de un documento de 55 filas.

Para revisar: iniciar el proyecto local, ingresar, abrir el menú y elegir Generar Presupuesto. Editar un concepto, añadir/quitar filas, guardar, recargar para comprobar el borrador y descargar el PDF. Las pruebas no emiten comprobantes fiscales ni envían mensajes.

## Actualización del 6 de octubre de 2026

Los nuevos presupuestos tienen cliente, CUIT, domicilio y localidad vacíos. La fecha inicial corresponde a Argentina y es editable. Los borradores existentes conservan sus datos y fecha. El número es de solo lectura, correlativo global de esta instalación, y se reserva al abrir el primer borrador o pulsar Nuevo presupuesto. Guardar, recargar o descargar conserva el número. El contador durable `secuencia.txt` y el bloqueo de archivo evitan duplicados entre usuarios/procesos. Un error de escritura puede dejar un salto sin reutilizar números. Los borradores anteriores se respaldan en `anteriores` al crear uno nuevo; aún no hay pantalla de historial. Respaldar toda la carpeta de presupuestos, incluido el contador. Sin integración ARCA.


### Ajuste de filas y asunto — 6 de octubre de 2026
- Los nuevos presupuestos comienzan sin asunto y sin artículos ni servicios.
- Los borradores existentes conservan sus datos; «Nuevo presupuesto» inicia uno vacío.
- Las filas se ordenan con la manija de arrastre, botones Subir/Bajar o flechas del teclado sobre la manija. El indicador marca dónde se insertará la fila; cancelar el gesto conserva el orden.
- El orden de pantalla se conserva al guardar y al generar el PDF. Los datos del ejemplo quedan únicamente como fixture de las pruebas de importes.

- Corrección del borrador local heredado: se vació únicamente el asunto del ejemplo en 1.json, con respaldo en anteriores. El valor predeterminado del modelo ya es vacío.

- A pedido del usuario se quitaron las filas del ejemplo del borrador local 1.json, con respaldo en anteriores. Tanto este borrador como los presupuestos nuevos abren Artículos y servicios sin filas.

- Mano de obra: importe predeterminado en 0 para nuevos presupuestos y borrador local actual; respaldo del borrador en anteriores. La prueba del ejemplo fija expresamente su importe histórico.
