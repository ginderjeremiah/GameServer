#Requires -Version 5.1
<#
.SYNOPSIS
  Collects merged backend coverage across all test projects, builds a report, and runs the gate.
.DESCRIPTION
  One command for local use and CI. Uses dotnet-coverage (a profiler-based collector, decoupled from
  the test framework) to produce a single merged Cobertura over the whole solution's test run, then
  ReportGenerator for the human-readable report + JSON summary, then build/coverage-gate.ps1 to
  enforce the per-assembly floors. A test failure and a gate breach both yield a non-zero exit.
  Runs on Windows PowerShell 5.1 (local) and PowerShell 7 (CI / pwsh).
.PARAMETER NoBuild
  Skip the solution build (assume the binaries are already current — e.g. CI built in a prior step).
#>
[CmdletBinding()]
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
# Don't let native-command non-zero exits throw under PS7's opt-in behaviour; we steer on $LASTEXITCODE
# so a failing test run still produces a report before we surface the failure. (No-op on 5.1.)
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
  dotnet tool restore
  if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed" }

  if (-not $NoBuild) {
    dotnet build Game.sln -v q -clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw "Solution build failed" }
  }

  New-Item -ItemType Directory -Force -Path 'coverage' | Out-Null
  $cobertura = 'coverage/backend.cobertura.xml'

  # Collect one merged Cobertura across every test project. dotnet-coverage profiles the test host
  # out-of-band, so it is immune to the xunit.v3 / Microsoft.Testing.Platform version coupling that
  # breaks the in-process CodeCoverage extension. The inner `dotnet test` exit code is preserved below.
  dotnet tool run dotnet-coverage collect -f cobertura -o $cobertura "dotnet test Game.sln --no-build --no-restore"
  $testExit = $LASTEXITCODE

  # Build the report. Exclusions are centralized here (one reviewable place): the test/support
  # assemblies, compiler-generated regex, EF migrations, and anything under obj/ are no-logic noise
  # that should neither inflate nor drag the gated numbers.
  dotnet tool run reportgenerator `
    -reports:$cobertura `
    -targetdir:coverage/backend-report `
    -reporttypes:"TextSummary;JsonSummary;Html" `
    "-assemblyfilters:+Game.*;-Game.*.Tests;-Game.TestInfrastructure" `
    "-classfilters:-System.Text.RegularExpressions.Generated*" `
    "-filefilters:-**/obj/**;-**/Migrations/**"
  if ($LASTEXITCODE -ne 0) { throw "ReportGenerator failed" }

  & (Join-Path $PSScriptRoot 'coverage-gate.ps1')
  $gateExit = $LASTEXITCODE

  if ($testExit -ne 0) {
    Write-Host "NOTE: the test run reported failures (exit $testExit); the coverage figures above are from a non-green run." -ForegroundColor Yellow
    exit $testExit
  }
  exit $gateExit
}
finally {
  Pop-Location
}
