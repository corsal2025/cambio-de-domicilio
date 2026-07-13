$ErrorActionPreference = "Stop"

$projectPath = "C:\Users\raul.salazar\Desktop\PROYECTOS RAUL\outlook-comuna-router\src\OutlookComunaRouter"
$dashboardUrl = "https://localhost:5001"

$listening = Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction SilentlyContinue
if (-not $listening) {
    Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory $projectPath -WindowStyle Hidden
    Start-Sleep -Seconds 8
}

Start-Process $dashboardUrl
