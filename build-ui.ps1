param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CRNTLY.StreamerBot.UI\CRNTLY.StreamerBot.UI.csproj'

dotnet restore $project
dotnet build $project -c $Configuration --no-restore

$dll = Join-Path $PSScriptRoot "CRNTLY.StreamerBot.UI\bin\$Configuration\net481\CRNTLY.StreamerBot.UI.dll"
Write-Host ""
Write-Host "Built: $dll"
Write-Host "Add this DLL under References in the Streamer.bot Execute C# Code editor."
