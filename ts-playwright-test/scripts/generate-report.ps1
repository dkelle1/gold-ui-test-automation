<#
.SYNOPSIS
  Opens the Allure report with the Allure CLI.
.DESCRIPTION
  Requires the allure CLI: npm i -g allure-commandline, or scoop install allure.
  For the Playwright HTML report - which needs no external CLI - use `npm run report` instead.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$resultsDir = 'allure-results'

if (-not (Test-Path $resultsDir)) {
    Write-Error "No allure-results found at $resultsDir - run scripts/run-tests.ps1 first."
}

# Normally already present: playwright.config.ts's globalSetup copies this in at the start of every run.
Copy-Item 'categories.json' (Join-Path $resultsDir 'categories.json') -Force

if (-not (Get-Command allure -ErrorAction SilentlyContinue)) {
    Write-Error "allure CLI not found. Install it, e.g.: npm i -g allure-commandline"
}

allure serve $resultsDir
