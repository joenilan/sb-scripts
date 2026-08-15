param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [string]$StreamerBotPath = '',

    [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CRNTLY.StreamerBot.UI\CRNTLY.StreamerBot.UI.csproj'

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

dotnet restore $project
dotnet build $project -c $Configuration --no-restore

$dll = Join-Path $PSScriptRoot "CRNTLY.StreamerBot.UI\bin\$Configuration\net481\CRNTLY.StreamerBot.UI.dll"
if (-not (Test-Path $dll)) {
    throw "Build completed but DLL was not found at: $dll"
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

$destination = Join-Path $dllsPath 'CRNTLY.StreamerBot.UI.dll'
Copy-Item -Path $dll -Destination $destination -Force

Write-Host "Deployed: $destination"
Write-Host "Streamer.bot can resolve custom assemblies from its dlls folder."
Write-Host "If Streamer.bot was already running with an older copy loaded, restart it before testing the new build."
