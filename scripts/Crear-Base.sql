-- Base independiente. No ejecuta ni modifica COMERCIO_DB.
IF DB_ID(N'ELECTRICISTAS_CORE10_DB') IS NULL CREATE DATABASE ELECTRICISTAS_CORE10_DB;
GO
USE ELECTRICISTAS_CORE10_DB;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF OBJECT_ID('EC_Usuarios') IS NULL
CREATE TABLE EC_Usuarios(
 Id int IDENTITY PRIMARY KEY, Nombre nvarchar(120) NOT NULL, Email nvarchar(200) NOT NULL UNIQUE,
 PasswordHash varchar(200) NOT NULL, EsAdmin bit NOT NULL DEFAULT 0, Activo bit NOT NULL DEFAULT 1,
 Fallos int NOT NULL DEFAULT 0, BloqueadoHasta datetime2 NULL);
IF OBJECT_ID('EC_Productos') IS NULL
CREATE TABLE EC_Productos(
 Id int IDENTITY PRIMARY KEY, Nombre nvarchar(250) NOT NULL, Descripcion nvarchar(1500) NOT NULL,
 Marca nvarchar(120) NOT NULL, Categoria nvarchar(120) NOT NULL DEFAULT '',
 CodigoCatalogo nvarchar(160) NOT NULL DEFAULT '', CodigoLocal nvarchar(160) NOT NULL DEFAULT '',
 Tipo nvarchar(120) NOT NULL DEFAULT 'Sin clasificar', Unidad nvarchar(30) NOT NULL DEFAULT 'unidad',
 PrecioEstimado decimal(18,2) NOT NULL DEFAULT 0 CHECK(PrecioEstimado>=0), Imagen nvarchar(260) NOT NULL DEFAULT '', Activo bit NOT NULL DEFAULT 1,
 Origen nvarchar(250) NOT NULL, ClaveOrigen varchar(64) NULL);
IF OBJECT_ID('EC_Presupuestos') IS NULL
CREATE TABLE EC_Presupuestos(
 Id int IDENTITY PRIMARY KEY, UsuarioId int NOT NULL REFERENCES EC_Usuarios(Id),
 Nombre nvarchar(160) NOT NULL, Local nvarchar(200) NOT NULL DEFAULT '', Observaciones nvarchar(1500) NOT NULL DEFAULT '',
 Fecha datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), EsPrueba bit NOT NULL DEFAULT 0,
 Revision int NOT NULL DEFAULT 1, Activo bit NOT NULL DEFAULT 1);
IF OBJECT_ID('EC_Items') IS NULL
CREATE TABLE EC_Items(
 Id int IDENTITY PRIMARY KEY, PresupuestoId int NOT NULL REFERENCES EC_Presupuestos(Id),
 ProductoId int NOT NULL REFERENCES EC_Productos(Id), Nombre nvarchar(250) NOT NULL, Descripcion nvarchar(1500) NOT NULL,
 Marca nvarchar(120) NOT NULL, CodigoCatalogo nvarchar(160) NOT NULL, CodigoLocal nvarchar(160) NOT NULL,
 Tipo nvarchar(120) NOT NULL, Unidad nvarchar(30) NOT NULL, Cantidad decimal(12,3) NOT NULL CHECK(Cantidad>0),
 PrecioUnitario decimal(18,2) NULL CHECK(PrecioUnitario>=0), Observaciones nvarchar(500) NOT NULL DEFAULT '');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_EC_Items_Presupuesto') CREATE INDEX IX_EC_Items_Presupuesto ON EC_Items(PresupuestoId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_EC_Productos_Origen') CREATE UNIQUE INDEX IX_EC_Productos_Origen ON EC_Productos(ClaveOrigen) WHERE ClaveOrigen IS NOT NULL;
IF COL_LENGTH('EC_Productos','Imagen') IS NULL ALTER TABLE EC_Productos ADD Imagen nvarchar(260) NOT NULL CONSTRAINT DF_EC_Productos_Imagen DEFAULT '';
UPDATE EC_Productos SET PrecioEstimado=0 WHERE PrecioEstimado IS NULL;
GO
