# Etapa 5 · Orden, PDF y revisión final

## Implementado

- Confirmación del carrito para generar una orden numerada.
- Guardado transaccional de la orden y sus productos en SQL Server.
- Vaciado automático del carrito después de confirmar.
- Consulta de órdenes desde el historial de presupuestos.
- Edición posterior de nombre, observaciones, materiales, cantidades y precios.
- Eliminación de “Casa de Electricidad” de las vistas, el flujo y el PDF.
- Generación y descarga del PDF desde la orden guardada.
- PDF con obra, número, fecha, responsable, productos, códigos, cantidades, precios, importes y total aproximado.
- Aviso visible de que los precios y el total deben consultarse y confirmarse.
- Vista de orden optimizada para celular: el catálogo adicional aparece solamente al buscar.
- Endpoint `/salud` para comprobar el estado de la aplicación en un servicio de nube.

## Validación

El script `scripts/probar_etapa5.cjs` recorre catálogo, carrito, confirmación, historial, descarga, contenido del PDF, eliminación de la orden temporal, vista móvil y endpoint de salud. La orden temporal se elimina del historial al terminar.

El archivo `lista de compra de test.pdf` es el resultado de referencia. Se verificó su estructura, texto y renderizado visual en formato A4 horizontal.
