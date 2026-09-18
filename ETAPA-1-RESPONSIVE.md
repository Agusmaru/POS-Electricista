# Etapa 1 · Diseño responsive

Aplicación ASP.NET Core MVC .NET 10. Abrir http://localhost:5088 en esta computadora. Para iniciarla nuevamente, ejecutar `Iniciar.ps1` desde la carpeta del proyecto.

## Cambios

- Identidad azul oscuro y amarillo, con navegación lateral en escritorio.
- Menú desplegable en tablet y celular, estado accesible, cierre con Escape y ajuste al redimensionar.
- Catálogo en tabla de escritorio y tarjetas en pantallas pequeñas.
- Formularios en una columna en celulares; controles táctiles de al menos 44 px y campos de 16 px.
- Apariencia común en catálogo, productos, usuarios, presupuestos, cuenta e ingreso.
- Enlace para saltar al contenido, indicadores de foco y navegación activa.

## Prueba manual sugerida

1. Ingresar con las credenciales existentes de `ACCESOS-LOCALES.txt`.
2. Recorrer Catálogo, Nuevo producto, Usuarios, Presupuestos y Cuenta.
3. Usar F12 y activar la vista de dispositivos del navegador, probando 390 px y 768 px; ampliar luego a escritorio.
4. Abrir y cerrar el menú móvil, navegar desde él y comprobar las tarjetas del catálogo.
5. Abrir el presupuesto de prueba y descargar su PDF.

La dirección localhost sirve en esta computadora. El acceso desde un celular físico requerirá configurar acceso por red o un entorno publicado.

## Alcance

Esta etapa adapta la presentación. Las fotografías, filtros y correcciones funcionales de catálogo, gestión completa de usuarios y cambios del presupuesto/PDF corresponden a las siguientes etapas. Se mantienen los precios y datos actuales. No se ejecutaron cambios en el esquema ni en el contenido del catálogo.

## Validación

Prueba de navegador en 320, 390, 768, 1024, 1280 y 1440 px para seis vistas autenticadas. Verifica desbordamientos, menú, roles, inicio y cierre de sesión y descarga de PDF. Evidencia: `datos/responsive/resultado.json` y capturas en la misma carpeta.

Para repetir: ejecutar `scripts/probar_responsive.cjs` con Node, Playwright y Microsoft Edge instalados. La variable `PLAYWRIGHT_PACKAGE` permite indicar una ruta absoluta al paquete Playwright. La aplicación debe estar iniciada en el puerto 5088.
