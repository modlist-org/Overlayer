# Runs the unit tests (no game DLLs required).
#   .\test.ps1
#   .\test.ps1 --filter "FullyQualifiedName~Signature"
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$TestArgs
)
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
dotnet test (Join-Path $Root 'Overlayer.Tests/Overlayer.Tests.csproj') @TestArgs
