# Sets the version via tool/set_version.sh. The base is the last git tag so a
# release can never be skipped (5.0.3 -> 5.0.5 blocked, 5.0.3 -> 5.1.0 allowed).
# Bypass with -Force/-f.
#   .\bump.ps1 patch
#   .\bump.ps1 minor
#   .\bump.ps1 major
#   .\bump.ps1 5.0.4
#   .\bump.ps1 patch -Force
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Target,
    [Alias('f')]
    [switch]$Force
)
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
$Info = Join-Path $Root 'Overlayer/Core/Info.cs'

$IsPart = $Target -in @('patch', 'minor', 'major')
$IsVersion = $Target -match '^[0-9]+\.[0-9]+\.[0-9]+$'
if (-not $IsPart -and -not $IsVersion) { Write-Error 'usage: .\bump.ps1 <patch|minor|major|X.Y.Z> [-Force|-f]' }

$line = Select-String -Path $Info -Pattern 'public const string Version = "([^"]+)"' | Select-Object -First 1
$Cur = $line.Matches[0].Groups[1].Value
if ($Cur -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') { Write-Error "Cannot read current version from $Info." }

try { $LastTag = (git -C $Root describe --tags --abbrev=0 2>$null) } catch { $LastTag = $null }
$Base = $Cur
if ($LastTag) {
    $TagContent = (git -C $Root show "${LastTag}:Overlayer/Core/Info.cs" 2>$null) -join "`n"
    $m = [regex]::Match($TagContent, 'public const string Version = "([^"]+)"')
    if ($m.Success) { $Base = $m.Groups[1].Value }
}
$TagDisplay = if ($LastTag) { $LastTag } else { 'none' }

if ($IsPart) {
    $b = $Base.Split('.')
    $Ma, $Mi, $Pa = [int]$b[0], [int]$b[1], [int]$b[2]
    switch ($Target) {
        'patch' { $Pa += 1 }
        'minor' { $Mi += 1; $Pa = 0 }
        'major' { $Ma += 1; $Mi = 0; $Pa = 0 }
    }
    $New = "$Ma.$Mi.$Pa"
    if (-not $Force -and -not ([Version]$New -gt [Version]$Cur)) {
        Write-Error "$New would not move past working tree $Cur (last tag: $TagDisplay). Pick a bigger part, or retry with -Force."
    }
} else {
    $New = $Target
    if (-not $Force) {
        $b = $Base.Split('.')
        $Allowed = @("$($b[0]).$($b[1]).$([int]$b[2] + 1)", "$($b[0]).$([int]$b[1] + 1).0", "$([int]$b[0] + 1).0.0")
        if ($Allowed -notcontains $New) {
            Write-Error "$New is not one step from $Base (last tag: $TagDisplay). Allowed: $($Allowed -join ' ') (or retry with -Force)."
        }
    }
}

Write-Host "--- changes since last tag ($TagDisplay) ---"
if ($LastTag) { git -C $Root log --oneline "$LastTag..HEAD" } else { git -C $Root log --oneline -10 }
Write-Host "--- $Cur -> $New ($Target) ---"

& bash (Join-Path $Root 'tool/set_version.sh') $New

Write-Host "next: git add Overlayer/Core/Info.cs Overlayer/Overlayer.csproj && git commit -m `"$New`" && git push"
