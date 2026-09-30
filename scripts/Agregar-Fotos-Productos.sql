-- Ejecutar manualmente sobre la base de datos de la aplicación.
-- Crea una galería de fotos reutilizables y migra la columna EC_Productos.Imagen.
-- Es idempotente: puede ejecutarse más de una vez.
-- Seleccioná ELECTRICISTAS_CORE10_DB en el editor antes de ejecutarlo.
IF DB_NAME()=N'master' OR OBJECT_ID('dbo.EC_Productos','U') IS NULL
    THROW 50001, N'Seleccioná la base ELECTRICISTAS_CORE10_DB antes de ejecutar este script.', 1;

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.EC_Fotos','U') IS NULL
BEGIN
    CREATE TABLE dbo.EC_Fotos(
        Id int IDENTITY NOT NULL CONSTRAINT PK_EC_Fotos PRIMARY KEY,
        NombreArchivo nvarchar(260) NOT NULL,
        FechaCarga datetime2 NOT NULL CONSTRAINT DF_EC_Fotos_FechaCarga DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_EC_Fotos_NombreArchivo UNIQUE(NombreArchivo)
    );
END;

IF OBJECT_ID('dbo.EC_ProductosFotos','U') IS NULL
BEGIN
    CREATE TABLE dbo.EC_ProductosFotos(
        ProductoId int NOT NULL,
        FotoId int NOT NULL,
        Orden int NOT NULL CONSTRAINT DF_EC_ProductosFotos_Orden DEFAULT 1,
        EsPrincipal bit NOT NULL CONSTRAINT DF_EC_ProductosFotos_Principal DEFAULT 0,
        CONSTRAINT PK_EC_ProductosFotos PRIMARY KEY(ProductoId,FotoId),
        CONSTRAINT CK_EC_ProductosFotos_Orden CHECK(Orden>0),
        CONSTRAINT FK_EC_ProductosFotos_Producto FOREIGN KEY(ProductoId) REFERENCES dbo.EC_Productos(Id) ON DELETE CASCADE,
        CONSTRAINT FK_EC_ProductosFotos_Foto FOREIGN KEY(FotoId) REFERENCES dbo.EC_Fotos(Id)
    );
END;

IF NOT EXISTS(
    SELECT 1 FROM sys.indexes
    WHERE name='UX_EC_ProductosFotos_Principal' AND object_id=OBJECT_ID('dbo.EC_ProductosFotos'))
    CREATE UNIQUE INDEX UX_EC_ProductosFotos_Principal
        ON dbo.EC_ProductosFotos(ProductoId) WHERE EsPrincipal=1;

IF NOT EXISTS(
    SELECT 1 FROM sys.indexes
    WHERE name='IX_EC_ProductosFotos_Foto' AND object_id=OBJECT_ID('dbo.EC_ProductosFotos'))
    CREATE INDEX IX_EC_ProductosFotos_Foto ON dbo.EC_ProductosFotos(FotoId);

-- Cada archivo se registra una sola vez, aunque esté asociado a varios productos.
INSERT dbo.EC_Fotos(NombreArchivo)
SELECT DISTINCT LTRIM(RTRIM(p.Imagen))
FROM dbo.EC_Productos p
WHERE LTRIM(RTRIM(ISNULL(p.Imagen,N'')))<>N''
  AND NOT EXISTS(
      SELECT 1 FROM dbo.EC_Fotos f
      WHERE f.NombreArchivo=LTRIM(RTRIM(p.Imagen))
  );

-- La imagen anterior pasa a ser la primera foto y la principal del producto.
INSERT dbo.EC_ProductosFotos(ProductoId,FotoId,Orden,EsPrincipal)
SELECT p.Id,f.Id,1,1
FROM dbo.EC_Productos p
JOIN dbo.EC_Fotos f ON f.NombreArchivo=LTRIM(RTRIM(p.Imagen))
WHERE LTRIM(RTRIM(ISNULL(p.Imagen,N'')))<>N''
  AND NOT EXISTS(
      SELECT 1 FROM dbo.EC_ProductosFotos pf
      WHERE pf.ProductoId=p.Id
  );

COMMIT;

SELECT
    (SELECT COUNT(*) FROM dbo.EC_Fotos) AS FotosRegistradas,
    (SELECT COUNT(*) FROM dbo.EC_ProductosFotos) AS VinculosProductoFoto,
    (SELECT COUNT(DISTINCT ProductoId) FROM dbo.EC_ProductosFotos) AS ProductosConFotos;
