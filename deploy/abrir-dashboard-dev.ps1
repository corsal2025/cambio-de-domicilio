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
    Start-Process -FilePath $exePath -WorkingDirectory $projectPath -WindowStyle Hidden
    Start-Sleep -Seconds 8
}

Start-Process $dashboardUrl
