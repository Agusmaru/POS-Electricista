# Revisión técnica del proyecto POS Electricista

Fecha de revisión: 25 de septiembre de 2026  
Proyecto: `Solucion.Net10.Core`  
Tecnología: ASP.NET Core 10 y SQL Server

## Resultado general

El proyecto compila correctamente y no presenta vulnerabilidades conocidas en sus dependencias NuGet. La aplicación cuenta con varias medidas de seguridad adecuadas, como consultas parametrizadas, protección antifalsificación, almacenamiento seguro de contraseñas y encabezados HTTP de protección.

Antes de considerar la versión como final, conviene corregir tres problemas prioritarios relacionados con las imágenes compartidas, la validación del carrito y la modificación automática de la base de datos durante el arranque.

## Problemas prioritarios

### 1. Una imagen compartida puede eliminarse para varios productos

**Prioridad:** alta.

Cuando se cambia o quita la imagen de un producto, el archivo físico anterior se elimina directamente desde `Web/Controllers/PortalController.cs`.

El catálogo importado contiene archivos de imagen compartidos por varios productos. Por ejemplo, diferentes códigos de Argeflex utilizan el mismo archivo. Si se edita uno de esos productos, el archivo puede borrarse y dejar sin imagen a todos los demás productos que todavía lo referencian.

**Corrección recomendada:**

- Antes de eliminar el archivo, consultar cuántos productos todavía lo utilizan.
- Borrarlo solamente cuando no existan más referencias.
- Como alternativa, conservar permanentemente las imágenes provenientes de la importación del catálogo y eliminar solamente imágenes subidas manualmente.
- Centralizar esta lógica en un servicio encargado de administrar imágenes.

**Archivos relacionados:**

- `Web/Controllers/PortalController.cs`, método `GuardarProducto`, aproximadamente línea 271.
- `Web/Controllers/PortalController.cs`, método `BorrarImagen`, aproximadamente línea 451.
- `scripts/importacion-imagenes/Actualizar-Imagenes-Catalogador.sql`, desde aproximadamente la línea 274.

### 2. El carrito puede confirmar productos que fueron desactivados

**Prioridad:** alta.

La disponibilidad del producto y del color se comprueba cuando el producto se agrega al carrito. Sin embargo, cuando se confirma el carrito solamente se validan las cantidades.

Esto permite la siguiente situación:

1. Un asesor agrega un producto al carrito.
2. Un administrador desactiva ese producto o elimina uno de sus colores.
3. El asesor confirma el carrito que ya tenía abierto.
4. La orden se guarda con información que ya no está disponible en el catálogo.

**Corrección recomendada:**

- Volver a consultar cada producto al confirmar el carrito.
- Comprobar que continúe activo.
- Comprobar que el color seleccionado siga asociado al producto.
- Si hay problemas, no generar la orden y mostrar cuáles artículos deben revisarse.
- Actualizar los datos descriptivos del carrito con la información actual del catálogo antes de guardar.

**Archivos relacionados:**

- `Web/Controllers/PortalController.cs`, confirmación del carrito, aproximadamente líneas 231 a 238.
- `Negocio/PresupuestoNegocio.cs`, método `Guardar`, aproximadamente líneas 36 a 43.

### 3. La aplicación modifica la estructura de SQL Server durante el arranque

**Prioridad:** alta para producción.

Al iniciar, la aplicación ejecuta los métodos `PrepararEtapa2` y `PrepararColores`. Estos métodos pueden ejecutar instrucciones `ALTER TABLE`, `CREATE TABLE`, `MERGE`, `UPDATE` e `INSERT`.

Esto genera dos riesgos:

- El usuario de SQL Server utilizado por la aplicación necesita permisos para modificar la estructura de la base.
- Si SQL Server está temporalmente inaccesible, la aplicación se cierra completamente antes de poder responder una página de diagnóstico.

**Corrección recomendada:**

- Mover las modificaciones de estructura a scripts SQL versionados.
- Ejecutar esos scripts manualmente antes de publicar una nueva versión.
- Utilizar para la aplicación un usuario SQL con permisos limitados de lectura y escritura.
- Registrar claramente la versión de base requerida por cada publicación.
- Agregar un tratamiento controlado para errores de conexión durante el arranque.

**Archivos relacionados:**

- `Web/Program.cs`, aproximadamente líneas 33 a 36.
- `Negocio/CatalogoNegocio.cs`, métodos `PrepararEtapa2` y `PrepararColores`, aproximadamente líneas 12 a 60.

## Problemas de prioridad media

### 4. La regla de cantidades enteras no se aplica al editar presupuestos

El carrito exige cantidades enteras, pero la pantalla de edición del presupuesto todavía permite cantidades con hasta tres decimales, por ejemplo `1.001`.

Es necesario definir una única regla de negocio:

- Si todos los materiales se administran por unidad, el presupuesto también debe exigir enteros.
- Si determinados materiales, como cables vendidos por metro, admiten fracciones, la regla debería depender de la unidad de cada producto.

**Corrección recomendada:** aplicar la misma validación en la interfaz, el controlador y la capa de negocio.

**Archivos relacionados:**

- `Web/Views/Portal/Presupuesto.cshtml`, campos de cantidad, aproximadamente líneas 21 y 30.
- `Web/Controllers/PortalController.cs`, acciones de presupuesto, aproximadamente líneas 330 y 338.
- `Negocio/PresupuestoNegocio.cs`, validación de cantidades, aproximadamente línea 41.

### 5. Cambiar solamente el precio puede dejar información anterior en el navegador

La versión del catálogo se calcula usando sus datos principales, pero no incluye `PrecioEstimado`. El resultado del catálogo se guarda en `sessionStorage` del navegador usando esa versión.

Si un administrador cambia solamente el precio, la versión puede continuar siendo la misma y el navegador puede reutilizar información anterior.

**Corrección recomendada:**

- Incluir `PrecioEstimado` en el cálculo de la versión del catálogo; o
- Eliminar completamente el precio del catálogo, de la respuesta JSON y del formulario de producto si ya no forma parte del sistema.

**Archivos relacionados:**

- `Web/Services/CatalogoCache.cs`, método `CalcularVersion`, aproximadamente líneas 49 a 64.
- `Web/Controllers/PortalController.cs`, endpoint `/Catalogo/Datos`, aproximadamente línea 165.
- `Web/wwwroot/portal.js`, lectura y almacenamiento del catálogo, aproximadamente líneas 531 a 543.

### 6. Falta reforzar la configuración HTTPS para MonsterASP.NET

Las cookies cuentan con `HttpOnly` y `SameSite`, pero no se establece explícitamente que deban enviarse solamente mediante HTTPS. Tampoco se configura el procesamiento de encabezados reenviados por el servidor proxy de Monster.

**Corrección recomendada para producción:**

- Configurar `CookieSecurePolicy.Always` para autenticación, sesión y antifalsificación.
- Configurar encabezados reenviados de forma restringida al proxy del proveedor.
- Habilitar HSTS en producción.
- Mantener activa la redirección de HTTP a HTTPS.
- Verificar que Monster conserve correctamente el esquema HTTPS original.

**Archivo relacionado:**

- `Web/Program.cs`, configuración de cookies y middleware, aproximadamente líneas 9 a 50.

### 7. El inicio de sesión necesita una limitación general de solicitudes

El sistema ya bloquea temporalmente una cuenta después de varios intentos incorrectos. Sin embargo, no existe un límite global por dirección IP o frecuencia.

Además, un email inexistente termina la validación inmediatamente, mientras que un usuario existente ejecuta el cálculo PBKDF2. Esa diferencia de tiempo podría utilizarse para intentar descubrir qué emails están registrados.

**Corrección recomendada:**

- Agregar el limitador de solicitudes de ASP.NET Core para `/Login`.
- Mantener el bloqueo actual por cuenta.
- Ejecutar una verificación ficticia de contraseña cuando el email no exista.
- Registrar intentos anormales sin guardar contraseñas ni información sensible.

**Archivo relacionado:**

- `Negocio/SeguridadCatalogo.cs`, método `Login`, aproximadamente líneas 46 a 56.

### 8. El carrito y el tema dependen de la memoria del servidor

La sesión utiliza `AddDistributedMemoryCache`. Si Monster reinicia el proceso de la aplicación, los carritos abiertos y la preferencia de tema almacenada en la sesión se pierden.

Esta configuración es aceptable mientras exista una sola instancia y la pérdida ocasional del carrito no sea crítica.

**Corrección recomendada para una versión final:**

- Guardar borradores de carrito en SQL Server; o
- Utilizar un proveedor de sesión distribuida.
- Mantener la preferencia visual también en una cookie persistente si se desea conservarla después de cerrar la sesión.

**Archivo relacionado:**

- `Web/Program.cs`, aproximadamente líneas 22 a 30.

### 9. El caché del catálogo contiene objetos modificables

El caché devuelve colecciones de productos cuyos objetos todavía pueden modificarse. El controlador, por ejemplo, cambia el campo `Imagen` cuando no encuentra el archivo físico.

Esto puede alterar el catálogo compartido para todos los usuarios sin cambiar su versión.

**Corrección recomendada:**

- Utilizar modelos inmutables dentro del caché.
- Crear copias o modelos específicos para cada respuesta.
- Evitar modificar directamente productos recibidos desde `CatalogoCache`.

**Archivos relacionados:**

- `Web/Services/CatalogoCache.cs`, aproximadamente líneas 18 a 32.
- `Web/Controllers/PortalController.cs`, aproximadamente línea 141.

## Mejoras de rendimiento y mantenimiento

### Paginación de presupuestos

La pantalla de presupuestos trae todo el historial desde SQL Server y después aplica filtros y ordenamiento en memoria. Esto funcionará con pocos registros, pero empeorará a medida que aumente el historial.

**Mejora recomendada:** aplicar búsqueda, ordenamiento y paginación directamente en SQL Server.

**Archivos relacionados:**

- `Negocio/PresupuestoNegocio.cs`, método `Listar`, aproximadamente líneas 10 a 16.
- `Web/Controllers/PortalController.cs`, aproximadamente líneas 276 a 294.

### Acceso asincrónico a SQL Server

Las conexiones y consultas utilizan operaciones sincrónicas. Una conexión lenta con la base remota puede mantener ocupados los hilos que atienden solicitudes web.

**Mejora recomendada:** migrar progresivamente a `OpenAsync`, `ExecuteReaderAsync`, `ExecuteScalarAsync` y `ExecuteNonQueryAsync`.

**Archivos relacionados:**

- `Negocio/CatalogoDatos.cs`.
- `Negocio/CatalogoNegocio.cs`.
- `Negocio/PresupuestoNegocio.cs`.
- `Negocio/SeguridadCatalogo.cs`.

### Bloqueo durante la recarga del catálogo

Cuando vence el caché, la consulta completa a SQL Server se ejecuta dentro de un bloqueo global. Mientras se realiza esa consulta, las demás solicitudes que necesitan el catálogo deben esperar.

**Mejora recomendada:** recargar fuera del bloqueo, utilizar `SemaphoreSlim` o aplicar una estrategia que mantenga la versión anterior mientras se carga la nueva.

**Archivo relacionado:**

- `Web/Services/CatalogoCache.cs`, aproximadamente líneas 23 a 46.

### Parámetros SQL con tipos explícitos

El método común de consultas utiliza `AddWithValue` para todos los parámetros. SQL Server puede inferir tipos o longitudes poco convenientes y dejar de aprovechar algunos índices.

**Mejora recomendada:** definir `SqlDbType`, longitud, precisión y escala para las consultas utilizadas con más frecuencia.

**Archivo relacionado:**

- `Negocio/CatalogoDatos.cs`, aproximadamente líneas 16 a 21.

### Endpoint de salud

El endpoint `/salud` responde siempre con `estado: ok`, incluso cuando la aplicación no puede acceder a SQL Server.

**Mejora recomendada:**

- Conservar un endpoint simple para saber si el proceso está activo.
- Agregar otro endpoint de disponibilidad que ejecute una consulta mínima como `SELECT 1`.
- Opcionalmente comprobar que la carpeta de imágenes se encuentre disponible.

**Archivo relacionado:**

- `Web/Program.cs`, aproximadamente línea 54.

### Identidad visual del PDF

El PDF todavía muestra el encabezado `ELECTRICIDAD | CATÁLOGO PROFESIONAL`, mientras que la aplicación utiliza la identidad de Furnarius Energy.

**Mejora recomendada:** actualizar el texto e incorporar el logo del cliente en el PDF.

**Archivo relacionado:**

- `Negocio/PresupuestoPdf.cs`, aproximadamente línea 47.

### Organización del código

`PortalController` concentra autenticación de usuario, catálogo, carrito, productos, presupuestos, imágenes, usuarios y preferencias visuales.

**Mejora recomendada:** dividirlo progresivamente en controladores y servicios dedicados:

- `CatalogoController`.
- `CarritoController`.
- `PresupuestosController`.
- `ProductosController`.
- `UsuariosController`.
- `ImagenProductoService`.

### Análisis de valores nulos

Los proyectos tienen desactivado `Nullable`. Por ese motivo, el compilador no puede advertir sobre varias situaciones en las que una consulta puede devolver `null`.

**Mejora recomendada:** activar el análisis de valores nulos gradualmente, empezando por `Dominio` y los modelos nuevos.

### Pruebas automáticas

No existe actualmente un proyecto de pruebas automatizadas.

Las primeras pruebas recomendadas son:

1. Un asesor no puede administrar productos ni usuarios.
2. Una imagen compartida no se elimina mientras otro producto la utilice.
3. Un carrito no puede confirmar un producto desactivado.
4. Un color eliminado no puede confirmarse.
5. Las cantidades respetan la regla definida.
6. Dos usuarios no pueden sobrescribir accidentalmente la misma revisión de un presupuesto.
7. Los filtros avanzados producen los resultados esperados.
8. El PDF se genera correctamente con varias páginas.

## Aspectos que ya están correctamente implementados

- La solución utiliza .NET 10.
- Las consultas revisadas están parametrizadas.
- La protección antifalsificación está aplicada globalmente.
- Las acciones privadas requieren autenticación.
- Las contraseñas se almacenan con PBKDF2, sal aleatoria y 210.000 iteraciones.
- Existe bloqueo temporal por intentos fallidos de inicio de sesión.
- Se evita desactivar al último administrador activo.
- Se evita que un administrador se quite a sí mismo sus permisos.
- Existe control de concurrencia mediante la revisión del presupuesto.
- Las imágenes subidas se validan por firma de archivo y tienen límite de tamaño.
- Los nombres de archivo generados para nuevas imágenes no dependen del nombre suministrado por el usuario.
- Se utilizan encabezados `Content-Security-Policy`, `X-Frame-Options`, `X-Content-Type-Options` y `Referrer-Policy`.
- Los datos sensibles locales, claves de protección y archivos de publicación están excluidos del repositorio Git.

## Verificaciones realizadas

- Compilación Release: correcta.
- Resultado de compilación: 0 errores y 0 advertencias.
- Dependencias NuGet: sin vulnerabilidades conocidas al momento de la revisión.
- Revisión de autenticación, permisos, carrito, catálogo, presupuestos, generación de PDF, sesión, caché, carga de imágenes y scripts SQL.
- Revisión del estado de Git y de los archivos excluidos.
- Existen dos modificaciones locales pendientes correspondientes a la validación de cantidades enteras del carrito:
  - `Web/Controllers/PortalController.cs`.
  - `Web/Views/Portal/Carrito.cshtml`.

## Limitación encontrada durante la prueba de ejecución

La prueba de navegación local no pudo completarse desde el proceso de revisión porque la conexión integrada de SQL Server devolvió el siguiente error:

`El nombre principal no es correcto. No se puede generar contexto SSPI.`

Este error puede depender del usuario de Windows con el que se ejecutó la prueba y no demuestra que la aplicación falle cuando se inicia normalmente desde el equipo. Sin embargo, permitió comprobar que actualmente la aplicación termina por completo cuando la conexión a SQL Server falla durante el arranque.

## Orden sugerido de implementación

### Etapa 1: integridad de información

1. Proteger las imágenes compartidas.
2. Revalidar productos y colores al confirmar el carrito.
3. Unificar la regla de cantidades.

### Etapa 2: preparación para producción

1. Retirar las modificaciones SQL del arranque.
2. Crear scripts SQL versionados.
3. Reforzar cookies, HTTPS y encabezados del proxy.
4. Mejorar el endpoint de salud.

### Etapa 3: seguridad y persistencia

1. Agregar límite de intentos al inicio de sesión.
2. Persistir los carritos o configurar sesión distribuida.
3. Evitar diferencias de tiempo para emails inexistentes.

### Etapa 4: rendimiento y mantenimiento

1. Paginar presupuestos desde SQL Server.
2. Migrar las consultas principales a operaciones asincrónicas.
3. Mejorar la recarga del caché.
4. Dividir `PortalController`.
5. Incorporar pruebas automáticas.

### Etapa 5: terminación visual

1. Actualizar la identidad visual del PDF.
2. Decidir si el precio permanecerá en el catálogo.
3. Revisar textos anteriores que todavía no utilicen la marca Furnarius Energy.

