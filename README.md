# Flux Electricistas en ASP.NET Core 10

Nueva versión de la aplicación de catálogo y presupuestos, migrada desde Web Forms a **ASP.NET Core MVC con vistas Razor y .NET 10**. Los tres proyectos usan `TargetFramework=net10.0`. No usa .NET Framework, System.Web, archivos ASPX ni IIS Express.

## Abrir la aplicación

- URL local: **http://localhost:5088**.
- Usuarios y contraseñas: `ACCESOS-LOCALES.txt`. Son los mismos que tenía la versión anterior en el momento de la copia.
- Ejecutar `Iniciar.ps1` para iniciar la versión compilada, o abrir `FluxElectricistas.slnx` en Visual Studio con soporte para .NET 10 y seleccionar **Web** como proyecto de inicio.
- También se puede ejecutar `dotnet run --project Web` desde esta carpeta. El perfil local utiliza el puerto 5088. La aplicación anterior puede seguir funcionando en 5087.

La entrega incluye `publicar`, con la compilación Release lista para ejecutar usando el runtime ASP.NET Core 10. Para recompilar después de cambiar el código, usar `dotnet publish Web -c Release -o publicar` con el servidor detenido. Para desarrollar, usar Visual Studio o `dotnet watch --project Web`.

## Arquitectura

| Proyecto | Responsabilidad |
| --- | --- |
| Dominio | Productos, marcas, categorías, usuarios y presupuestos. Cantidades e importes con decimal. |
| Negocio | Consultas SQL parametrizadas, validación, autenticación de usuarios, persistencia y generación PDF. |
| Web | ASP.NET Core MVC, controladores, vistas Razor, autenticación por cookies y antiforgery. |

Se conserva la lógica de presupuestos y PDF que ya estaba validada. La presentación se migró a archivos `.cshtml`, que codifican el contenido al mostrarlo. La autenticación ahora usa cookies de ASP.NET Core con nombre propio, sin compartir sesiones con la versión Framework. La verificación de contraseñas usa la API moderna PBKDF2 y mantiene compatibilidad con los hashes existentes. Microsoft.Data.SqlClient 6.1.6 reemplaza a System.Data.SqlClient.

Se conserva la corrección de rendimiento: el catálogo se ordena en memoria de la aplicación para evitar esperas RESOURCE_SEMAPHORE en SQL Server Express, y la pantalla inicial no realiza dos consultas idénticas.

## Base independiente

La base es **ELECTRICISTAS_CORE10_DB**, en `.\SQLEXPRESS`. Se copiaron 1.677 productos, dos usuarios, un presupuesto y sus ocho renglones desde ELECTRICISTAS_DB. Los identificadores, descripciones, precios guardados y contraseñas se conservaron. No se modificó la base de la aplicación anterior.

Las versiones son independientes: los cambios realizados en una no aparecen automáticamente en la otra. Esta copia no establece sincronización.

La conexión está en `Web/appsettings.json`. Se puede reemplazar con `ConnectionStrings__Electricistas` o con `ELECTRICISTAS_CORE10_CONNECTION_STRING`. No se reutiliza la variable de entorno de Framework para evitar conectarse por accidente a su base.

Para reproducir la copia en una base nueva y vacía, usar `scripts/Preparar-Base.ps1 -CopiarDesdeFramework`. El script se niega a sobrescribir un destino con datos. Para cargar solo el catálogo, omitir ese parámetro; ese modo no crea usuarios. Nunca usar estos scripts para sobrescribir una base con cambios reales.

## Funciones conservadas

- Administrador: alta, edición y baja lógica de productos; marca, categoría, tipo, unidad y códigos editables; creación de usuarios.
- Asesor: catálogo con filtros por producto/descripción/código, marca y tipo; presupuestos propios con edición, duplicación y eliminación del historial.
- Cantidades de hasta tres decimales, precios de hasta dos decimales, redondeo monetario y validación de valores.
- Conservación de descripciones y precios por presupuesto, independientemente de los cambios posteriores en el catálogo.
- Protección por rol, aislamiento de presupuestos y PDF por usuario, formularios antiforgery, bloqueo temporal de intentos fallidos y cierre de sesión.
- Descarga de PDF con encabezados, paginación, unidades, precios, subtotales y total aproximado en ARS. Si faltan precios, indica un subtotal parcial.

## Catálogo y ejemplo

El Excel no trae precios ni una columna Tipo: los precios del catálogo siguen **A consultar**, y Tipo está **Sin clasificar** para completar desde administración. Las unidades conservan el criterio anterior: metros para cable unipolar/subterráneo y unidad para los demás, sujetas a revisión según presentación de venta. El informe de origen está en `datos/informe-importacion.json`.

`lista de compra de test.pdf` fue descargado desde la aplicación .NET 10. Incluye ocho productos reales y **precios ficticios de prueba por ARS 499.900,00**. No son precios del proveedor y no se guardaron como precios del catálogo. La advertencia de consultar y confirmar precios con la casa de electricidad aparece en todas las páginas.

## Pruebas y despliegue

La solución compila en Release sin errores ni advertencias. `scripts/probar_web.py` ejecuta 32 pruebas HTTP; el resultado está en `datos/pruebas-web.json`. Verifica login de ambos roles, catálogo, restricciones de acceso, formularios, operaciones sobre presupuestos, cantidades, precios, concurrencia, PDF y cierre de sesión. El script utiliza Python con pypdf y crea registros temporales para probar altas y bajas.

Para nube se requiere ASP.NET Core Runtime 10, una base SQL Server/Azure SQL accesible y configuración del entorno. El desarrollo ya no depende de Windows/IIS Express; en Linux debe configurarse una autenticación de base compatible en lugar de la identidad integrada local. Usar HTTPS, cookies seguras, certificados SQL válidos y almacenamiento persistente y protegido de las claves de Data Protection (App_Data/keys). La conexión local permite el certificado de desarrollo de SQL Server; cambiar TrustServerCertificate a False para un servidor con certificado válido.

Esta entrega está ejecutándose localmente. No se contrató ni publicó un servicio cloud.
