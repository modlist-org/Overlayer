# Builds the main mod.
# Requires Directory.Build.props with GamePath (copy from Directory.Build.example.props).
#   .\build.ps1
#   .\build.ps1 Debug_ML
param(
    [string]$Configuration = 'Release_ML',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$BuildArgs
)
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
if (-not (Test-Path (Join-Path $Root 'Directory.Build.props'))) {
    Write-Error 'Directory.Build.props not found. Copy Directory.Build.example.props and set GamePath first.'
}
dotnet build (Join-Path $Root 'Overlayer/Overlayer.csproj') -c $Configuration @BuildArgs
