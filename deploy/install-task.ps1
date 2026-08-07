<#
.SYNOPSIS
    Instala CambioDeDomicilio como Tarea Programada de Windows, corriendo de forma
    continua (no es una tarea de un solo disparo) con reinicio automático ante fallas.

.DESCRIPTION
    Requiere ejecutarse como Administrador. Antes de correr este script:
      1. Publicar la app:  dotnet publish src/CambioDeDomicilio -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
      2. Completar publish/appsettings.json (o crear appsettings.Development.json) con
         Router:Ews:Username / Router:Ews:Password reales de la cuenta de AD del buzón
         (no requiere Azure AD ni aprobación de TI — ver README.md).
      3. Completar data/comunas.csv con el directorio real de comunas.
      4. Confiar el certificado HTTPS local (dotnet dev-certs https --trust) y crear al
         menos un usuario del dashboard (publish\CambioDeDomicilio.exe --add-user <nombre>).

    Sin las credenciales EWS reales, la tarea se instalará pero el servicio fallará su
    ciclo de lectura en cada intento (queda reintentando, no crashea el proceso, pero
    tampoco procesará correos reales).
#>

param(
    [string]$TaskName = "CambioDeDomicilio",
    [string]$PublishPath = (Join-Path $PSScriptRoot "..\publish"),
    [string]$RunAsUser = $env:USERNAME
)

$ErrorActionPreference = "Stop"

$exePath = Join-Path $PublishPath "CambioDeDomicilio.exe"
if (-not (Test-Path $exePath)) {
    throw "No se encontró $exePath. Ejecuta primero: dotnet publish src/CambioDeDomicilio -c Release -o publish"
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
    -Description "Sondeo continuo de cambiodedomicilio@munivalpo.cl (CambioDeDomicilio)" `
    -Force

Write-Host "Tarea '$TaskName' registrada. Para iniciarla ahora: Start-ScheduledTask -TaskName '$TaskName'"
Write-Host "Para ver su estado: Get-ScheduledTask -TaskName '$TaskName' | Get-ScheduledTaskInfo"
Write-Host "Logs: revisar la consola del proceso no aplica (corre sin ventana); usar Get-Content sobre el log configurado si se agrega file logging, o Event Viewer > Applications."
