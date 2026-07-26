#Requires -Version 5.1
<#
.SYNOPSIS
  Shared pure helpers for the backend coverage gate, floor-reseed and coverage-run scripts.
  Dot-source, don't run directly.
.DESCRIPTION
  Everything here is a pure function of its arguments — no disk, no host state — so
  build/coverage.test.ps1 can drive it with synthetic fixtures. The scripts that own side effects
  (reading the report, enumerating logs, writing to the host, choosing an exit code) keep those in
  their own bodies and call in here for the arithmetic and the filtering decisions.
#>

# Member names of a coverage-floors.json section ("gated" / "gatedNamespaces"), as a genuinely empty
# array when the section is absent or has no members. @($section.PSObject.Properties.Name) alone
# yields a *one-element array holding $null* in both those cases, so callers would iterate once with a
# null name and then index the report by $null.
function Get-FloorSectionNames($section) {
  return @($section.PSObject.Properties.Name | Where-Object { $null -ne $_ })
}

# Aggregate line/branch coverage for the classes of an assembly whose names fall under any of the
# given namespace prefixes. Returns the summed counts and the recomputed percentages (rounded to
# one decimal to match ReportGenerator's own figures); a percentage is $null when its denominator is
# zero (nothing to cover), so callers can skip that dimension rather than dividing by zero.
function Get-NamespaceCoverage($assembly, $namespaces) {
  $coveredLines = 0; $coverableLines = 0; $coveredBranches = 0; $totalBranches = 0
  foreach ($c in $assembly.classesinassembly) {
    $inScope = $false
    foreach ($ns in $namespaces) {
      if ($c.name.StartsWith("$ns.")) { $inScope = $true; break }
    }
    if (-not $inScope) { continue }
    $coveredLines += $c.coveredlines
    $coverableLines += $c.coverablelines
    $coveredBranches += $c.coveredbranches
    $totalBranches += $c.totalbranches
  }
  $linePct = $null
  if ($coverableLines -gt 0) { $linePct = [math]::Round(100.0 * $coveredLines / $coverableLines, 1) }
  $branchPct = $null
  if ($totalBranches -gt 0) { $branchPct = [math]::Round(100.0 * $coveredBranches / $totalBranches, 1) }
  return [pscustomobject]@{
    CoveredLines = $coveredLines; CoverableLines = $coverableLines
    CoveredBranches = $coveredBranches; TotalBranches = $totalBranches
    LinePct = $linePct; BranchPct = $branchPct
  }
}

# Exact percentage from raw counts, margin subtracted, floored to a whole percent — never a
# fractional floor, matching the style already hand-written into coverage-floors.json. Returns $null
# when there is nothing to cover, so callers skip that dimension instead of suggesting a floor of 0.
function Get-SuggestedFloor($covered, $total, $marginPoints) {
  if ($null -eq $total -or $total -eq 0) { return $null }
  $actual = 100.0 * $covered / $total
  $suggested = [math]::Floor($actual - $marginPoints)
  if ($suggested -lt 0) { $suggested = 0 }
  return [pscustomobject]@{ Actual = $actual; Suggested = $suggested }
}

# The ratchet rule: a floor only ever rises, so the value to keep is the higher of the current floor
# and the suggestion. Label says which way the suggestion fell, so a real coverage regression shows
# up as a visible "below current" line rather than being silently held at the current floor.
function Get-FloorRatchet($currentFloor, $suggested) {
  $label = 'no change'
  if ($suggested -gt $currentFloor) {
    $label = 'raise'
  } elseif ($suggested -lt $currentFloor) {
    $label = 'below current — keep existing, ratchet only rises'
  }
  return [pscustomobject]@{ Kept = [math]::Max([double]$currentFloor, [double]$suggested); Label = $label }
}

# A test project's log proves the project green only by carrying the passing run summary. Anything
# else — a failure, or a crashed/truncated run that never printed a summary — must still be reported,
# so absence of proof is not treated as proof of absence.
function Test-TestLogProvenGreen($lines) {
  foreach ($line in $lines) {
    if ($line -like 'Test run summary: Passed!*') { return $true }
  }
  return $false
}

# The signal in a test log is the `failed <test>` blocks and the run summary; the periodic
# `[+n/-n/?n]` progress lines and blank padding are noise that would bury it.
function Select-TestLogDetailLine($lines) {
  return @($lines | Where-Object { $_ -notmatch '^\[\+' -and -not [string]::IsNullOrWhiteSpace($_) })
}
