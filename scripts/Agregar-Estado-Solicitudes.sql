-- Ejecutar manualmente sobre la base local de la aplicación.
-- Agrega el estado de preparación/compartido sin modificar solicitudes existentes.
-- Es idempotente: puede ejecutarse más de una vez.
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.EC_Presupuestos', N'U') IS NULL
    THROW 50001, 'No existe dbo.EC_Presupuestos en la base seleccionada.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- EXEC fuerza a SQL Server a compilar cada ALTER después de comprobar la estructura actual.
    IF COL_LENGTH(N'dbo.EC_Presupuestos', N'EstadoSolicitud') IS NULL
        EXEC(N'ALTER TABLE dbo.EC_Presupuestos
               ADD EstadoSolicitud varchar(20) NOT NULL
               CONSTRAINT DF_EC_Presupuestos_EstadoSolicitud DEFAULT ''Preparada'';');

    IF COL_LENGTH(N'dbo.EC_Presupuestos', N'CompartidaFecha') IS NULL
        EXEC(N'ALTER TABLE dbo.EC_Presupuestos ADD CompartidaFecha datetime2 NULL;');

    IF COL_LENGTH(N'dbo.EC_Presupuestos', N'CanalCompartido') IS NULL
        EXEC(N'ALTER TABLE dbo.EC_Presupuestos ADD CanalCompartido varchar(20) NULL;');

    IF OBJECT_ID(N'dbo.CK_EC_Presupuestos_EstadoSolicitud', N'C') IS NULL
        EXEC(N'ALTER TABLE dbo.EC_Presupuestos
               ADD CONSTRAINT CK_EC_Presupuestos_EstadoSolicitud
               CHECK (EstadoSolicitud IN (''Preparada'', ''Compartida''));');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- También se compila después de crear las columnas.
EXEC(N'SELECT Id, Nombre, EstadoSolicitud, CompartidaFecha, CanalCompartido
       FROM dbo.EC_Presupuestos
       WHERE Activo = 1
       ORDER BY Id DESC;');
