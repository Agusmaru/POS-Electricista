# Etapa 7 — Importación masiva desde CATALOGADOR V2.0

## Resultado de la extracción

- Fuente: `CATALOGADOR V2.0.xlsm`.
- Archivos incrustados encontrados: 116.
- Imágenes extraídas: 116, sin duplicados binarios.
- Formatos: 75 PNG y 41 JPG.
- Relaciones de filas de producto con imágenes: 1026.
- Claves distintas `CodigoCatalogo + Marca`: 997.
- Claves con una única imagen candidata: 969.
- Claves ambiguas: 28, con dos imágenes candidatas cada una.

Las imágenes se guardaron directamente en `Web/wwwroot/uploads/productos`. Sus nombres se construyen con un hash del contenido, por ejemplo `catalogador-v2-<hash>.png`, para que una nueva ejecución sea estable y no produzca colisiones.

## Archivo de control

`scripts/importacion-imagenes/MAPEO-IMAGENES-CATALOGADOR.csv` contiene:

- Estado: `LISTO` o `AMBIGUO`.
- Código de catálogo.
- Marca.
- Nombre del archivo extraído.
- Hoja y filas de origen del Excel.
- Nombre de referencia del producto.

La asociación se realiza mediante `CodigoCatalogo + Marca`. El `Id` numérico de SQL Server no se usa porque pertenece a la base de datos y no identifica de forma estable las filas del Excel.

## Estado de la base de datos

En esta etapa no se ejecutaron actualizaciones. El siguiente paso consiste en generar un SQL transaccional que:

1. cargue solamente las filas `LISTO`;
2. compare por código y marca normalizados;
3. no sobrescriba una imagen existente;
4. muestre una vista previa de coincidencias y descartes;
5. permita revertir la operación antes de confirmar.

Los casos `AMBIGUO` quedan excluidos hasta su revisión manual.
