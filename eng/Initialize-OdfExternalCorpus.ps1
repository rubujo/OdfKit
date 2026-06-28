[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [ValidateSet("external-corpus", "odfdom-sample-corpus")]
    [string]$Template = "external-corpus",

    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$templateRoot = Join-Path $repoRoot "docs/examples/$Template"
if (-not (Test-Path -LiteralPath $templateRoot)) {
    throw "External corpus template root not found: $templateRoot"
}

$resolvedOutputRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputRoot)
if (-not (Test-Path -LiteralPath $resolvedOutputRoot)) {
    New-Item -ItemType Directory -Path $resolvedOutputRoot | Out-Null
}

$files = @(
    "manifest.json",
    "baseline-exceptions.json"
)

foreach ($fileName in $files) {
    $source = Join-Path $templateRoot $fileName
    $target = Join-Path $resolvedOutputRoot $fileName
    if ((Test-Path -LiteralPath $target) -and -not $Force) {
        Write-Host "Skipping existing file: $target"
        continue
    }

    if ($PSCmdlet.ShouldProcess($target, "Copy external corpus template")) {
        Copy-Item -LiteralPath $source -Destination $target -Force:$Force
        Write-Host "Wrote: $target"
    }
}

Write-Host "Set ODFKIT_PARITY_CORPUS_ROOT to: $resolvedOutputRoot"
