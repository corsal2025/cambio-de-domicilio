<#
.SYNOPSIS
    Detiene y elimina la Tarea Programada de OutlookComunaRouter.
#>

param(
    [string]$TaskName = "OutlookComunaRouter"
)

$ErrorActionPreference = "Stop"

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "Tarea '$TaskName' eliminada."
} else {
    Write-Host "No existe una tarea llamada '$TaskName'."
}
