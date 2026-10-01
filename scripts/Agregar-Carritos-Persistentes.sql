-- Ejecutar manualmente sobre la base local de la aplicación.
-- Conserva el carrito por usuario aunque expire la sesión o se reinicie el sitio.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.EC_Carritos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EC_Carritos
    (
        UsuarioId int NOT NULL CONSTRAINT PK_EC_Carritos PRIMARY KEY,
        Contenido nvarchar(max) NOT NULL,
        Actualizado datetime2 NOT NULL CONSTRAINT DF_EC_Carritos_Actualizado DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_EC_Carritos_Usuarios FOREIGN KEY (UsuarioId) REFERENCES dbo.EC_Usuarios(Id) ON DELETE CASCADE
    );
END;

COMMIT TRANSACTION;

SELECT UsuarioId, Actualizado FROM dbo.EC_Carritos ORDER BY Actualizado DESC;
