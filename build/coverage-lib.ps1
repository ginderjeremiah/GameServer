#Requires -Version 5.1
<#
.SYNOPSIS
  Shared helpers for the backend coverage gate and floor-reseed scripts. Dot-source, don't run directly.
#>

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
