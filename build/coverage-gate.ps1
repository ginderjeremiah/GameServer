#Requires -Version 5.1
<#
.SYNOPSIS
  Fails the build when a gated assembly's coverage drops below its floor.
.DESCRIPTION
  Reads ReportGenerator's JSON summary and build/coverage-floors.json. Assemblies listed under
  "gated" must meet their line (and optional branch) floor; every other measured assembly is
  reported as measure-only and never fails the gate. Floors are a ratchet — seeded at the current
  level and only ever raised. Normally invoked by build/coverage.ps1 (which produces the summary
  first), but can be run standalone against an existing report.
  Runs on Windows PowerShell 5.1 (local) and PowerShell 7 (CI / pwsh).
.OUTPUTS
  Exit 0 = all gated assemblies meet their floors. Exit 1 = a floor was breached.
  Exit 2 = the coverage summary was not found.
#>
[CmdletBinding()]
param(
  [string]$SummaryPath,
  [string]$FloorsPath
)
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $SummaryPath) { $SummaryPath = Join-Path $repoRoot 'coverage/backend-report/Summary.json' }
if (-not $FloorsPath) { $FloorsPath = Join-Path $PSScriptRoot 'coverage-floors.json' }

if (-not (Test-Path $SummaryPath)) {
  Write-Host "Coverage summary not found at $SummaryPath. Run build/coverage.ps1 first." -ForegroundColor Red
  exit 2
}

$summary = Get-Content $SummaryPath -Raw | ConvertFrom-Json
$floors = Get-Content $FloorsPath -Raw | ConvertFrom-Json

$byName = @{}
foreach ($a in $summary.coverage.assemblies) { $byName[$a.name] = $a }

$gatedNames = @($floors.gated.PSObject.Properties.Name)
$failures = New-Object System.Collections.Generic.List[string]
$rows = New-Object System.Collections.Generic.List[object]

function Format-Pct($v) {
  if ($null -eq $v) { return 'n/a' }
  return ("{0}%" -f $v)
}

foreach ($name in $gatedNames) {
  $floor = $floors.gated.$name
  $a = $byName[$name]
  if ($null -eq $a) {
    $failures.Add("$name is gated but missing from the coverage report (renamed or not built?)")
    $rows.Add([pscustomobject]@{ Assembly = $name; Line = '--'; Branch = '--'; Floor = "L$($floor.line)"; Status = 'MISSING' })
    continue
  }
  $lineOk = $a.coverage -ge $floor.line
  $branchOk = $true
  if ($null -ne $floor.branch -and $null -ne $a.branchcoverage) {
    $branchOk = $a.branchcoverage -ge $floor.branch
  }
  if (-not $lineOk) { $failures.Add("$name line $($a.coverage)% < floor $($floor.line)%") }
  if (-not $branchOk) { $failures.Add("$name branch $($a.branchcoverage)% < floor $($floor.branch)%") }
  $status = 'PASS'
  if (-not ($lineOk -and $branchOk)) { $status = 'FAIL' }
  $floorText = "L$($floor.line)"
  if ($null -ne $floor.branch) { $floorText = "L$($floor.line)/B$($floor.branch)" }
  $rows.Add([pscustomobject]@{ Assembly = $name; Line = (Format-Pct $a.coverage); Branch = (Format-Pct $a.branchcoverage); Floor = $floorText; Status = $status })
}

foreach ($a in $summary.coverage.assemblies) {
  if ($gatedNames -contains $a.name) { continue }
  $rows.Add([pscustomobject]@{ Assembly = $a.name; Line = (Format-Pct $a.coverage); Branch = (Format-Pct $a.branchcoverage); Floor = 'measure-only'; Status = '-' })
}

Write-Host ""
$sorted = $rows | Sort-Object @{ Expression = { if ($_.Floor -eq 'measure-only') { 1 } else { 0 } } }, Assembly
($sorted | Format-Table -AutoSize | Out-String).TrimEnd() | Write-Host
Write-Host ""

if ($failures.Count -gt 0) {
  Write-Host "Backend coverage gate FAILED:" -ForegroundColor Red
  foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
  exit 1
}
Write-Host "Backend coverage gate passed ($($gatedNames.Count) gated assemblies)." -ForegroundColor Green
exit 0
