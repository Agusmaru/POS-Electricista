# Etapa 6 — Colores para cables unipolares

## Alcance implementado

- Los productos cuyo nombre es `cable unipolar` o cuyo código comienza con `UNIP-` requieren color.
- Colores disponibles: Marrón, Negro, Rojo, Azul, Verde-Amarillo y Blanco.
- El carrito agrupa por producto y color: repetir la misma combinación suma cantidades; otro color crea otro renglón.
- El color se conserva al confirmar la orden y se muestra en el presupuesto y en el PDF.
- Los presupuestos anteriores continúan siendo válidos con color vacío.

## Base de datos

- `EC_Colores`: catálogo administrado de colores.
- `EC_ProductoColores`: relación entre productos y colores disponibles.
- `EC_Items.ColorId`: referencia opcional al color.
- `EC_Items.ColorNombre`: copia histórica del nombre elegido.

La aplicación aplica la migración de forma idempotente al iniciar. Para ejecutarla manualmente en MonsterASP.NET se puede usar `scripts/Agregar-Colores-Cables.sql` sobre la base seleccionada.

## Prueba realizada

1. Se agregó `UNIP-050` Marrón con cantidad 10.
2. Se agregó el mismo producto Azul con cantidad 20: quedaron dos renglones.
3. Se agregó Marrón con cantidad 5: el renglón Marrón quedó en 15 y no se creó otro.
4. Se verificó la vista responsive a 390 px.
5. Se generó y revisó el PDF de prueba con ambos colores y sin precios.
