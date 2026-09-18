# Etapa 4 · Carrito de compra y presupuesto

## Implementado

- Botón para agregar productos desde las vistas de lista y tarjetas del catálogo.
- Selección de cantidad antes de agregar cada producto.
- Contador visible del carrito en el catálogo y la navegación.
- Carrito independiente por usuario, conservado durante cuatro horas de inactividad.
- Agrupación de cantidades al agregar nuevamente el mismo producto.
- Nombre de obra generado automáticamente y editable.
- Pantalla de revisión con productos, códigos, marcas, imágenes, cantidades, precios y total aproximado.
- Actualización de cantidades, eliminación individual y vaciado completo.
- Acceso para volver al catálogo sin perder el contenido.
- Aviso visible de que los precios deben consultarse antes de comprar.
- Adaptación completa para computadora, tablet y celular.

## Prueba manual

1. Ingresar y abrir el catálogo.
2. Agregar productos desde las vistas Lista y Tarjetas.
3. Navegar a otra sección y comprobar que el contador se conserva.
4. Abrir Carrito y editar el nombre de obra.
5. Modificar cantidades, quitar un producto y volver al catálogo.
6. Vaciar el carrito y comprobar el estado vacío.

## Validación automática

El script `scripts/probar_etapa4.cjs` verifica el inicio de sesión, renderizado del botón, agregado desde el catálogo, contador, persistencia durante la navegación, nombre automático y editable, actualización de cantidades, total, eliminación, vaciado y adaptación móvil. Resultado guardado en `datos/pruebas-etapa4.json` con 13 comprobaciones correctas.

La confirmación definitiva de la orden y la generación del PDF corresponden a la etapa 5.
