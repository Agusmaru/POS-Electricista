param([int]$Puerto = 5088)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$webRoot = Join-Path $projectRoot 'Web'
$dotnet = Get-Command dotnet -ErrorAction Stop
$publishedDll = Join-Path $projectRoot 'publicar\Web.dll'
$existing = Get-NetTCPConnection -LocalPort $Puerto -State Listen -ErrorAction SilentlyContinue
if ($existing) { Write-Output "El puerto $Puerto está ocupado. Si es Flux Core 10, abrí http://localhost:$Puerto ."; exit }
& $dotnet.Source publish (Join-Path $webRoot 'Web.csproj') -c Release -o (Join-Path $projectRoot 'publicar')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar. Verificá que esté instalado el SDK .NET 10.' }
$publishedRoot = Split-Path $publishedDll -Parent
$process = Start-Process -FilePath $dotnet.Source -ArgumentList @('"'+$publishedDll+'"','--urls',"http://localhost:$Puerto",'--contentRoot','"'+$publishedRoot+'"') -WorkingDirectory $publishedRoot -WindowStyle Hidden -PassThru
Write-Output "Flux ASP.NET Core 10 iniciado en http://localhost:$Puerto (PID $($process.Id))."
Write-Output 'Usuarios y contraseñas: ACCESOS-LOCALES.txt.'
