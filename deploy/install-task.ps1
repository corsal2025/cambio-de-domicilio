<#
.SYNOPSIS
    Instala OutlookComunaRouter como Tarea Programada de Windows, corriendo de forma
    continua (no es una tarea de un solo disparo) con reinicio automático ante fallas.

.DESCRIPTION
    Requiere ejecutarse como Administrador. Antes de correr este script:
      1. Publicar la app:  dotnet publish src/OutlookComunaRouter -c Release -o publish
      2. Completar publish/appsettings.json (o crear appsettings.Development.json) con
         TenantId, ClientId, ClientSecret reales del registro en Azure AD (ver README.md).
      3. Completar data/comunas.csv con el directorio real de comunas.

    Sin las credenciales reales de Azure AD, la tarea se instalará pero el servicio
    fallará su ciclo de autenticación en cada intento (queda reintentando, no crashea
    el proceso, pero tampoco procesará correos reales).
#>

param(
    [string]$TaskName = "OutlookComunaRouter",
    [string]$PublishPath = (Join-Path $PSScriptRoot "..\publish"),
    [string]$RunAsUser = $env:USERNAME
)

$ErrorActionPreference = "Stop"

$exePath = Join-Path $PublishPath "OutlookComunaRouter.exe"
if (-not (Test-Path $exePath)) {
    throw "No se encontró $exePath. Ejecuta primero: dotnet publish src/OutlookComunaRouter -c Release -o publish"
}

$action = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $PublishPath

# Se dispara al iniciar sesión y también al arrancar el sistema, para que sobreviva reinicios.
$logonTrigger = New-ScheduledTaskTrigger -AtLogOn
$bootTrigger = New-ScheduledTaskTrigger -AtStartup

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartCount 999 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero) # sin límite de tiempo de ejecución: es un proceso continuo

$principal = New-ScheduledTaskPrincipal -UserId $RunAsUser -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName `
    -Action $action `
    -Trigger @($logonTrigger, $bootTrigger) `
    -Settings $settings `
    -Principal $principal `
    -Description "Sondeo continuo de cambiodedomicilio@munivalpo.cl (OutlookComunaRouter)" `
    -Force

Write-Host "Tarea '$TaskName' registrada. Para iniciarla ahora: Start-ScheduledTask -TaskName '$TaskName'"
Write-Host "Para ver su estado: Get-ScheduledTask -TaskName '$TaskName' | Get-ScheduledTaskInfo"
Write-Host "Logs: revisar la consola del proceso no aplica (corre sin ventana); usar Get-Content sobre el log configurado si se agrega file logging, o Event Viewer > Applications."
