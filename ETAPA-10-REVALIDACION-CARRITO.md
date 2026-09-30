# Etapa 10: revalidación del carrito

## Objetivo

Evitar que se genere una orden con productos desactivados, eliminados o con colores que dejaron de estar disponibles después de agregarlos al carrito.

## Comportamiento

Al confirmar el carrito, la aplicación vuelve a consultar el catálogo directamente en SQL Server:

- Comprueba que cada producto exista y continúe activo.
- Comprueba que el color siga asociado y activo.
- Actualiza nombre, descripción, marca, códigos, unidad e imagen antes de guardar la orden.
- Conserva la cantidad y las observaciones ingresadas por el usuario.
- Si encuentra un problema, conserva el carrito y muestra el detalle en la misma pantalla.
- No genera una orden parcial.
- Los administradores pueden activar o desactivar productos directamente desde el catálogo.
- Los asesores no ven esa acción y el servidor también rechaza intentos directos.

## Prueba sugerida

1. Iniciar sesión como asesor o administrador.
2. Agregar un producto al carrito.
3. En otra sesión administrativa, usar el botón **Desactivar** que aparece junto a **Editar**.
4. Volver al carrito original y confirmar.
5. Verificar que la orden no se genere y que el carrito indique el producto inactivo.
6. En el catálogo administrativo, cambiar el filtro Estado a **Inactivos**, pulsar **Activar** y comprobar que el producto desaparezca de esa vista y que el carrito pueda confirmarse.
7. Iniciar sesión como asesor y comprobar que no aparezcan los botones **Editar**, **Desactivar** ni **Activar**.
8. Repetir la prueba con un cable: agregarlo con un color y retirar esa asociación antes de confirmar.
9. Verificar que el mensaje identifique el color no disponible.

## Estado

Implementado, compilado y probado correctamente en la versión local el 30 de septiembre de 2026.
