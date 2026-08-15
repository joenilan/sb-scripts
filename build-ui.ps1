param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [string]$StreamerBotPath = '',

    [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CRNTLY.StreamerBot.UI\CRNTLY.StreamerBot.UI.csproj'
$outputDir = Join-Path $PSScriptRoot "CRNTLY.StreamerBot.UI\bin\$Configuration\net481"
$dll = Join-Path $outputDir 'CRNTLY.StreamerBot.UI.dll'

function Resolve-StreamerBotPath {
    param([string]$ExplicitPath)

    $candidates = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $candidates.Add($ExplicitPath)
    }

    foreach ($envName in @('STREAMERBOT_PATH', 'STREAMER_BOT_PATH')) {
        $value = [Environment]::GetEnvironmentVariable($envName)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $candidates.Add($value)
        }
    }

    try {
        $running = Get-Process -Name 'Streamer.bot' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($running -and $running.Path) {
            $candidates.Add((Split-Path -Parent $running.Path))
        }
    }
    catch {
        # Process path lookup can fail without access; continue to folder discovery.
    }

    if ($env:USERPROFILE -and (Test-Path $env:USERPROFILE)) {
        Get-ChildItem -Path $env:USERPROFILE -Directory -Filter 'streamer.bot*' -ErrorAction SilentlyContinue |
            ForEach-Object { $candidates.Add($_.FullName) }
    }

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        $fullPath = [IO.Path]::GetFullPath($candidate)
        if (Test-Path (Join-Path $fullPath 'Streamer.bot.exe')) {
            return $fullPath
        }
    }

    return $null
}

# Never allow a failed build to look successful because an older DLL still exists.
if (Test-Path $dll) {
    Remove-Item $dll -Force
}

dotnet restore $project
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE. Nothing was deployed."
}

dotnet build $project -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE. Nothing was deployed."
}

if (-not (Test-Path $dll)) {
    throw "Build reported success but DLL was not found at: $dll"
}

Write-Host ""
Write-Host "Built: $dll"

if ($NoDeploy) {
    Write-Host "Deployment skipped (-NoDeploy)."
    exit 0
}

$resolvedStreamerBotPath = Resolve-StreamerBotPath $StreamerBotPath
if (-not $resolvedStreamerBotPath) {
    Write-Warning "Streamer.bot was not found automatically. The DLL was built but not deployed."
    Write-Host "Run again with:"
    Write-Host ".\build-ui.ps1 -StreamerBotPath 'C:\path\to\streamer.bot'"
    exit 0
}

$dllsPath = Join-Path $resolvedStreamerBotPath 'dlls'
New-Item -ItemType Directory -Path $dllsPath -Force | Out-Null

# CRNTLY is loaded dynamically by the Streamer.bot script. Any WPF framework
# dependencies it uses must live beside it so the CLR can resolve them at runtime.
$runtimeDlls = @()
$runtimeDlls += Get-Item $dll
$runtimeDlls += Get-ChildItem -Path $outputDir -File -Filter 'ModernWpf*.dll' -ErrorAction SilentlyContinue
$runtimeDlls = $runtimeDlls | Sort-Object FullName -Unique

foreach ($runtimeDll in $runtimeDlls) {
    $destination = Join-Path $dllsPath $runtimeDll.Name
    Copy-Item -Path $runtimeDll.FullName -Destination $destination -Force
    Write-Host "Deployed: $destination"
}

Write-Host "Streamer.bot can resolve CRNTLY and its UI dependencies from the dlls folder."
Write-Host "If Streamer.bot was already running with an older copy loaded, restart it before testing the new build."
