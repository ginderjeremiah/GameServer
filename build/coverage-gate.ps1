#Requires -Version 5.1
<#
.SYNOPSIS
  Fails the build when a gated assembly (or gated namespace sub-area) drops below its floor.
.DESCRIPTION
  Reads ReportGenerator's JSON summary and build/coverage-floors.json. Assemblies listed under
  "gated" must meet their line (and optional branch) floor. Entries under "gatedNamespaces" gate a
  namespace subset of an otherwise measure-only assembly: their line/branch coverage is aggregated
  from the report's per-class entries (summing covered/coverable lines and covered/total branches
  for the classes in the listed namespaces), so logic-heavy corners of a thin-adapter assembly can
  be ratcheted without gating the whole assembly. Every other measured assembly is reported as
  measure-only and never fails the gate. Floors are a ratchet — seeded at the current level and only
  ever raised. Normally invoked by build/coverage.ps1 (which produces the summary first), but can be
  run standalone against an existing report.
  Runs on Windows PowerShell 5.1 (local) and PowerShell 7 (CI / pwsh).
.OUTPUTS
  Exit 0 = all gated assemblies and namespaces meet their floors. Exit 1 = a floor was breached.
  Exit 2 = the coverage summary was not found.
#>
[CmdletBinding()]
param(
  [string]$SummaryPath,
  [string]$FloorsPath
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

# Gated namespace sub-areas: a logic-heavy slice of an otherwise measure-only assembly, gated on the
# coverage aggregated from its per-class entries.
$gatedNsNames = @()
if ($null -ne $floors.gatedNamespaces) { $gatedNsNames = @($floors.gatedNamespaces.PSObject.Properties.Name) }
foreach ($name in $gatedNsNames) {
  $entry = $floors.gatedNamespaces.$name
  $a = $byName[$entry.assembly]
  if ($null -eq $a) {
    $failures.Add("$name is gated but its assembly '$($entry.assembly)' is missing from the coverage report (renamed or not built?)")
    $rows.Add([pscustomobject]@{ Assembly = $name; Line = '--'; Branch = '--'; Floor = "L$($entry.line)"; Status = 'MISSING' })
    continue
  }
  $ns = Get-NamespaceCoverage $a @($entry.namespaces)
  if ($ns.CoverableLines -eq 0) {
    $failures.Add("$name matched no covered classes in '$($entry.assembly)' (namespaces removed or renamed?)")
    $rows.Add([pscustomobject]@{ Assembly = $name; Line = '--'; Branch = '--'; Floor = "L$($entry.line)"; Status = 'MISSING' })
    continue
  }
  $lineOk = $ns.LinePct -ge $entry.line
  $branchOk = $true
  if ($null -ne $entry.branch -and $null -ne $ns.BranchPct) {
    $branchOk = $ns.BranchPct -ge $entry.branch
  }
  if (-not $lineOk) { $failures.Add("$name line $($ns.LinePct)% < floor $($entry.line)%") }
  if (-not $branchOk) { $failures.Add("$name branch $($ns.BranchPct)% < floor $($entry.branch)%") }
  $status = 'PASS'
  if (-not ($lineOk -and $branchOk)) { $status = 'FAIL' }
  $floorText = "L$($entry.line)"
  if ($null -ne $entry.branch) { $floorText = "L$($entry.line)/B$($entry.branch)" }
  $rows.Add([pscustomobject]@{ Assembly = $name; Line = (Format-Pct $ns.LinePct); Branch = (Format-Pct $ns.BranchPct); Floor = $floorText; Status = $status })
}

foreach ($a in $summary.coverage.assemblies) {
  if ($gatedNames -contains $a.name) { continue }
  $rows.Add([pscustomobject]@{ Assembly = $a.name; Line = (Format-Pct $a.coverage); Branch = (Format-Pct $a.branchcoverage); Floor = 'measure-only'; Status = '-' })
}

Write-Host ""
$sorted = $rows | Sort-Object @{ Expression = { if ($_.Floor -eq 'measure-only') { 1 } else { 0 } } }, Assembly
# Pin a width so the table still renders under a width-0 / non-TTY host (otherwise Out-String wraps to nothing).
($sorted | Format-Table -AutoSize | Out-String -Width 200).TrimEnd() | Write-Host
Write-Host ""

if ($failures.Count -gt 0) {
  Write-Host "Backend coverage gate FAILED:" -ForegroundColor Red
  foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
  exit 1
}
Write-Host "Backend coverage gate passed ($($gatedNames.Count) gated assemblies, $($gatedNsNames.Count) gated namespaces)." -ForegroundColor Green
exit 0
