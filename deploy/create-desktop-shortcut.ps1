<#
.SYNOPSIS
    Crea un acceso directo en el Escritorio que abre el dashboard de
    CambioDeDomicilio en el navegador, iniciando el proceso si no está corriendo.

.DESCRIPTION
    El servicio normalmente corre como Tarea Programada (ver install-task.ps1) y
    ya está activo en segundo plano. Este acceso directo es para el caso en que el
    operador quiera abrir el dashboard sin depender de que la tarea programada esté
    corriendo (por ejemplo, en un equipo donde el servicio se inicia manualmente).

.PARAMETER PublishPath
    Carpeta donde está publicado CambioDeDomicilio.exe.

.PARAMETER DashboardUrl
    URL del dashboard a abrir. Por defecto usa localhost:5001 (HTTPS, ver appsettings.json).
#>

param(
    [string]$PublishPath = (Resolve-Path (Join-Path $PSScriptRoot "..\publish")).Path,
    [string]$DashboardUrl = "https://localhost:5001"
)

$ErrorActionPreference = "Stop"

$exePath = (Resolve-Path (Join-Path $PublishPath "CambioDeDomicilio.exe")).Path
if (-not (Test-Path $exePath)) {
    throw "No se encontró $exePath. Ejecuta primero: dotnet publish src/CambioDeDomicilio -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish"
}

# Script intermedio que el acceso directo ejecuta: inicia el proceso solo si no está
# corriendo (evita instancias duplicadas escuchando el mismo puerto), y siempre abre
# el navegador en la URL del dashboard.
$launcherPath = Join-Path $PublishPath "abrir-dashboard.ps1"
$launcherContent = @"
`$running = Get-Process -Name "CambioDeDomicilio" -ErrorAction SilentlyContinue
if (-not `$running) {
    # The app opens its own browser tab ~2s after it starts binding (see Program.cs).
    # Only open a tab here when it was already running, otherwise the user gets 2 tabs.
    Start-Process -FilePath "$exePath" -WorkingDirectory "$PublishPath" -WindowStyle Hidden
} else {
    Start-Process "$DashboardUrl"
}
"@
Set-Content -Path $launcherPath -Value $launcherContent -Encoding UTF8

# VBScript wrapper so Windows never draws a console/terminal window (style 0)
$vbsPath = Join-Path $PublishPath "abrir-dashboard.vbs"
$vbsContent = @"
Set shell = CreateObject("WScript.Shell")
scriptDir = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
psScript = scriptDir & "\abrir-dashboard.ps1"
shell.Run "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & psScript & """", 0, False
"@
Set-Content -Path $vbsPath -Value $vbsContent -Encoding ASCII

$desktopPath = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktopPath "CambioDeDomicilio - Dashboard.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = "wscript.exe"
$shortcut.Arguments = "`"$vbsPath`""
$shortcut.WorkingDirectory = $PublishPath
$shortcut.Description = "Abrir el dashboard de CambioDeDomicilio"
$iconPath = Join-Path $PublishPath "wwwroot\img\logo-municipalidad.ico"
if (-not (Test-Path $iconPath)) {
    $iconPath = Join-Path $PSScriptRoot "logo-municipalidad.ico"
}
if (Test-Path $iconPath) {
    $shortcut.IconLocation = "$iconPath,0"
} else {
    $shortcut.IconLocation = "$exePath,0"
}
$shortcut.Save()

# Remove the console service shortcut if it exists
$directShortcutPath = Join-Path $desktopPath "CambioDeDomicilio (Servicio).lnk"
if (Test-Path $directShortcutPath) {
    Remove-Item $directShortcutPath -Force
}

Write-Host "Acceso directo 100% silencioso creado en: $shortcutPath"
Write-Host "Al hacer doble clic: se ejecuta en segundo plano sin ventana negra de consola y abre $DashboardUrl."
