# Preparación para publicar en la nube

## Aplicación

- Runtime requerido: .NET 10.
- Comando de publicación: `dotnet publish Web/Web.csproj -c Release -o publicar`.
- Inicio: `dotnet Web.dll` dentro de la carpeta publicada.
- Comprobación de disponibilidad: `GET /salud` devuelve `{"estado":"ok"}`.

## Base de datos

Definir la variable de entorno `ELECTRICISTAS_CORE10_CONNECTION_STRING` con la conexión del SQL Server administrado. La aplicación usa esa variable antes que la conexión local de `appsettings.json`.

La cuenta de base debe poder leer y escribir las tablas `EC_Usuarios`, `EC_Productos`, `EC_Presupuestos` y `EC_Items`. El catálogo y las órdenes permanecen separados del código publicado.

## Archivos persistentes

Configurar almacenamiento persistente para:

- `App_Data/keys`: claves de protección de cookies.
- `wwwroot/uploads/productos`: fotografías cargadas desde el ABM.

Sin un volumen persistente, las sesiones podrían invalidarse y las imágenes cargadas podrían perderse al reiniciar o volver a publicar el servicio.

## Configuración recomendada

- Usar HTTPS en el dominio público.
- Mantener la cadena de conexión solamente como secreto del servicio.
- Ejecutar una única instancia mientras las sesiones y las claves estén almacenadas localmente.
- Realizar copias de seguridad periódicas de SQL Server y del volumen de imágenes.
- Cambiar las contraseñas iniciales antes de habilitar el acceso externo.
