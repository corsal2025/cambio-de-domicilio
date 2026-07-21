$ErrorActionPreference = "Stop"

$projectPath = "C:\Users\raul.salazar\Desktop\PROYECTOS RAUL\outlook-comuna-router\src\OutlookComunaRouter"
$exePath = Join-Path $projectPath "bin\Debug\net10.0\OutlookComunaRouter.exe"
$dashboardUrl = "https://localhost:5001"

$listening = Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction SilentlyContinue
if (-not $listening) {
    # Launching the built exe directly (instead of "dotnet run") keeps this to a single
    # process: "dotnet run" spawns the real app as a separate child whose console window
    # doesn't inherit -WindowStyle Hidden from the wrapper, which is what caused two
    # entries to show up in the taskbar.
    if (-not (Test-Path $exePath)) {
        Start-Process -FilePath "dotnet" -ArgumentList "build" -WorkingDirectory $projectPath -WindowStyle Hidden -Wait
    }
    # Set here (not inherited) so it applies to this run only: without it, the exe runs as
    # Production and skips user-secrets, falling back to appsettings.json's relative
    # "data/router.db" path resolved against the exe's own bin folder, which doesn't exist there.
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    Start-Process -FilePath $exePath -WorkingDirectory $projectPath -WindowStyle Hidden
    Start-Sleep -Seconds 8
}

Start-Process $dashboardUrl
