$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\CambioDeDomicilio"
$exePath = Join-Path $projectPath "bin\Debug\net10.0\CambioDeDomicilio.exe"
$dashboardUrl = "https://localhost:5001"

# Get-NetTCPConnection -State Listen is unreliable here (observed returning no rows even
# while the exe was demonstrably serving requests on 5001) — checking the process itself
# is what every manual check in this project actually relies on, so match that here too.
$running = Get-Process -Name "CambioDeDomicilio" -ErrorAction SilentlyContinue
if (-not $running) {
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
    # The app opens its own browser tab ~2s after it starts binding (see Program.cs).
    # Don't open one here too, otherwise the user gets 2 tabs.
} else {
    Start-Process $dashboardUrl
}
