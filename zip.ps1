# Assembles release zips from an existing build output (see tool/make_zips.sh).
# Requires Git Bash / WSL (make_zips.sh is a bash script).
#   .\zip.ps1
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ZipArgs
)
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
& bash (Join-Path $Root 'tool/make_zips.sh') @ZipArgs
