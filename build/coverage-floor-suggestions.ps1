#Requires -Version 5.1
<#
.SYNOPSIS
  Prints paste-ready, margin-safe floors for build/coverage-floors.json from the latest coverage run.
.DESCRIPTION
  Seeding a floor at an assembly's exact current percentage leaves zero margin against ordinary
  run-to-run coverage noise (the DB-integration suites cover a handful of Game.DataAccess lines only
  under real timing/retry conditions, so the exact covered-line count can drift by one between runs).
  When that noise straddles a floor seeded flush against actual coverage, CI fails on a commit that
  changed nothing relevant (#1626/#1627).

  This computes each gated assembly's and gated namespace's *exact* line/branch percentage from the
  report's raw covered/coverable counts (not ReportGenerator's truncated display value), subtracts
  -MarginPoints, and floors the result to a whole percent — the same shape of number already hand-
  written into coverage-floors.json, just never seeded flush. It only ever suggests raising a floor
  (matching the ratchet's "only ever rises" rule); it prints, it does not write, so a real coverage
  regression is still a deliberate, reviewable edit rather than a silent floor drop.

  Run after build/coverage.ps1 (or dotnet-coverage + reportgenerator directly), then paste any raised
  values into build/coverage-floors.json by hand:
    ./build/coverage.ps1 -NoBuild
    ./build/coverage-floor-suggestions.ps1
.PARAMETER MarginPoints
  Percentage points of headroom to leave below actual coverage when suggesting a floor. Default 1.0 —
  comfortably larger than the ~0.1-point noise a single flaky line produces on assemblies this size.
#>
[CmdletBinding()]
param(
  [string]$SummaryPath,
  [string]$FloorsPath,
  [double]$MarginPoints = 1.0
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'coverage-lib.ps1')

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

# Exact percentage from raw counts, margin subtracted, floored to a whole percent — never a
# fractional floor, matching the style already hand-written into coverage-floors.json.
function Get-SuggestedFloor($covered, $total, $marginPoints) {
  if ($null -eq $total -or $total -eq 0) { return $null }
  $actual = 100.0 * $covered / $total
  $suggested = [math]::Floor($actual - $marginPoints)
  if ($suggested -lt 0) { $suggested = 0 }
  return [pscustomobject]@{ Actual = $actual; Suggested = $suggested }
}

function Show-Suggestion($label, $currentFloor, $metric) {
  if ($null -eq $metric) { return }
  $arrow = if ($metric.Suggested -gt $currentFloor) { '(raise)' } elseif ($metric.Suggested -lt $currentFloor) { '(below current — keep existing, ratchet only rises)' } else { '(no change)' }
  $kept = [math]::Max($currentFloor, $metric.Suggested)
  Write-Host ("  {0,-10} actual {1,7:N4}%  current floor {2,3}  suggested {3,3}  -> {4,3} {5}" -f $label, $metric.Actual, $currentFloor, $metric.Suggested, $kept, $arrow)
}

Write-Host ""
Write-Host "Gated assemblies (margin ${MarginPoints}pt):" -ForegroundColor Cyan
foreach ($name in @($floors.gated.PSObject.Properties.Name)) {
  $floor = $floors.gated.$name
  $a = $byName[$name]
  if ($null -eq $a) {
    Write-Host "$name -- missing from coverage report (renamed or not built?)" -ForegroundColor Yellow
    continue
  }
  Write-Host "$name"
  Show-Suggestion 'line' $floor.line (Get-SuggestedFloor $a.coveredlines $a.coverablelines $MarginPoints)
  if ($null -ne $floor.branch) {
    Show-Suggestion 'branch' $floor.branch (Get-SuggestedFloor $a.coveredbranches $a.totalbranches $MarginPoints)
  }
}

if ($null -ne $floors.gatedNamespaces) {
  Write-Host ""
  Write-Host "Gated namespaces (margin ${MarginPoints}pt):" -ForegroundColor Cyan
  foreach ($name in @($floors.gatedNamespaces.PSObject.Properties.Name)) {
    $entry = $floors.gatedNamespaces.$name
    $a = $byName[$entry.assembly]
    if ($null -eq $a) {
      Write-Host "$name -- assembly '$($entry.assembly)' missing from coverage report" -ForegroundColor Yellow
      continue
    }
    $ns = Get-NamespaceCoverage $a @($entry.namespaces)
    Write-Host "$name"
    Show-Suggestion 'line' $entry.line (Get-SuggestedFloor $ns.CoveredLines $ns.CoverableLines $MarginPoints)
    if ($null -ne $entry.branch) {
      Show-Suggestion 'branch' $entry.branch (Get-SuggestedFloor $ns.CoveredBranches $ns.TotalBranches $MarginPoints)
    }
  }
}
Write-Host ""
Write-Host "Only the '-> N (raise)' column is ever safe to paste into coverage-floors.json; a floor never moves down." -ForegroundColor DarkGray
