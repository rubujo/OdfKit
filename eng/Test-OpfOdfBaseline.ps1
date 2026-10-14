[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ValidatorJar,
    [string]$Manifest = "tests/fixtures/corpus/manifest.json",
    [string]$CorpusRoot = "tests/fixtures/corpus",
    [string]$Exceptions = "docs/opf-baseline-exceptions.json",
    [string]$ReportPath = "artifacts/opf-baseline.json",
    [string]$JavaPath = "java",
    [ValidateRange(1, 300)][int]$TimeoutSeconds = 30,
    [switch]$RequireAllExceptions
)

$ErrorActionPreference = "Stop"
$timer = [Diagnostics.Stopwatch]::StartNew()
$repoRoot = Split-Path -Parent $PSScriptRoot
function Resolve-RepoPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}
$jar = Resolve-RepoPath $ValidatorJar
$tool = (Get-Content (Join-Path $repoRoot "eng/external-tools.json") -Raw | ConvertFrom-Json).opfOdfValidator
if ((Get-FileHash -LiteralPath $jar).Hash -ne $tool.sha256) { throw "OPF JAR SHA-256 mismatch." }
$root = Resolve-RepoPath $CorpusRoot
$prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$data = Get-Content -LiteralPath (Resolve-RepoPath $Manifest) -Raw | ConvertFrom-Json
$exceptionData = Get-Content -LiteralPath (Resolve-RepoPath $Exceptions) -Raw | ConvertFrom-Json
if ($exceptionData.validator -ne 'opf-odf-validator' -or $exceptionData.validatorVersion -ne $tool.version) {
    throw "OPF exceptions require review for this validator/version."
}
$results = [Collections.Generic.List[object]]::new()
$usedExceptions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$fixtureIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($fixture in $data.fixtures) {
    if ([string]::IsNullOrWhiteSpace($fixture.id) -or -not $fixtureIds.Add($fixture.id)) {
        throw "Missing or duplicate OPF fixture id."
    }
    $path = [IO.Path]::GetFullPath((Join-Path $root $fixture.path))
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Fixture escapes corpus root: $path" }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing fixture: $path" }
    if ($fixture.sha256 -and (Get-FileHash -LiteralPath $path).Hash -ne $fixture.sha256) { throw "Fixture SHA-256 mismatch: $path" }
    if ($fixture.expected -notin @('valid', 'invalid')) { throw "Unknown expected classification: $($fixture.id)" }
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $JavaPath
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in @('-jar', $jar, '-o', 'JSON')) { $info.ArgumentList.Add($argument) }
    if ($fixture.profile -like '*Extended' -or $fixture.version -in @('1.0', '1.1')) { $info.ArgumentList.Add('-e') }
    $info.ArgumentList.Add($path)
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (-not $process.Start()) { throw "Could not start OPF." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "OPF timed out: $($fixture.id)"
        }
        $output = $stdout.GetAwaiter().GetResult() + "`n" + $stderr.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    }
    finally { $process.Dispose() }
    # OPF 混合輸出 console 訊息與 JSON；必須有完整報告，且與結束碼一致。
    $start = $output.IndexOf('{')
    $end = $output.LastIndexOf('}')
    if ($exitCode -notin @(0, 1) -or $start -lt 0 -or $end -le $start) {
        throw "OPF infrastructure failure or missing report for $($fixture.id) (exit $exitCode): $output"
    }
    $report = $output.Substring($start, $end - $start + 1) | ConvertFrom-Json
    if ($report.is_valid -isnot [bool] -or $report.validation_results.Count -eq 0 -or
        $report.is_valid -ne ($exitCode -eq 0)) { throw "Inconsistent OPF report: $($fixture.id)" }
    if (@($report.validation_results.checks | Where-Object { $_.message.severity -eq 'FATAL' }).Count -gt 0) {
        throw "OPF fatal failure for $($fixture.id): $output"
    }
    $actual = if ($report.is_valid) { 'valid' } else { 'invalid' }
    $matches = $actual -eq $fixture.expected
    $errorSignatures = @($report.validation_results.checks | Where-Object { $_.message.severity -in @('ERROR', 'FATAL') } |
        ForEach-Object { $_.message.id + ':' + [regex]::Replace($_.message.title, 'line \d+ and column \d+', 'line * and column *') } |
        Sort-Object -Unique)
    $exception = @($exceptionData.exceptions | Where-Object { $_.fixtureId -ceq $fixture.id })
    if ($exception.Count -gt 1) { throw "Duplicate OPF exception: $($fixture.id)" }
    $documented = $false
    if ($exception.Count -eq 1) {
        $entry = $exception[0]
        $usedExceptions.Add($entry.fixtureId) | Out-Null
        $documented = -not $matches -and $entry.expected -eq $fixture.expected -and
            $entry.actual -eq $actual -and
            ($errorSignatures -join "`n") -ceq (@($entry.errorSignatures | Sort-Object -Unique) -join "`n") -and
            -not [string]::IsNullOrWhiteSpace($entry.reason)
        if (-not $documented) { throw "OPF exception changed or is stale: $($fixture.id)" }
    }
    $results.Add([pscustomobject]@{ id = $fixture.id; version = $fixture.version; kind = $fixture.kind;
        expected = $fixture.expected; actual = $actual; matched = $matches; documentedException = $documented;
        diagnostics = $report.validation_results })
}
if ($results.Count -eq 0) { throw "OPF baseline cannot be empty." }
if ($RequireAllExceptions -and $usedExceptions.Count -ne $exceptionData.exceptions.Count) { throw "Unused OPF exceptions require review." }
$failures = @($results | Where-Object { -not $_.matched -and -not $_.documentedException })
$resolvedReport = Resolve-RepoPath $ReportPath
New-Item -ItemType Directory -Force (Split-Path -Parent $resolvedReport) | Out-Null
[pscustomobject]@{ validator = 'opf-odf-validator'; version = $tool.version; fixtureCount = $results.Count;
    elapsedSeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 2);
    matched = @($results | Where-Object matched).Count; documentedExceptions = $usedExceptions.Count;
    failed = $failures.Count; results = @($results) } | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $resolvedReport -Encoding utf8
Write-Host "OPF baseline: $($results.Count) fixtures, $($usedExceptions.Count) documented exceptions, $($failures.Count) mismatches. Report: $resolvedReport"
if ($failures.Count -gt 0) { throw "OPF classification mismatch: $($failures.id -join ', ')" }
