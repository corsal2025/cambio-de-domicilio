<#
.SYNOPSIS
    Unified publish script for CambioDeDomicilio.
    Runs steps 4–9 from deploy/README.md in one command.

.DESCRIPTION
    - Builds and publishes self-contained single-file exe
    - Copies runtime data (comunas.csv, etc.)
    - Installs the scheduled task (requires admin)
    - Creates desktop shortcut (optional)
    - Output: ./publish/CambioDeDomicilio.exe

.PARAMETER PublishDir
    Output directory for the published exe (default: ./publish)

.PARAMETER InstallTask
    Register the Windows Scheduled Task (requires admin)

.PARAMETER Shortcut
    Create a desktop shortcut after publish

.PARAMETER DevCert
    Trust the ASP.NET dev HTTPS certificate

.PARAMETER ConfigOnly
    Only copy config and data files, skip dotnet publish

.EXAMPLE
    # Quick publish (no admin needed)
    .\deploy\publish.ps1

    # Full production deploy
    .\deploy\publish.ps1 -DevCert -InstallTask -Shortcut

    # Second PC: just copy publish folder, trust cert
    .\deploy\publish.ps1 -ConfigOnly -DevCert
#>

param(
    [string]$PublishDir = "publish",
    [switch]$InstallTask,
    [switch]$Shortcut,
    [switch]$DevCert,
    [switch]$ConfigOnly
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = "src\CambioDeDomicilio\CambioDeDomicilio.csproj"

# Resolve full path
$PublishPath = Join-Path $RepoRoot $PublishDir
Write-Host "=== CambioDeDomicilio: Publish Script ===" -ForegroundColor Cyan
Write-Host "Repo:     $RepoRoot"
Write-Host "Output:   $PublishPath"
Write-Host ""

# Step 0: dev certificate
if ($DevCert) {
    Write-Host "[1/5] Instalando certificado HTTPS de desarrollo..." -ForegroundColor Yellow
    dotnet dev-certs https --trust
    Write-Host "  ✅ Certificado instalado." -ForegroundColor Green
    Write-Host ""
}

# Step 1: dotnet publish (or config-only)
if (-not $ConfigOnly) {
    Write-Host "[2/5] Publicando (self-contained, single-file)..." -ForegroundColor Yellow
    Push-Location $RepoRoot
    try {
        dotnet restore $Project
        dotnet build $Project -c Release --no-restore
        dotnet test tests\CambioDeDomicilio.Tests\CambioDeDomicilio.Tests.csproj -c Release --no-build --verbosity normal
        dotnet publish $Project -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o $PublishPath
    } finally {
        Pop-Location
    }
    Write-Host "  ✅ Publicado en: $PublishPath" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[2/5] Saltando dotnet publish (modo config-only)." -ForegroundColor Yellow
    if (-not (Test-Path $PublishPath\CambioDeDomicilio.exe)) {
        Write-Warning "No se encontró $PublishPath\CambioDeDomicilio.exe. Ejecuta sin -ConfigOnly primero."
        exit 1
    }
    Write-Host ""
}

# Step 2: copy data files
Write-Host "[3/5] Copiando datos de runtime..." -ForegroundColor Yellow
$TargetData = Join-Path $PublishPath "data"
if (Test-Path (Join-Path $RepoRoot "data")) {
    New-Item -ItemType Directory -Path $TargetData -Force | Out-Null
    # NEVER copy SQLite databases: publish\data\router.db is the production database and the
    # repo's data\router.db is a dev copy — overwriting it would wipe real cases. Other runtime
    # files (comunas.csv, certificate) are only copied when missing, so operator edits survive.
    Get-ChildItem (Join-Path $RepoRoot "data") -File |
        Where-Object { $_.Name -notlike 'router.db*' -and $_.Extension -ne '.db' } |
        ForEach-Object {
            $destination = Join-Path $TargetData $_.Name
            if (-not (Test-Path $destination)) { Copy-Item $_.FullName $destination }
        }
    Write-Host "  ✅ data/ completado en: $TargetData (base de datos existente intacta)" -ForegroundColor Green
} else {
    Write-Host "  ⚠️  No hay data/ en la raíz. Crea data/comunas.csv manualmente." -ForegroundColor Yellow
}
Write-Host ""

# Step 3: create config from example if missing
Write-Host "[4/5] Configurando appsettings.json..." -ForegroundColor Yellow
$TargetConfig = Join-Path $PublishPath "appsettings.json"
if (-not (Test-Path $TargetConfig)) {
    $ExampleConfig = Join-Path $RepoRoot "src\CambioDeDomicilio\appsettings.Example.json"
    if (Test-Path $ExampleConfig) {
        Copy-Item $ExampleConfig $TargetConfig
        Write-Host "  ✅ appsettings.json creado desde appsettings.Example.json" -ForegroundColor Green
        Write-Host "  ⚠️  EDITALO: completa Router:Ews:Username, Router:Ews:Password y Kestrel:Certificates:Default:Password" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ✅ appsettings.json ya existe" -ForegroundColor Green
}
Write-Host ""

# Step 4: install scheduled task
if ($InstallTask) {
    Write-Host "[5/5] Instalando tarea programada..." -ForegroundColor Yellow
    $InstallScript = Join-Path $RepoRoot "deploy\install-task.ps1"
    if (Test-Path $InstallScript) {
        & $InstallScript
        Write-Host "  ✅ Tarea instalada." -ForegroundColor Green
    } else {
        Write-Warning "No se encontró $InstallScript"
    }
    Write-Host ""
}

# Step 5: desktop shortcut
if ($Shortcut) {
    $ShortcutScript = Join-Path $RepoRoot "deploy\create-desktop-shortcut.ps1"
    if (Test-Path $ShortcutScript) {
        & $ShortcutScript
        Write-Host "  ✅ Acceso directo creado." -ForegroundColor Green
    }
    Write-Host ""
}

Write-Host "=== Listo ===" -ForegroundColor Cyan
Write-Host "Ejecutable: $PublishPath\CambioDeDomicilio.exe"
Write-Host "Modo prueba: .\CambioDeDomicilio.exe --smoke-test"
Write-Host "Iniciar:     Start-ScheduledTask -TaskName CambioDeDomicilio"
Write-Host "Dashboard:   https://localhost:5001"
