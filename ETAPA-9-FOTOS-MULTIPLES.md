# Etapa 9: fotos múltiples por producto

## Objetivo

Permitir que un producto tenga varias fotos y que una misma foto pueda utilizarse en varios productos, sin borrar archivos físicos al desvincularlos.

## Modelo de datos

- `EC_Fotos`: registra cada archivo una sola vez.
- `EC_ProductosFotos`: vincula productos y fotos, guarda su orden e identifica la foto principal.
- `EC_Productos.Imagen`: se conserva temporalmente por compatibilidad y se sincroniza con la foto principal.

## Funcionamiento del ABM

- Permite cargar hasta 8 archivos por operación.
- Permite hasta 12 fotos asociadas por producto.
- Acepta JPG, PNG y WebP, con un máximo de 5 MB por archivo.
- Permite elegir una foto principal.
- Permite desvincular fotos individualmente.
- Desvincular no elimina el archivo de `wwwroot/uploads/productos`.
- Si se desvincula la principal, la primera foto restante se convierte en principal.

## Instalación local

1. Detener la aplicación.
2. Abrir SQL Server Management Studio y seleccionar `ELECTRICISTAS_CORE10_DB` en el selector de base. No ejecutarlo sobre `master`.
3. Ejecutar `scripts/Agregar-Fotos-Productos.sql`.
4. Verificar el resumen final de fotos, vínculos y productos migrados.
5. Iniciar la aplicación.
6. Editar un producto con foto y confirmar que aparezca en la galería como principal.
7. Agregar una segunda foto, guardarla y volver a abrir el producto.
8. Cambiar la foto principal y comprobar el catálogo.
9. Desvincular una foto compartida y comprobar que continúe visible en los demás productos.

## Publicación futura

Antes de publicar esta etapa en Monster, ejecutar el mismo script sobre la base remota. El código no debe desplegarse antes de que existan las tablas nuevas.


## Estado

Completado y probado correctamente en la versión local el 30 de septiembre de 2026. La base local fue migrada mediante `scripts/Agregar-Fotos-Productos.sql`.
