-- Ejecutar manualmente sobre la instancia local de SQL Server.
-- Corrige la configuración que provoca cierres/reaperturas y restaura la foto de BC 034 L.
USE [ELECTRICISTAS_CORE10_DB];
GO

ALTER DATABASE [ELECTRICISTAS_CORE10_DB] SET AUTO_CLOSE OFF;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.EC_Productos',N'U') IS NULL
   OR OBJECT_ID(N'dbo.EC_Fotos',N'U') IS NULL
   OR OBJECT_ID(N'dbo.EC_ProductosFotos',N'U') IS NULL
    THROW 50001, N'Faltan las tablas de productos y fotos esperadas.', 1;

DECLARE @ProductoId int=(
    SELECT TOP (1) Id
    FROM dbo.EC_Productos
    WHERE CodigoCatalogo=N'BC 034 L' AND Marca=N'DAISA'
    ORDER BY Id
);
DECLARE @FotoId int=(
    SELECT Id
    FROM dbo.EC_Fotos
    WHERE NombreArchivo=N'catalogador-v2-df32e23b1aa7e7d8.jpg'
);

IF @ProductoId IS NULL
    THROW 50002, N'No se encontró el producto DAISA BC 034 L.', 1;
IF @FotoId IS NULL
    THROW 50003, N'No se encontró la foto compartida en EC_Fotos.', 1;

IF NOT EXISTS(SELECT 1 FROM dbo.EC_ProductosFotos WHERE ProductoId=@ProductoId)
BEGIN
    INSERT dbo.EC_ProductosFotos(ProductoId,FotoId,Orden,EsPrincipal)
    VALUES(@ProductoId,@FotoId,1,1);
END;

UPDATE p SET Imagen=f.NombreArchivo
FROM dbo.EC_Productos p
JOIN dbo.EC_ProductosFotos pf ON pf.ProductoId=p.Id AND pf.EsPrincipal=1
JOIN dbo.EC_Fotos f ON f.Id=pf.FotoId
WHERE p.Id=@ProductoId;

COMMIT;

SELECT name,is_auto_close_on
FROM sys.databases
WHERE name=DB_NAME();

SELECT p.Id,p.CodigoCatalogo,p.Imagen,f.NombreArchivo,pf.EsPrincipal
FROM dbo.EC_Productos p
LEFT JOIN dbo.EC_ProductosFotos pf ON pf.ProductoId=p.Id
LEFT JOIN dbo.EC_Fotos f ON f.Id=pf.FotoId
WHERE p.Id=@ProductoId;
GO
