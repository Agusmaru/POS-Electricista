param([string]$Servidor = '.\SQLEXPRESS',[switch]$CopiarDesdeFramework)
$ErrorActionPreference = 'Stop'
& sqlcmd -S $Servidor -E -C -I -b -i (Join-Path $PSScriptRoot 'Crear-Base.sql')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la base Core10.' }
if ($CopiarDesdeFramework) {
    & sqlcmd -S $Servidor -E -C -I -b -i (Join-Path $PSScriptRoot 'Copiar-Datos-Framework.sql')
} else {
    & sqlcmd -S $Servidor -E -C -I -b -i (Join-Path (Split-Path $PSScriptRoot -Parent) 'datos\catalogo.sql')
}
if ($LASTEXITCODE -ne 0) { throw 'No se completó la copia/importación. El destino no debe tener datos al copiar desde Framework.' }
