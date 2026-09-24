# Etapas del proyecto

## 1. Diseño responsive — completada

Menú, identidad visual y adaptación de las vistas para computadora, tablet y celular.

## 2. Catálogo y productos — completada

- Corregir el buscador.
- Añadir filtros por fabricante.
- Incorporar fotografías al ABM de productos.
- Mostrar Producto, Código, Marca, Descripción e Imágenes.
- Inicializar los precios en $0.
- Permitir alternar el catálogo entre vista de lista y vista de tarjetas.
- Conservar la vista elegida por el usuario al navegar y volver al catálogo.

## 3. Usuarios — completada

- ABM completo para crear, editar, activar y desactivar usuarios.
- Roles Administrador y Asesor.
- Búsqueda y filtros por rol y estado.
- Restablecimiento opcional de contraseña.
- Protección de la cuenta administradora en uso y del último administrador activo.

## 4. Carrito de compra y presupuesto — completada

- Añadir productos al carrito directamente desde el catálogo.
- Indicar y modificar cantidades.
- Quitar productos o vaciar el carrito.
- Mantener el carrito mientras el usuario navega por el sistema.
- Mostrar el resumen con productos, cantidades, precios y total aproximado.
- Generar automáticamente un nombre de obra y permitir editarlo.
- Incorporar una pantalla de revisión antes de confirmar el presupuesto.
- Adaptar todo el circuito a computadora, tablet y celular.

## 5. Orden, PDF y revisión final — completada

- Confirmar el carrito para generar una orden o presupuesto.
- Guardar la orden y sus productos para poder consultarla posteriormente.
- Generar el PDF a partir de la orden confirmada.
- Quitar “Casa de Electricidad” del presupuesto y del PDF.
- Informar que los precios y el total son aproximados y deben consultarse.
- Probar el circuito completo desde el catálogo hasta el PDF.
- Preparar la aplicación para su publicación en la nube.

## 8. Catálogo dinámico y búsqueda avanzada — en desarrollo

- Descargar una versión liviana del catálogo una vez por sesión. **Completado en 8.1.**
- Mantener SQL Server como fuente oficial de productos.
- Filtrar y paginar en bloques de 30 desde el navegador. **Completado en 8.1.**
- Cargar las imágenes únicamente cuando sean visibles. **Completado en 8.1.**
- Incorporar criterios independientes por Producto, Código, Marca, Descripción, Tipo, Estado e Imagen. **Completado en 8.2.**
- Combinar criterios mediante Y, O, Y NO y O NO. **Completado en 8.2.**
- Actualizar los filtros simples, las reglas avanzadas y la paginación sin recargar la página. **Completado en 8.1 y 8.2.**
- Invalidar la copia local cuando un administrador modifique el catálogo. **Completado en 8.1.**
- Validar nuevamente en el servidor los productos agregados al carrito.
