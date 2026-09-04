<#
.SYNOPSIS
  Run the backend test suites.
.DESCRIPTION
  Usage: scripts/run-tests.ps1 [-Suite all|unit|integration]
    all         -> unit + integration (default)
    unit        -> xUnit + NUnit unit tests only (no Docker needed)
    integration -> Testcontainers/Respawn integration tests only (Docker required)

  The integration suite needs a running Docker daemon: Testcontainers pulls
  mcr.microsoft.com/mssql/server and starts a throwaway SQL Server for the run.
#>
param(
    [ValidateSet('all', 'unit', 'integration')]
    [string]$Suite = 'all'
)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

dotnet restore Backend.sln
dotnet build Backend.sln -c Release --no-restore

function Invoke-Unit {
    dotnet test tests/OrderService.UnitTests.Xunit/OrderService.UnitTests.Xunit.csproj `
        -c Release --no-build `
        --logger "trx;LogFileName=unit-xunit.trx" `
        --results-directory artifacts
    dotnet test tests/OrderService.UnitTests.Nunit/OrderService.UnitTests.Nunit.csproj `
        -c Release --no-build `
        --logger "trx;LogFileName=unit-nunit.trx" `
        --results-directory artifacts
}

function Invoke-Integration {
    dotnet test tests/OrderService.IntegrationTests/OrderService.IntegrationTests.csproj `
        -c Release --no-build `
        --logger "trx;LogFileName=integration.trx" `
        --results-directory artifacts `
        --blame-hang-timeout 5min
}

switch ($Suite) {
    'unit'        { Invoke-Unit }
    'integration' { Invoke-Integration }
    'all'         { Invoke-Unit; Invoke-Integration }
}
