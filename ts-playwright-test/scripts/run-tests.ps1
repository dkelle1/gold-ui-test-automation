<#
.SYNOPSIS
  Runs the Playwright Test suite.
.EXAMPLE
  scripts/run-tests.ps1
  scripts/run-tests.ps1 -Project chromium -Grep '@smoke'
  scripts/run-tests.ps1 -Project all
#>
[CmdletBinding()]
param(
    [string]$Project = 'chromium',
    [string]$Grep = ''
)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$testArgs = @()
if ($Project -ne 'all') {
    $testArgs += @('--project', $Project)
}
if ($Grep) {
    $testArgs += @('--grep', $Grep)
}

npx playwright test @testArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
