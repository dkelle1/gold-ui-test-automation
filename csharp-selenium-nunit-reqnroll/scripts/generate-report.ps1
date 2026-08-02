# Copies categories.json into the Allure results and opens the report with the Allure CLI.
# Requires the allure CLI: npm i -g allure-commandline, or scoop install allure.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$resultsDir = "SauceDemo.UiTests/bin/Release/net10.0/allure-results"

if (-not (Test-Path $resultsDir)) {
    Write-Error "No allure-results found at $resultsDir - run scripts/run-tests.ps1 first."
    exit 1
}

Copy-Item "SauceDemo.UiTests/categories.json" (Join-Path $resultsDir "categories.json") -Force

if (-not (Get-Command allure -ErrorAction SilentlyContinue)) {
    Write-Error "allure CLI not found. Install it, e.g.: npm i -g allure-commandline / scoop install allure"
    exit 1
}

allure serve $resultsDir
