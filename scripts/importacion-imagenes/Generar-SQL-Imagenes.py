import argparse
import csv
from pathlib import Path


def sql_text(value):
    return "N'" + str(value).replace("'", "''") + "'"


def load_ready_rows(path):
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = [row for row in csv.DictReader(handle, delimiter=";") if row["Estado"] == "LISTO"]
    keys = {(row["CodigoCatalogo"].strip().upper(), row["Marca"].strip().upper()) for row in rows}
    if len(keys) != len(rows):
        raise RuntimeError("El mapeo LISTO contiene claves CodigoCatalogo + Marca duplicadas.")
    return rows


def generate(rows):
    values = []
    for row in rows:
        values.append(
            "    (" + ", ".join([
                sql_text(row["CodigoCatalogo"].strip()),
                sql_text(row["Marca"].strip()),
                sql_text(row["Imagen"].strip()),
            ]) + ")"
        )
    inserts = ",\n".join(values) + ";"
    return f"""-- Actualización masiva de fotografías extraídas de CATALOGADOR V2.0.
-- Este archivo no crea ni modifica productos. Solamente completa EC_Productos.Imagen.
-- IMPORTANTE: queda en modo simulación. Revise los resultados y cambie @Confirmar a 1 para aplicar.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Confirmar bit = 0; -- 0 = vista previa sin cambios; 1 = actualizar y confirmar.

IF OBJECT_ID(N'dbo.EC_Productos', N'U') IS NULL
    THROW 50001, 'No existe dbo.EC_Productos en la base seleccionada.', 1;

IF COL_LENGTH(N'dbo.EC_Productos', N'CodigoCatalogo') IS NULL
   OR COL_LENGTH(N'dbo.EC_Productos', N'Marca') IS NULL
   OR COL_LENGTH(N'dbo.EC_Productos', N'Imagen') IS NULL
    THROW 50002, 'EC_Productos no contiene CodigoCatalogo, Marca e Imagen.', 1;

-- Permite volver a ejecutar el archivo completo en la misma pestaña de SSMS.
DROP TABLE IF EXISTS #Candidatos;
DROP TABLE IF EXISTS #Diagnostico;
DROP TABLE IF EXISTS #MapeoImagenes;

CREATE TABLE #MapeoImagenes
(
    CodigoCatalogo nvarchar(160) COLLATE DATABASE_DEFAULT NOT NULL,
    Marca nvarchar(120) COLLATE DATABASE_DEFAULT NOT NULL,
    Imagen nvarchar(260) COLLATE DATABASE_DEFAULT NOT NULL,
    PRIMARY KEY (CodigoCatalogo, Marca)
);

INSERT INTO #MapeoImagenes (CodigoCatalogo, Marca, Imagen)
VALUES
{inserts}

SELECT
    m.CodigoCatalogo,
    m.Marca,
    m.Imagen,
    COUNT(p.Id) AS Coincidencias,
    SUM(CASE WHEN p.Id IS NOT NULL AND NULLIF(LTRIM(RTRIM(p.Imagen)), N'') IS NOT NULL THEN 1 ELSE 0 END) AS ConImagenExistente
INTO #Diagnostico
FROM #MapeoImagenes AS m
LEFT JOIN dbo.EC_Productos AS p
    ON UPPER(LTRIM(RTRIM(p.CodigoCatalogo))) = UPPER(LTRIM(RTRIM(m.CodigoCatalogo)))
   AND UPPER(LTRIM(RTRIM(p.Marca))) = UPPER(LTRIM(RTRIM(m.Marca)))
GROUP BY m.CodigoCatalogo, m.Marca, m.Imagen;

SELECT
    DB_NAME() AS BaseSeleccionada,
    (SELECT COUNT(*) FROM #MapeoImagenes) AS ClavesEnMapeo,
    SUM(CASE WHEN Coincidencias = 0 THEN 1 ELSE 0 END) AS SinCoincidencia,
    SUM(CASE WHEN Coincidencias = 1 THEN 1 ELSE 0 END) AS CoincidenciaUnica,
    SUM(CASE WHEN Coincidencias > 1 THEN 1 ELSE 0 END) AS CoincidenciaMultiple,
    SUM(CASE WHEN Coincidencias = 1 AND ConImagenExistente > 0 THEN 1 ELSE 0 END) AS YaTenianImagen,
    SUM(CASE WHEN Coincidencias = 1 AND ConImagenExistente = 0 THEN 1 ELSE 0 END) AS ParaActualizar
FROM #Diagnostico;

SELECT N'SIN_COINCIDENCIA' AS Estado, CodigoCatalogo, Marca, Imagen, Coincidencias
FROM #Diagnostico
WHERE Coincidencias = 0
UNION ALL
SELECT N'COINCIDENCIA_MULTIPLE', CodigoCatalogo, Marca, Imagen, Coincidencias
FROM #Diagnostico
WHERE Coincidencias > 1
ORDER BY Estado, Marca, CodigoCatalogo;

SELECT
    N'IMAGEN_EXISTENTE' AS Estado,
    p.Id,
    p.CodigoCatalogo,
    p.Marca,
    p.Imagen AS ImagenActual,
    d.Imagen AS ImagenPropuesta
FROM #Diagnostico AS d
JOIN dbo.EC_Productos AS p
    ON UPPER(LTRIM(RTRIM(p.CodigoCatalogo))) = UPPER(LTRIM(RTRIM(d.CodigoCatalogo)))
   AND UPPER(LTRIM(RTRIM(p.Marca))) = UPPER(LTRIM(RTRIM(d.Marca)))
WHERE d.Coincidencias = 1
  AND d.ConImagenExistente > 0
ORDER BY p.Marca, p.CodigoCatalogo;

SELECT
    p.Id,
    p.CodigoCatalogo,
    p.Marca,
    d.Imagen
INTO #Candidatos
FROM #Diagnostico AS d
JOIN dbo.EC_Productos AS p
    ON UPPER(LTRIM(RTRIM(p.CodigoCatalogo))) = UPPER(LTRIM(RTRIM(d.CodigoCatalogo)))
   AND UPPER(LTRIM(RTRIM(p.Marca))) = UPPER(LTRIM(RTRIM(d.Marca)))
WHERE d.Coincidencias = 1
  AND d.ConImagenExistente = 0
  AND NULLIF(LTRIM(RTRIM(p.Imagen)), N'') IS NULL;

SELECT N'VISTA_PREVIA' AS Estado, Id, CodigoCatalogo, Marca, Imagen
FROM #Candidatos
ORDER BY Marca, CodigoCatalogo;

IF @Confirmar = 0
BEGIN
    PRINT N'SIMULACIÓN FINALIZADA: no se modificó la base. Para aplicar, cambie @Confirmar a 1 y ejecute nuevamente el archivo completo.';
    DROP TABLE IF EXISTS #Candidatos;
    DROP TABLE IF EXISTS #Diagnostico;
    DROP TABLE IF EXISTS #MapeoImagenes;
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE p
       SET p.Imagen = c.Imagen
    FROM dbo.EC_Productos AS p
    JOIN #Candidatos AS c ON c.Id = p.Id
    WHERE NULLIF(LTRIM(RTRIM(p.Imagen)), N'') IS NULL;

    DECLARE @Actualizados int = @@ROWCOUNT;
    DECLARE @Esperados int = (SELECT COUNT(*) FROM #Candidatos);

    IF @Actualizados <> @Esperados
        THROW 50003, 'La cantidad actualizada cambió durante la ejecución. Se revierte la operación.', 1;

    COMMIT TRANSACTION;

    SELECT N'APLICADO' AS Estado, @Actualizados AS ProductosActualizados, DB_NAME() AS BaseSeleccionada;

    DROP TABLE IF EXISTS #Candidatos;
    DROP TABLE IF EXISTS #Diagnostico;
    DROP TABLE IF EXISTS #MapeoImagenes;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mapping", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    rows = load_ready_rows(args.mapping)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(generate(rows), encoding="utf-8-sig")
    print(f"Filas LISTO incluidas: {len(rows)}")


if __name__ == "__main__":
    main()
