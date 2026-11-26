# Builds the main mod (Mono and/or IL2CPP).
# Requires Directory.Build.props with O5Dev/game paths (copy from Directory.Build.example.props).
#   .\build.ps1
#   .\build.ps1 Release_ML_IL2CPP
#   .\build.ps1 all
param(
    [string]$Configuration = 'Release_ML',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$BuildArgs
)
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
if (-not (Test-Path (Join-Path $Root 'Directory.Build.props')) -and -not (Test-Path (Join-Path $Root 'Overlayer/Directory.Build.props'))) {
    Write-Error 'Directory.Build.props not found (checked root and Overlayer/). Copy Directory.Build.example.props to Directory.Build.props first.'
}
$Proj = Join-Path $Root 'Overlayer/Overlayer.csproj'
if ($Configuration -eq 'all') {
    dotnet build $Proj -c Release_ML @BuildArgs
    dotnet build $Proj -c Release_ML_IL2CPP @BuildArgs
} else {
    dotnet build $Proj -c $Configuration @BuildArgs
}
