-- Ejecutar sobre la base de datos de la aplicación.
-- Es idempotente: puede ejecutarse más de una vez.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.EC_Colores') IS NULL
BEGIN
    CREATE TABLE dbo.EC_Colores(
        Id int IDENTITY PRIMARY KEY,
        Nombre nvarchar(60) NOT NULL,
        CodigoHex varchar(7) NOT NULL,
        Activo bit NOT NULL CONSTRAINT DF_EC_Colores_Activo DEFAULT 1,
        Orden int NOT NULL,
        CONSTRAINT UQ_EC_Colores_Nombre UNIQUE(Nombre)
    );
END;

IF OBJECT_ID('dbo.EC_ProductoColores') IS NULL
BEGIN
    CREATE TABLE dbo.EC_ProductoColores(
        ProductoId int NOT NULL,
        ColorId int NOT NULL,
        CONSTRAINT PK_EC_ProductoColores PRIMARY KEY(ProductoId,ColorId),
        CONSTRAINT FK_EC_ProductoColores_Producto FOREIGN KEY(ProductoId) REFERENCES dbo.EC_Productos(Id),
        CONSTRAINT FK_EC_ProductoColores_Color FOREIGN KEY(ColorId) REFERENCES dbo.EC_Colores(Id)
    );
END;

IF COL_LENGTH('dbo.EC_Items','ColorId') IS NULL
    ALTER TABLE dbo.EC_Items ADD ColorId int NULL;

IF COL_LENGTH('dbo.EC_Items','ColorNombre') IS NULL
    ALTER TABLE dbo.EC_Items ADD ColorNombre nvarchar(60) NOT NULL
        CONSTRAINT DF_EC_Items_ColorNombre DEFAULT '';

IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_EC_Items_Colores')
    ALTER TABLE dbo.EC_Items ADD CONSTRAINT FK_EC_Items_Colores
        FOREIGN KEY(ColorId) REFERENCES dbo.EC_Colores(Id);

MERGE dbo.EC_Colores AS destino
USING (VALUES
    (N'Marrón','#795548',1),
    (N'Negro','#161616',2),
    (N'Rojo','#D32F2F',3),
    (N'Azul','#1976D2',4),
    (N'Verde-Amarillo','#8BAF24',5),
    (N'Blanco','#F4F4F4',6)
) AS origen(Nombre,CodigoHex,Orden)
ON destino.Nombre=origen.Nombre
WHEN MATCHED THEN UPDATE SET CodigoHex=origen.CodigoHex,Orden=origen.Orden
WHEN NOT MATCHED THEN
    INSERT(Nombre,CodigoHex,Activo,Orden)
    VALUES(origen.Nombre,origen.CodigoHex,1,origen.Orden);

INSERT dbo.EC_ProductoColores(ProductoId,ColorId)
SELECT p.Id,c.Id
FROM dbo.EC_Productos p
CROSS JOIN dbo.EC_Colores c
WHERE p.Activo=1
  AND c.Activo=1
  AND (LOWER(LTRIM(RTRIM(p.Nombre)))=N'cable unipolar' OR p.CodigoCatalogo LIKE N'UNIP-%')
  AND NOT EXISTS(
      SELECT 1 FROM dbo.EC_ProductoColores pc
      WHERE pc.ProductoId=p.Id AND pc.ColorId=c.Id
  );

COMMIT;

