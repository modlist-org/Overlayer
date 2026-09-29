# Prints the game Unity + MelonLoader versions in release-notes format.
# Game path comes from Directory.Build.props (root or Overlayer/) or args.
#   .\tool\check_version.ps1 [GamePath] [GameData]
param(
    [string]$GamePath = '',
    [string]$GameData = ''
)
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$Props = Join-Path $Root 'Directory.Build.props'
if (-not (Test-Path $Props)) { $Props = Join-Path $Root 'Overlayer/Directory.Build.props' }
if (-not $GamePath) {
    $m = Select-String -Path $Props -Pattern '<GamePath>(.*?)</GamePath>' | Select-Object -First 1
    if ($m) { $GamePath = $m.Matches[0].Groups[1].Value }
}
if (-not $GameData) {
    $m = Select-String -Path $Props -Pattern '<GameData>(.*?)</GameData>' | Select-Object -First 1
    if ($m) { $GameData = $m.Matches[0].Groups[1].Value }
}
if (-not $GamePath -or -not $GameData) { Write-Error 'usage: .\tool\check_version.ps1 [GamePath] [GameData]' }

$UnityVer = ''
$bins = @(Get-ChildItem (Join-Path $GamePath 'UnityPlayer.*') -File -ErrorAction SilentlyContinue) + @(
    Join-Path $GamePath "$GameData/globalgamemanagers"
)
foreach ($bin in $bins) {
    $p = if ($bin -is [string]) { $bin } else { $bin.FullName }
    if (-not (Test-Path $p)) { continue }
    $text = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($p))
    $m = [regex]::Match($text, '6000\.[0-9]+\.[0-9]+[a-z][0-9]+|202[0-9]\.[0-9]+\.[0-9]+[a-z][0-9]+')
    if ($m.Success) { $UnityVer = $m.Value; break }
}

$MlVer = ''
$MlFile = Join-Path $GamePath 'MelonLoader/MelonLoader.version'
if (Test-Path $MlFile) { $MlVer = (Get-Content $MlFile -Raw).Trim() }

$UnityOut = if ($UnityVer) { $UnityVer } else { 'unknown' }
$MlOut = if ($MlVer) { $MlVer } else { 'unknown' }
Write-Host "Build Unity Version : ``$UnityOut``"
Write-Host "ML : ``$MlOut``"
