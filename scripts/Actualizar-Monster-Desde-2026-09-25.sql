/*
    Actualización consolidada de la base publicada en Monster el 25/09/2026.
    Compatible con la aplicación del commit 0441061.

    IMPORTANTE:
    1. Realizar un respaldo antes de ejecutar.
    2. Seleccionar manualmente la base de datos del sitio. No ejecutar sobre master.
    3. Ejecutar este archivo antes de publicar el código nuevo.
    4. Es idempotente: puede ejecutarse nuevamente si fuera necesario.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() = N'master'
    THROW 50001, N'Seleccioná la base de datos de la aplicación antes de ejecutar el script.', 1;

IF OBJECT_ID(N'dbo.EC_Productos', N'U') IS NULL
   OR OBJECT_ID(N'dbo.EC_Usuarios', N'U') IS NULL
   OR OBJECT_ID(N'dbo.EC_Presupuestos', N'U') IS NULL
    THROW 50002, N'La base seleccionada no contiene las tablas principales de POS Electricista.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    ---------------------------------------------------------------------------
    -- 1. Galería reutilizable de fotografías
    ---------------------------------------------------------------------------
    IF OBJECT_ID(N'dbo.EC_Fotos', N'U') IS NULL
        EXEC(N'
            CREATE TABLE dbo.EC_Fotos
            (
                Id int IDENTITY NOT NULL CONSTRAINT PK_EC_Fotos PRIMARY KEY,
                NombreArchivo nvarchar(260) NOT NULL,
                FechaCarga datetime2 NOT NULL
                    CONSTRAINT DF_EC_Fotos_FechaCarga DEFAULT SYSUTCDATETIME(),
                CONSTRAINT UQ_EC_Fotos_NombreArchivo UNIQUE (NombreArchivo)
            );');

    IF OBJECT_ID(N'dbo.EC_ProductosFotos', N'U') IS NULL
        EXEC(N'
            CREATE TABLE dbo.EC_ProductosFotos
            (
                ProductoId int NOT NULL,
                FotoId int NOT NULL,
                Orden int NOT NULL CONSTRAINT DF_EC_ProductosFotos_Orden DEFAULT 1,
                EsPrincipal bit NOT NULL CONSTRAINT DF_EC_ProductosFotos_Principal DEFAULT 0,
                CONSTRAINT PK_EC_ProductosFotos PRIMARY KEY (ProductoId, FotoId),
                CONSTRAINT CK_EC_ProductosFotos_Orden CHECK (Orden > 0),
                CONSTRAINT FK_EC_ProductosFotos_Producto
                    FOREIGN KEY (ProductoId) REFERENCES dbo.EC_Productos(Id) ON DELETE CASCADE,
                CONSTRAINT FK_EC_ProductosFotos_Foto
                    FOREIGN KEY (FotoId) REFERENCES dbo.EC_Fotos(Id)
            );');

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_EC_ProductosFotos_Principal'
          AND object_id = OBJECT_ID(N'dbo.EC_ProductosFotos')
    )
        EXEC(N'
            CREATE UNIQUE INDEX UX_EC_ProductosFotos_Principal
            ON dbo.EC_ProductosFotos(ProductoId)
            WHERE EsPrincipal = 1;');

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_EC_ProductosFotos_Foto'
          AND object_id = OBJECT_ID(N'dbo.EC_ProductosFotos')
    )
        EXEC(N'
            CREATE INDEX IX_EC_ProductosFotos_Foto
            ON dbo.EC_ProductosFotos(FotoId);');

    -- Registra una sola vez cada archivo que ya estaba en EC_Productos.Imagen.
    EXEC(N'
        INSERT dbo.EC_Fotos(NombreArchivo)
        SELECT DISTINCT LTRIM(RTRIM(p.Imagen))
        FROM dbo.EC_Productos p
        WHERE LTRIM(RTRIM(ISNULL(p.Imagen, N''''))) <> N''''
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.EC_Fotos f
              WHERE f.NombreArchivo = LTRIM(RTRIM(p.Imagen))
          );');

    -- Cada imagen histórica se convierte en la foto principal del producto.
    EXEC(N'
        INSERT dbo.EC_ProductosFotos(ProductoId, FotoId, Orden, EsPrincipal)
        SELECT p.Id, f.Id, 1, 1
        FROM dbo.EC_Productos p
        JOIN dbo.EC_Fotos f
          ON f.NombreArchivo = LTRIM(RTRIM(p.Imagen))
        WHERE LTRIM(RTRIM(ISNULL(p.Imagen, N''''))) <> N''''
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.EC_ProductosFotos pf
              WHERE pf.ProductoId = p.Id
          );');

    ---------------------------------------------------------------------------
    -- 2. Listas de materiales persistentes por usuario
    ---------------------------------------------------------------------------
    IF OBJECT_ID(N'dbo.EC_Carritos', N'U') IS NULL
        EXEC(N'
            CREATE TABLE dbo.EC_Carritos
            (
                UsuarioId int NOT NULL CONSTRAINT PK_EC_Carritos PRIMARY KEY,
                Contenido nvarchar(max) NOT NULL,
                Actualizado datetime2 NOT NULL
                    CONSTRAINT DF_EC_Carritos_Actualizado DEFAULT SYSUTCDATETIME(),
                CONSTRAINT FK_EC_Carritos_Usuarios
                    FOREIGN KEY (UsuarioId) REFERENCES dbo.EC_Usuarios(Id) ON DELETE CASCADE
            );');

    ---------------------------------------------------------------------------
    -- 3. Estado de las solicitudes de cotización
    -- Se usa SQL dinámico para que cada columna sea compilada después de existir.
    ---------------------------------------------------------------------------
    IF COL_LENGTH(N'dbo.EC_Presupuestos', N'EstadoSolicitud') IS NULL
        EXEC(N'
            ALTER TABLE dbo.EC_Presupuestos
            ADD EstadoSolicitud varchar(20) NOT NULL
                CONSTRAINT DF_EC_Presupuestos_EstadoSolicitud DEFAULT ''Preparada'';');

    IF COL_LENGTH(N'dbo.EC_Presupuestos', N'CompartidaFecha') IS NULL
        EXEC(N'
            ALTER TABLE dbo.EC_Presupuestos
            ADD CompartidaFecha datetime2 NULL;');

    IF COL_LENGTH(N'dbo.EC_Presupuestos', N'CanalCompartido') IS NULL
        EXEC(N'
            ALTER TABLE dbo.EC_Presupuestos
            ADD CanalCompartido varchar(20) NULL;');

    IF OBJECT_ID(N'dbo.CK_EC_Presupuestos_EstadoSolicitud', N'C') IS NULL
    BEGIN
        EXEC(N'
            UPDATE dbo.EC_Presupuestos
            SET EstadoSolicitud = ''Preparada''
            WHERE EstadoSolicitud NOT IN (''Preparada'', ''Compartida'');');

        EXEC(N'
            ALTER TABLE dbo.EC_Presupuestos
            ADD CONSTRAINT CK_EC_Presupuestos_EstadoSolicitud
            CHECK (EstadoSolicitud IN (''Preparada'', ''Compartida''));');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-------------------------------------------------------------------------------
-- Verificación final. Estos SELECT no modifican información.
-------------------------------------------------------------------------------
EXEC(N'
    SELECT
        DB_NAME() AS BaseActual,
        (SELECT COUNT(*) FROM dbo.EC_Fotos) AS FotosRegistradas,
        (SELECT COUNT(*) FROM dbo.EC_ProductosFotos) AS VinculosProductoFoto,
        (SELECT COUNT(DISTINCT ProductoId) FROM dbo.EC_ProductosFotos) AS ProductosConFotos,
        (SELECT COUNT(*) FROM dbo.EC_Carritos) AS ListasPersistidas;

    SELECT TOP (20)
        Id,
        Nombre,
        EstadoSolicitud,
        CompartidaFecha,
        CanalCompartido
    FROM dbo.EC_Presupuestos
    WHERE Activo = 1
    ORDER BY Id DESC;');

PRINT N'Actualización de Monster finalizada correctamente.';
