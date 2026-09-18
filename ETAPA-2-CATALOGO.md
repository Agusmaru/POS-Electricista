# Etapa 2 · Catálogo y productos

## Implementado

- Búsqueda por producto, descripción, marca, categoría, códigos y tipo.
- Filtro por fabricante mediante el campo Marca.
- Columnas Producto, Código, Marca, Descripción, Imágenes y Precio.
- Precio inicial de nuevos productos en $0,00 y normalización a cero de precios vacíos existentes.
- Imagen principal en el ABM: carga, reemplazo y eliminación de JPG, PNG o WebP de hasta 5 MB.
- Nombre aleatorio para archivos cargados y validación del contenido real de la imagen.
- Marcador “Sin imagen” cuando el producto no tiene fotografía.
- Selector Lista/Tarjetas en escritorio y tablet, con preferencia conservada durante un año.
- Catálogo móvil presentado como tarjetas para evitar desplazamiento horizontal.

## Prueba manual

1. Ingresar como administrador y abrir Catálogo.
2. Buscar por producto, código o marca y aplicar el filtro Marca.
3. Alternar entre Lista y Tarjetas, navegar a otra pantalla y volver.
4. Editar un producto, cargar una imagen y guardar.
5. Volver a editarlo para reemplazar o quitar la imagen.

## Validación automática

El script `scripts/probar_etapa2.cjs` verifica columnas, precios, búsqueda, fabricante, modos de vista, persistencia, carga y eliminación de imagen y adaptación móvil. Resultado guardado en `datos/pruebas-etapa2.json`.
