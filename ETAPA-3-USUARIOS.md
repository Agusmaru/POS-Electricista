# Etapa 3 · Usuarios

## Implementado

- Listado de usuarios con búsqueda por nombre o correo.
- Filtros por rol y estado.
- Alta y edición de usuarios.
- Activación y desactivación sin borrar datos.
- Roles Administrador y Asesor.
- Restablecimiento opcional de contraseña desde la edición.
- Bloqueo del acceso al panel para usuarios asesores.
- Protección para impedir que un administrador desactive o quite el rol de su propia cuenta.
- Protección para conservar al menos un administrador activo.
- Vista adaptada a computadora, tablet y celular.

## Prueba manual

1. Ingresar como administrador y abrir Usuarios.
2. Crear un usuario de prueba con rol Asesor.
3. Buscarlo y editar su nombre o contraseña.
4. Desactivarlo y comprobar que no puede iniciar sesión.
5. Reactivarlo y verificar nuevamente el acceso.
6. Ingresar como asesor y comprobar que no puede abrir el ABM de usuarios.

## Validación automática

El script `scripts/probar_etapa3.cjs` verifica acceso administrativo, listado, filtros, edición, cambio de rol, restablecimiento de contraseña, desactivación, reactivación, permisos, protecciones de seguridad y adaptación móvil. Resultado guardado en `datos/pruebas-etapa3.json` con 12 comprobaciones correctas.
