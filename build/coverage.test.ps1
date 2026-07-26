#Requires -Version 5.1
<#
.SYNOPSIS
  Tests for the logic in build/*.ps1 — the coverage aggregation, floor suggestion, ratchet, test-log
  filtering, and the gate's pass/fail/exit-code classification.
.DESCRIPTION
  The failure mode these guard against is silent, not loud: a subtly wrong Get-NamespaceCoverage does
  not crash, it reports a plausible number and the gate keeps passing. So every case asserts on a
  value or an exit code, never merely that a script ran.

  Two styles, matching the two shapes of logic:
    - The pure functions in coverage-lib.ps1 are dot-sourced and called directly with synthetic
      report objects — the same [pscustomobject] shape ConvertFrom-Json yields for a ReportGenerator
      JsonSummary, so a fixture cannot drift from what production code actually consumes.
    - coverage-gate.ps1 keeps its logic in its script body and signals through three distinct exit
      codes, so it is driven end-to-end as a child process against synthetic Summary.json /
      coverage-floors.json files. A child process (rather than a dot-source) is what makes `exit N`
      observable as an exit code instead of terminating this harness.

  Hand-rolled rather than Pester, mirroring .claude/hooks/session-start.test.sh: every build/*.ps1
  declares `#Requires -Version 5.1` and is documented as running on both Windows PowerShell 5.1 and
  pwsh 7, and Pester 7 cannot run under 5.1 (which preinstalls the incompatible 3.4 dialect). A
  dependency-free harness runs wherever the scripts it tests run.

  Assertion text is deliberately ASCII: these files are BOM-less UTF-8, which Windows PowerShell 5.1
  reads as ANSI, so a non-ASCII literal would not survive the round trip on the host 5.1 support exists for.
.OUTPUTS
  Exit 0 = every case passed. Exit 1 = at least one case failed.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'coverage-lib.ps1')

$GateScript = Join-Path $PSScriptRoot 'coverage-gate.ps1'
$SuggestionsScript = Join-Path $PSScriptRoot 'coverage-floor-suggestions.ps1'
$Work = Join-Path ([System.IO.Path]::GetTempPath()) ("coverage-tests-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $Work | Out-Null

# The PowerShell binary of the edition running this harness, so child processes match it rather than a
# hard-coded one. Derived from $PSHOME rather than the current process's executable: under an embedded
# host (the ISE, or anything hosting PowerShell in-process) the process exe is that host's binary, and
# launching it would run the wrong program and fail every child-process case confusingly.
$psBinary = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh' } else { 'powershell.exe' }
$HostExe = Join-Path $PSHOME $psBinary

$script:passed = 0
$script:failed = 0

function Write-Pass($name, $detail) {
  Write-Host "  PASS: $name - $detail"
  $script:passed++
}

function Write-Fail($name, $detail) {
  Write-Host "  FAIL: $name - $detail" -ForegroundColor Red
  $script:failed++
}

# -eq coerces the right operand to the left's type. PowerShell 7 returns $false on a conversion it
# can't make, but 5.1's conversion rules differ, and under $ErrorActionPreference = 'Stop' a throwing
# comparison would abort the whole run instead of failing one case. Catching keeps it reportable.
function Assert-Equal($name, $expected, $actual) {
  try {
    $isEqual = ($expected -eq $actual)
  } catch {
    Write-Fail $name "comparing '$expected' with '$actual' threw: $($_.Exception.Message)"
    return
  }
  if ($isEqual) {
    Write-Pass $name "$expected"
  } else {
    Write-Fail $name "expected '$expected', got '$actual'"
  }
}

function Assert-Null($name, $actual) {
  if ($null -eq $actual) {
    Write-Pass $name 'null'
  } else {
    Write-Fail $name "expected null, got '$actual'"
  }
}

# Both text assertions treat "no output at all" as a failure rather than a vacuous pass: a child
# process that never launched would otherwise satisfy Assert-NotContains and read as green.
function Assert-Contains($name, $expected, $text) {
  if ([string]::IsNullOrEmpty($text)) {
    Write-Fail $name "expected '$expected' but there was no output"
  } elseif ($text.Contains($expected)) {
    Write-Pass $name "output carries '$expected'"
  } else {
    Write-Fail $name "output never carried '$expected'"
  }
}

function Assert-NotContains($name, $unexpected, $text) {
  if ([string]::IsNullOrEmpty($text)) {
    Write-Fail $name "expected output free of '$unexpected' but there was no output"
  } elseif ($text.Contains($unexpected)) {
    Write-Fail $name "output unexpectedly carried '$unexpected'"
  } else {
    Write-Pass $name "output free of '$unexpected'"
  }
}

# --- Fixture builders -------------------------------------------------------------------------
# Property names are lower-cased to match ReportGenerator's JsonSummary exactly; the gate and
# coverage-lib read these names directly, so a rename here would be a silently passing test.

function New-Class($name, $coveredLines, $coverableLines, $coveredBranches, $totalBranches) {
  return [pscustomobject]@{
    name            = $name
    coveredlines    = $coveredLines
    coverablelines  = $coverableLines
    coveredbranches = $coveredBranches
    totalbranches   = $totalBranches
  }
}

function New-Assembly($name, $coverage, $branchCoverage, $classes) {
  $covLines = 0; $covrLines = 0; $covBranches = 0; $totBranches = 0
  foreach ($c in $classes) {
    $covLines += $c.coveredlines; $covrLines += $c.coverablelines
    $covBranches += $c.coveredbranches; $totBranches += $c.totalbranches
  }
  return [pscustomobject]@{
    name              = $name
    coverage          = $coverage
    branchcoverage    = $branchCoverage
    coveredlines      = $covLines
    coverablelines    = $covrLines
    coveredbranches   = $covBranches
    totalbranches     = $totBranches
    classesinassembly = @($classes)
  }
}

# Writes a case's Summary.json / coverage-floors.json. WriteAllText keeps both BOM-less UTF-8 on
# either host.
function New-Fixture($caseName, $assemblies, $floors) {
  $dir = Join-Path $Work $caseName
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  $summaryPath = Join-Path $dir 'Summary.json'
  $floorsPath = Join-Path $dir 'floors.json'
  $summary = [pscustomobject]@{ coverage = [pscustomobject]@{ assemblies = @($assemblies) } }
  [System.IO.File]::WriteAllText($summaryPath, ($summary | ConvertTo-Json -Depth 10))
  [System.IO.File]::WriteAllText($floorsPath, ($floors | ConvertTo-Json -Depth 10))
  return [pscustomobject]@{ SummaryPath = $summaryPath; FloorsPath = $floorsPath }
}

# Both scripts under test take the same -SummaryPath/-FloorsPath pair and signal through their exit
# code, so one runner drives either. A child process rather than a dot-source is what makes their
# `exit N` observable instead of terminating this harness.
function Invoke-BuildScript($scriptPath, $summaryPath, $floorsPath) {
  $output = & $HostExe -NoProfile -File $scriptPath -SummaryPath $summaryPath -FloorsPath $floorsPath 2>&1 |
    Out-String
  return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
}

function Invoke-Gate($caseName, $assemblies, $floors) {
  $f = New-Fixture $caseName $assemblies $floors
  return Invoke-BuildScript $GateScript $f.SummaryPath $f.FloorsPath
}

function Invoke-Suggestions($caseName, $assemblies, $floors) {
  $f = New-Fixture $caseName $assemblies $floors
  return Invoke-BuildScript $SuggestionsScript $f.SummaryPath $f.FloorsPath
}

try {
  Write-Host ''
  Write-Host '=== Get-NamespaceCoverage (gated-namespace aggregation) ==='

  # Sums only the classes under the listed prefixes, across more than one prefix, and recomputes
  # the percentages from the summed counts rather than averaging the per-class figures.
  # In scope: 30/40 lines, 6/10 branches. Out of scope class contributes nothing.
  $assembly = New-Assembly 'Game.Api' 50 40 @(
    (New-Class 'Game.Api.Sockets.SocketHandler' 20 25 4 6),
    (New-Class 'Game.Api.Filters.ErrorStatusFilter' 10 15 2 4),
    (New-Class 'Game.Api.Controllers.PlayerController' 1 100 1 100)
  )
  $ns = Get-NamespaceCoverage $assembly @('Game.Api.Sockets', 'Game.Api.Filters')
  Assert-Equal 'aggregates covered lines across prefixes' 30 $ns.CoveredLines
  Assert-Equal 'aggregates coverable lines across prefixes' 40 $ns.CoverableLines
  Assert-Equal 'aggregates covered branches across prefixes' 6 $ns.CoveredBranches
  Assert-Equal 'aggregates total branches across prefixes' 10 $ns.TotalBranches
  Assert-Equal 'line pct recomputed from the summed counts' 75 $ns.LinePct
  Assert-Equal 'branch pct recomputed from the summed counts' 60 $ns.BranchPct

  # Rounding matches ReportGenerator's own figures: one decimal place. 21/32 = 65.625 -> 65.6.
  $rounding = New-Assembly 'Game.Core' 65 0 @((New-Class 'Game.Core.Battle.Engine' 21 32 0 0))
  $ns = Get-NamespaceCoverage $rounding @('Game.Core.Battle')
  Assert-Equal 'line pct rounded to one decimal' 65.6 $ns.LinePct

  # The prefix is matched with a trailing dot, so a sibling namespace that merely starts with the
  # same characters is out of scope. Without the dot, Game.Api.SocketsLegacy would be gated by the
  # Game.Api.Sockets floor and quietly change what the gate measures.
  $sibling = New-Assembly 'Game.Api' 50 50 @(
    (New-Class 'Game.Api.Sockets.Real' 10 10 0 0),
    (New-Class 'Game.Api.SocketsLegacy.Impostor' 0 90 0 0)
  )
  $ns = Get-NamespaceCoverage $sibling @('Game.Api.Sockets')
  Assert-Equal 'sibling namespace excluded by the trailing dot' 10 $ns.CoverableLines
  Assert-Equal 'sibling namespace excluded (line pct unaffected)' 100 $ns.LinePct

  # A namespace list that matches nothing yields zero counts and null percentages — the state the
  # gate turns into a loud MISSING failure rather than a silent pass.
  $ns = Get-NamespaceCoverage $sibling @('Game.Api.Renamed')
  Assert-Equal 'no matching classes -> zero coverable lines' 0 $ns.CoverableLines
  Assert-Null 'no matching classes -> null line pct' $ns.LinePct
  Assert-Null 'no matching classes -> null branch pct' $ns.BranchPct

  # Zero denominators are guarded independently: a namespace with lines but no branches still
  # reports a line percentage, and only the branch dimension goes null.
  $noBranches = New-Assembly 'Game.Core' 90 0 @((New-Class 'Game.Core.Dto.Thing' 9 10 0 0))
  $ns = Get-NamespaceCoverage $noBranches @('Game.Core.Dto')
  Assert-Equal 'zero branches -> line pct still computed' 90 $ns.LinePct
  Assert-Null 'zero branches -> null branch pct (no divide by zero)' $ns.BranchPct

  Write-Host ''
  Write-Host '=== Get-SuggestedFloor (margin-safe floor seeding) ==='

  # Exact percentage from raw counts, margin subtracted, floored to a whole percent.
  # 967/1000 = 96.7, less 1.0pt margin = 95.7, floored = 95.
  $s = Get-SuggestedFloor 967 1000 1.0
  Assert-Equal 'suggested floor holds back the margin and floors' 95 $s.Suggested

  # Actual keeps full precision — the whole point is to compute from raw covered/coverable counts
  # rather than ReportGenerator's truncated display value.
  $s = Get-SuggestedFloor 9699 10000 1.0
  Assert-Equal 'actual is exact, not truncated to the display value' 96.99 $s.Actual
  Assert-Equal 'a fractional actual still yields a whole-percent floor' 95 $s.Suggested

  # Nothing to cover is not a floor of zero — it is no suggestion at all, so the caller skips
  # the dimension instead of printing a meaningless 0.
  Assert-Null 'zero denominator -> no suggestion' (Get-SuggestedFloor 0 0 1.0)
  Assert-Null 'null denominator -> no suggestion' (Get-SuggestedFloor 0 $null 1.0)

  # A margin larger than actual coverage must clamp at 0 rather than suggesting a negative floor.
  $s = Get-SuggestedFloor 0 100 1.0
  Assert-Equal 'negative suggestion clamped to zero' 0 $s.Suggested

  Write-Host ''
  Write-Host '=== Get-FloorRatchet (a floor only ever rises) ==='

  # The ratchet keeps the higher value in every direction, and labels which way the
  # suggestion fell so a coverage regression is visible rather than silently held.
  $r = Get-FloorRatchet 92 95
  Assert-Equal 'suggestion above current is kept' 95 $r.Kept
  Assert-Equal 'suggestion above current is labelled a raise' 'raise' $r.Label

  $r = Get-FloorRatchet 92 88
  Assert-Equal 'suggestion below current keeps the existing floor' 92 $r.Kept
  Assert-Contains 'suggestion below current is labelled as such' 'below current' $r.Label

  $r = Get-FloorRatchet 92 92
  Assert-Equal 'equal suggestion keeps the floor' 92 $r.Kept
  Assert-Equal 'equal suggestion is labelled no change' 'no change' $r.Label

  Write-Host ''
  Write-Host '=== Get-FloorSectionNames (empty / absent floor sections) ==='

  # An empty or absent section must mean "nothing gated here". A bare @() around
  # .PSObject.Properties.Name yields a one-element array holding $null for *both* — an empty property
  # collection member-enumerates to $null exactly as an absent one does — which made the gate and the
  # reseed script iterate once with a null name and index the report by $null.
  Assert-Equal 'a populated section lists its members' 2 (Get-FloorSectionNames (ConvertFrom-Json '{"Game.Core":{},"Game.Application":{}}')).Count
  Assert-Equal 'an empty section yields no names' 0 (Get-FloorSectionNames (ConvertFrom-Json '{}')).Count
  Assert-Equal 'an absent section yields no names' 0 (Get-FloorSectionNames $null).Count

  # A single-member section must still come back as an array. PowerShell unrolls a one-element array
  # on return, and both callers take .Count on the result — which happens to work on a scalar, but is
  # a quiet dependence rather than the array the helper's contract promises.
  $single = Get-FloorSectionNames (ConvertFrom-Json '{"Game.Core":{}}')
  Assert-Equal 'a single-member section yields one name' 1 $single.Count
  Assert-Equal 'a single-member section stays an array' $true ($single -is [array])

  Write-Host ''
  Write-Host '=== Test-TestLogProvenGreen / Select-TestLogDetailLine ==='

  # A project is skipped only on *proof* it was green. A failing run, and equally a crashed or
  # truncated run that never printed a summary, must both still be reported.
  $green = @('[+12/-0/?0]', 'Test run summary: Passed! - Game.Core.Tests.dll (net10.0)')
  Assert-Equal 'passing summary proves the project green' $true (Test-TestLogProvenGreen $green)

  $red = @('failed BattleTests.Fights_To_Death', 'Test run summary: Failed! - Game.Core.Tests.dll')
  Assert-Equal 'failing summary is not green' $false (Test-TestLogProvenGreen $red)

  $truncated = @('[+3/-0/?0]', 'The active test run was aborted.')
  Assert-Equal 'a run with no summary line is not treated as green' $false (Test-TestLogProvenGreen $truncated)

  Assert-Equal 'an empty log is not treated as green' $false (Test-TestLogProvenGreen @())

  # Filtering keeps the failure detail and drops only the progress spinner and blank padding.
  $noisy = @(
    '[+1/-0/?0]',
    '',
    '  failed BattleTests.Fights_To_Death (12ms)',
    '   ',
    '  Assert.Equal() Failure: Values differ',
    '[+2/-1/?0]',
    '  at Game.Core.Tests.BattleTests.Fights_To_Death()',
    'Test run summary: Failed! - Game.Core.Tests.dll'
  )
  # No @() wrapper: the function guarantees an array, and wrapping a returned array re-wraps it.
  $kept = Select-TestLogDetailLine $noisy
  Assert-Equal 'filter keeps exactly the signal lines' 4 $kept.Count
  $keptText = $kept -join "`n"
  Assert-Contains 'filter keeps the failing test name' 'failed BattleTests.Fights_To_Death' $keptText
  Assert-Contains 'filter keeps the assertion detail' 'Assert.Equal() Failure' $keptText
  Assert-Contains 'filter keeps the stack frame' 'at Game.Core.Tests.BattleTests' $keptText
  Assert-NotContains 'filter drops the progress spinner lines' '[+' $keptText

  # A log whose signal is a single line must still come back as an array rather than unrolling to a
  # bare string, so a caller enumerating the result doesn't iterate the string's characters.
  $oneLine = Select-TestLogDetailLine @('[+1/-0/?0]', '', 'Test run summary: Failed! - Game.Core.Tests.dll')
  Assert-Equal 'a single surviving line stays an array' $true ($oneLine -is [array])
  Assert-Equal 'a single surviving line is the only element' 1 $oneLine.Count

  Write-Host ''
  Write-Host '=== coverage-gate.ps1 (classification and exit codes) ==='

  # A missing summary is its own exit code, distinct from a breach: "you did not run coverage"
  # must not read as "coverage regressed".
  $missing = Invoke-BuildScript $GateScript (Join-Path $Work 'does-not-exist/Summary.json') (Join-Path $PSScriptRoot 'coverage-floors.json')
  Assert-Equal 'missing summary exits 2' 2 $missing.ExitCode
  Assert-Contains 'missing summary says how to produce one' 'Run build/coverage.ps1 first' $missing.Output

  $healthyClasses = @(
    (New-Class 'Game.Api.Sockets.SocketHandler' 95 100 95 100),
    (New-Class 'Game.Api.Controllers.PlayerController' 1 100 0 10)
  )

  # The green path: every gated assembly and namespace clears its floor.
  $pass = Invoke-Gate 'all_pass' @(
    (New-Assembly 'Game.Core' 97 94 @((New-Class 'Game.Core.Battle.Engine' 97 100 94 100))),
    (New-Assembly 'Game.Api' 48 81 $healthyClasses)
  ) ([pscustomobject]@{
    gated = [pscustomobject]@{ 'Game.Core' = [pscustomobject]@{ line = 96; branch = 93 } }
    gatedNamespaces = [pscustomobject]@{
      'Game.Api (sockets)' = [pscustomobject]@{ assembly = 'Game.Api'; namespaces = @('Game.Api.Sockets'); line = 94; branch = 92 }
    }
  })
  Assert-Equal 'floors met exits 0' 0 $pass.ExitCode
  Assert-Contains 'floors met reports the gate passed' 'Backend coverage gate passed' $pass.Output

  # A measure-only assembly is reported but never gated — Game.Api sits at 48% line coverage in
  # the passing run above, which would fail any floor if it had one.
  Assert-Contains 'ungated assembly is reported measure-only' 'measure-only' $pass.Output

  # The assembly backing a gated namespace still gets its own measure-only row: the slice is
  # gated, the assembly as a whole is not. Both rows are expected to be present.
  Assert-Contains 'gated namespace has its own row' 'Game.Api (sockets)' $pass.Output

  # A config with both sections present but empty gates nothing and passes — the regression test for
  # the null-name crash above, at the level a real coverage-floors.json would hit it.
  $allEmpty = Invoke-Gate 'all_empty' @(
    (New-Assembly 'Game.Core' 12 12 @((New-Class 'Game.Core.Battle.Engine' 12 100 12 100)))
  ) ([pscustomobject]@{ gated = [pscustomobject]@{}; gatedNamespaces = [pscustomobject]@{} })
  Assert-Equal 'empty gate sections exit 0' 0 $allEmpty.ExitCode
  Assert-Contains 'empty gate sections report zero gated entries' '0 gated assemblies, 0 gated namespaces' $allEmpty.Output

  # Line breach on a gated assembly.
  $lineBreach = Invoke-Gate 'line_breach' @(
    (New-Assembly 'Game.Core' 90 94 @((New-Class 'Game.Core.Battle.Engine' 90 100 94 100)))
  ) ([pscustomobject]@{
    gated = [pscustomobject]@{ 'Game.Core' = [pscustomobject]@{ line = 96; branch = 93 } }
  })
  Assert-Equal 'gated line breach exits 1' 1 $lineBreach.ExitCode
  Assert-Contains 'gated line breach names the shortfall' 'Game.Core line 90% < floor 96%' $lineBreach.Output

  # Branch breach is caught independently of line coverage, which is comfortably over its floor here.
  $branchBreach = Invoke-Gate 'branch_breach' @(
    (New-Assembly 'Game.Core' 99 80 @((New-Class 'Game.Core.Battle.Engine' 99 100 80 100)))
  ) ([pscustomobject]@{
    gated = [pscustomobject]@{ 'Game.Core' = [pscustomobject]@{ line = 96; branch = 93 } }
  })
  Assert-Equal 'gated branch breach exits 1' 1 $branchBreach.ExitCode
  Assert-Contains 'gated branch breach names the shortfall' 'Game.Core branch 80% < floor 93%' $branchBreach.Output

  # A gated assembly absent from the report fails loudly. A rename that silently dropped an
  # assembly from the gate would otherwise leave it ungated and passing.
  $absent = Invoke-Gate 'absent_assembly' @(
    (New-Assembly 'Game.Application' 99 99 @((New-Class 'Game.Application.Handler' 99 100 99 100)))
  ) ([pscustomobject]@{
    gated = [pscustomobject]@{ 'Game.Core' = [pscustomobject]@{ line = 96; branch = 93 } }
  })
  Assert-Equal 'gated assembly missing from the report exits 1' 1 $absent.ExitCode
  Assert-Contains 'missing gated assembly is called out' 'is gated but missing from the coverage report' $absent.Output
  Assert-Contains 'missing gated assembly shows a MISSING row' 'MISSING' $absent.Output

  # A configured branch floor with no branch data in the report is a failure, not a skip —
  # otherwise losing branch data would silently retire the branch half of the gate.
  $noBranchData = Invoke-Gate 'no_branch_data' @(
    (New-Assembly 'Game.Core' 99 $null @((New-Class 'Game.Core.Battle.Engine' 99 100 0 0)))
  ) ([pscustomobject]@{
    gated = [pscustomobject]@{ 'Game.Core' = [pscustomobject]@{ line = 96; branch = 93 } }
  })
  Assert-Equal 'branch floor with no branch data exits 1' 1 $noBranchData.ExitCode
  Assert-Contains 'missing branch data is called out' 'no branch data in report' $noBranchData.Output

  # A gated namespace below its floor fails even though the assembly it lives in is measure-only:
  # the whole point of the sub-area gate.
  $nsBreach = Invoke-Gate 'namespace_breach' @(
    (New-Assembly 'Game.Api' 48 40 @(
      (New-Class 'Game.Api.Sockets.SocketHandler' 80 100 95 100),
      (New-Class 'Game.Api.Controllers.PlayerController' 1 100 0 10)
    ))
  ) ([pscustomobject]@{
    gated = [pscustomobject]@{}
    gatedNamespaces = [pscustomobject]@{
      'Game.Api (sockets)' = [pscustomobject]@{ assembly = 'Game.Api'; namespaces = @('Game.Api.Sockets'); line = 94; branch = 92 }
    }
  })
  Assert-Equal 'gated namespace breach exits 1' 1 $nsBreach.ExitCode
  Assert-Contains 'gated namespace breach names the shortfall' 'Game.Api (sockets) line 80% < floor 94%' $nsBreach.Output

  # A namespace rename that empties the bucket fails loudly rather than passing on zero classes.
  $nsEmpty = Invoke-Gate 'namespace_empty' @(
    (New-Assembly 'Game.Api' 99 99 @((New-Class 'Game.Api.Renamed.SocketHandler' 99 100 99 100)))
  ) ([pscustomobject]@{
    gated = [pscustomobject]@{}
    gatedNamespaces = [pscustomobject]@{
      'Game.Api (sockets)' = [pscustomobject]@{ assembly = 'Game.Api'; namespaces = @('Game.Api.Sockets'); line = 94; branch = 92 }
    }
  })
  Assert-Equal 'emptied gated namespace exits 1' 1 $nsEmpty.ExitCode
  Assert-Contains 'emptied gated namespace is called out' 'matched no covered classes' $nsEmpty.Output

  # A gated namespace whose backing assembly is absent entirely is a distinct failure from an
  # emptied bucket, and must name the assembly it went looking for.
  $nsNoAssembly = Invoke-Gate 'namespace_no_assembly' @(
    (New-Assembly 'Game.Core' 99 99 @((New-Class 'Game.Core.Battle.Engine' 99 100 99 100)))
  ) ([pscustomobject]@{
    gated = [pscustomobject]@{}
    gatedNamespaces = [pscustomobject]@{
      'Game.Api (sockets)' = [pscustomobject]@{ assembly = 'Game.Api'; namespaces = @('Game.Api.Sockets'); line = 94; branch = 92 }
    }
  })
  Assert-Equal 'gated namespace with a missing assembly exits 1' 1 $nsNoAssembly.ExitCode
  Assert-Contains 'missing backing assembly is named' "its assembly 'Game.Api' is missing" $nsNoAssembly.Output

  Write-Host ''
  Write-Host '=== coverage-floor-suggestions.ps1 (reporting and the ratchet, end to end) ==='

  # One assembly, deliberately straddling its two floors: 97.2% line against a floor of 90 must
  # suggest a raise, while 94.1% branch against a floor of 99 must report below-current and keep 99.
  # Both labels in one run, rendered by the real script rather than asserted on the pure function.
  $straddle = @(
    (New-Assembly 'Game.Core' 97.2 94.1 @((New-Class 'Game.Core.Battle.Engine' 972 1000 941 1000))),
    (New-Assembly 'Game.Api' 61 55 @(
      (New-Class 'Game.Api.Sockets.Handler' 970 1000 950 1000),
      (New-Class 'Game.Api.Controllers.Player' 5 400 2 300)
    ))
  )
  $populatedFloors = [pscustomobject]@{
    gated = [pscustomobject]@{ 'Game.Core' = [pscustomobject]@{ line = 90; branch = 99 } }
    gatedNamespaces = [pscustomobject]@{
      'Game.Api (sockets)' = [pscustomobject]@{ assembly = 'Game.Api'; namespaces = @('Game.Api.Sockets'); line = 80; branch = 80 }
    }
  }
  $sug = Invoke-Suggestions 'suggest_populated' $straddle $populatedFloors
  Assert-Equal 'suggestions run exits 0' 0 $sug.ExitCode
  Assert-Contains 'suggestions print the gated-assemblies header' 'Gated assemblies (margin 1pt)' $sug.Output
  Assert-Contains 'suggestions print the gated-namespaces header' 'Gated namespaces (margin 1pt)' $sug.Output
  Assert-Contains 'a raisable floor renders as a raise' '(raise)' $sug.Output
  Assert-Contains 'a below-current suggestion says the ratchet only rises' 'below current' $sug.Output
  Assert-Contains 'the exact actual percentage is printed, not the display value' '97.2000%' $sug.Output
  Assert-Contains 'the paste-safety hint is printed' "Only the '-> N (raise)' column is ever safe to paste" $sug.Output

  # The header-suppression behaviour this PR changed: a present-but-empty gatedNamespaces must print
  # no namespaces header rather than a header with nothing under it.
  $emptyNs = Invoke-Suggestions 'suggest_empty_ns' $straddle ([pscustomobject]@{
    gated = [pscustomobject]@{ 'Game.Core' = [pscustomobject]@{ line = 90; branch = 99 } }
    gatedNamespaces = [pscustomobject]@{}
  })
  Assert-Equal 'an empty namespaces section still exits 0' 0 $emptyNs.ExitCode
  Assert-Contains 'the gated-assemblies section still prints' 'Gated assemblies' $emptyNs.Output
  Assert-NotContains 'an empty namespaces section prints no header' 'Gated namespaces' $emptyNs.Output

  # A gated assembly absent from the report is a warning here rather than a failure — this script
  # reports, it never gates — but it must still name what it could not find.
  $sugAbsent = Invoke-Suggestions 'suggest_absent' @(
    (New-Assembly 'Game.Application' 99 99 @((New-Class 'Game.Application.Handler' 99 100 99 100)))
  ) ([pscustomobject]@{ gated = [pscustomobject]@{ 'Game.Core' = [pscustomobject]@{ line = 90 } } })
  Assert-Equal 'a missing gated assembly does not fail the reseed script' 0 $sugAbsent.ExitCode
  Assert-Contains 'a missing gated assembly is named' 'missing from coverage report' $sugAbsent.Output

  # A gated namespace whose backing assembly is absent warns and names the assembly.
  $sugNoAsm = Invoke-Suggestions 'suggest_no_assembly' @(
    (New-Assembly 'Game.Core' 99 99 @((New-Class 'Game.Core.Battle.Engine' 99 100 99 100)))
  ) ([pscustomobject]@{
    gated = [pscustomobject]@{}
    gatedNamespaces = [pscustomobject]@{
      'Game.Api (sockets)' = [pscustomobject]@{ assembly = 'Game.Api'; namespaces = @('Game.Api.Sockets'); line = 80 }
    }
  })
  Assert-Equal 'a missing backing assembly does not fail the reseed script' 0 $sugNoAsm.ExitCode
  Assert-Contains 'a missing backing assembly is named' "assembly 'Game.Api' missing from coverage report" $sugNoAsm.Output

  # Same distinct exit code as the gate for "you have not run coverage yet".
  $sugNoSummary = Invoke-BuildScript $SuggestionsScript (Join-Path $Work 'nope/Summary.json') (Join-Path $PSScriptRoot 'coverage-floors.json')
  Assert-Equal 'a missing summary exits 2' 2 $sugNoSummary.ExitCode

  Write-Host ''
  Write-Host "=== $script:passed passed, $script:failed failed ==="
  if ($script:failed -gt 0) { exit 1 }
  exit 0
}
finally {
  Remove-Item -Recurse -Force $Work -ErrorAction SilentlyContinue
}
