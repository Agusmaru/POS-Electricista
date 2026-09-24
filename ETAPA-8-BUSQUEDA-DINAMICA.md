# Etapa 8 — Catálogo dinámico y búsqueda avanzada

## Objetivo

Cargar una representación liviana del catálogo una sola vez por sesión y realizar en el navegador las búsquedas, reglas, ordenamiento y paginación de 30 productos. SQL Server continúa siendo la fuente oficial y las operaciones del carrito se validan en el servidor.

## Flujo previsto

1. `/Catalogo/Datos` entrega los productos autorizados para el usuario actual.
2. El navegador conserva los datos en memoria y opcionalmente en `sessionStorage`.
3. La vista renderiza solamente los 30 productos de la página seleccionada.
4. Las imágenes usan carga diferida y se solicitan únicamente al mostrarse.
5. Un cambio realizado desde el ABM invalida la versión del catálogo.

## Búsqueda avanzada

Cada regla contiene:

- unión lógica: Y, O, Y NO u O NO;
- campo;
- operador;
- valor.

Campos iniciales:

- Producto.
- Código.
- Marca.
- Descripción.
- Tipo.
- Estado.
- Imagen.

Operadores iniciales:

- Contiene y no contiene.
- Es y no es.
- Empieza con y termina con.
- Está vacío y no está vacío.

Se admitirán hasta diez criterios. Los grupos anidados y las búsquedas guardadas quedan para una ampliación posterior.

## Entregas

### 8.1 Datos y paginación — completada

- Endpoint de catálogo liviano.
- Caché compartida de .NET.
- Versión e invalidación del catálogo.
- Paginación local de 30 elementos.
- Conservación de lista o tarjetas.

Verificación realizada:

- Compilación Release sin errores ni advertencias.
- Aplicación local y control de salud con respuesta HTTP 200.
- Acceso comprobado con Administrador y Asesor.
- Endpoint y página con la misma versión de catálogo.
- 1677 productos disponibles y 30 filas renderizadas inicialmente.
- Sintaxis JavaScript y cambios de Git verificados.

### 8.2 Constructor de reglas — completada

- Alta y eliminación de criterios.
- Operadores dependientes del campo.
- Combinación lógica.
- Actualización dinámica del resultado.
- Adaptación responsive.

Decisiones de funcionamiento:

- Las reglas avanzadas se aplican junto con los filtros simples.
- La primera regla inicia el cálculo y las siguientes se combinan en el orden mostrado.
- Las reglas se conservan durante la sesión al alternar entre Lista y Tarjetas.
- “Limpiar” elimina filtros simples y reglas; “Quitar todas” elimina solamente las reglas.
- Estado e Imagen muestran valores cerrados para evitar errores de escritura.

Verificación realizada:

- Los siete campos y sus operadores se renderizan correctamente.
- Regla “Producto contiene abrazadera”: 14 coincidencias.
- Combinación con “Y NO Marca es DAISA”: 0 coincidencias para los datos actuales.
- Regla “Imagen es Con imagen”: 968 coincidencias.
- Persistencia comprobada al pasar de Tarjetas a Lista.
- Vista móvil verificada a 390 × 844 píxeles.
- Consola del navegador sin errores.

### 8.3 Terminación

- Resumen de filtros activos.
- Estado vacío y recuperación de errores.
- Accesibilidad mediante teclado.
- Pruebas con Administrador y Asesor.
- Evaluación de búsquedas guardadas.
