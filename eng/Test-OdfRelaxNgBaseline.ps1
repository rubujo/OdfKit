[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$JingJar,

    [string]$Manifest = "tests/fixtures/corpus/manifest.json",
    [string]$CorpusRoot = "tests/fixtures/corpus",
    [string]$SchemaRoot = "tools/OdfSchemaGenerator/schemas",
    [string]$JavaPath = "java",
    [string[]]$ExcludedKinds = @("Formula", "FormulaTemplate", "FlatFormula"),
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

$schemas = @{
    "1.1" = "OpenDocument-schema-v1.1.rng"
    "1.2" = "OpenDocument-v1.2-os-schema.rng"
    "1.3" = "OpenDocument-v1.3-schema.rng"
    "1.4" = "OpenDocument-v1.4-schema.rng"
}
$packageXmlEntries = @("content.xml", "styles.xml", "meta.xml", "settings.xml")

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Invoke-JingValidation {
    param(
        [Parameter(Mandatory = $true)][string]$SchemaPath,
        [Parameter(Mandatory = $true)][string]$XmlPath
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $JavaPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add("-jar")
    $startInfo.ArgumentList.Add($resolvedJingJar)
    $startInfo.ArgumentList.Add("-i")
    $startInfo.ArgumentList.Add($SchemaPath)
    $startInfo.ArgumentList.Add($XmlPath)

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Unable to start Jing."
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            throw "Jing timed out while validating $XmlPath."
        }

        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = (($stdout + [Environment]::NewLine + $stderr).Trim())
        }
    }
    finally {
        $process.Dispose()
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedJingJar = Resolve-RepoPath $JingJar
$resolvedManifest = Resolve-RepoPath $Manifest
$resolvedCorpusRoot = Resolve-RepoPath $CorpusRoot
$resolvedSchemaRoot = Resolve-RepoPath $SchemaRoot
$corpusPrefix = $resolvedCorpusRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

foreach ($requiredPath in @($resolvedJingJar, $resolvedManifest)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required file not found: $requiredPath"
    }
}

$manifestData = Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json
$results = [System.Collections.Generic.List[object]]::new()
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("odfkit-jing-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
try {
    foreach ($fixture in $manifestData.fixtures) {
        if (-not $schemas.ContainsKey([string]$fixture.version)) {
            continue
        }
        if ($ExcludedKinds -contains [string]$fixture.kind) {
            continue
        }

        $fixturePath = [System.IO.Path]::GetFullPath((Join-Path $resolvedCorpusRoot ([string]$fixture.path)))
        if (-not $fixturePath.StartsWith($corpusPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Fixture path escapes the corpus root: $($fixture.path)"
        }
        if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
            throw "Fixture not found: $fixturePath"
        }

        $schemaPath = Join-Path $resolvedSchemaRoot $schemas[[string]$fixture.version]
        if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) {
            throw "Schema not found: $schemaPath"
        }

        $documents = [System.Collections.Generic.List[string]]::new()
        $fixtureTemporaryRoot = $null
        if ([string]$fixture.kind -like "Flat*") {
            $documents.Add($fixturePath)
        }
        else {
            $fixtureTemporaryRoot = Join-Path $temporaryRoot ([Guid]::NewGuid().ToString("N"))
            New-Item -ItemType Directory -Path $fixtureTemporaryRoot | Out-Null
            $archive = [System.IO.Compression.ZipFile]::OpenRead($fixturePath)
            try {
                foreach ($entryName in $packageXmlEntries) {
                    $entry = $archive.GetEntry($entryName)
                    if ($null -eq $entry) {
                        continue
                    }

                    $targetPath = Join-Path $fixtureTemporaryRoot $entryName
                    $source = $entry.Open()
                    $target = [System.IO.File]::Create($targetPath)
                    try {
                        $source.CopyTo($target)
                    }
                    finally {
                        $target.Dispose()
                        $source.Dispose()
                    }
                    $documents.Add($targetPath)
                }
            }
            finally {
                $archive.Dispose()
            }
        }

        if ($documents.Count -eq 0) {
            throw "Package does not contain a RELAX NG document stream: $fixturePath"
        }

        $diagnostics = [System.Collections.Generic.List[string]]::new()
        $isValid = $true
        foreach ($document in $documents) {
            $validation = Invoke-JingValidation -SchemaPath $schemaPath -XmlPath $document
            if ($validation.ExitCode -eq 1) {
                $isValid = $false
                if (-not [string]::IsNullOrWhiteSpace($validation.Output)) {
                    $diagnostics.Add($validation.Output)
                }
            }
            elseif ($validation.ExitCode -ne 0) {
                throw "Jing infrastructure failure (exit code $($validation.ExitCode)): $($validation.Output)"
            }
        }

        $expectedValid = [string]::Equals([string]$fixture.expected, "valid", [StringComparison]::OrdinalIgnoreCase)
        $results.Add([pscustomobject]@{
            id = [string]$fixture.id
            kind = [string]$fixture.kind
            version = [string]$fixture.version
            expected = [string]$fixture.expected
            actual = if ($isValid) { "valid" } else { "invalid" }
            matched = ($isValid -eq $expectedValid)
            diagnostics = @($diagnostics)
        })
    }
}
finally {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$mismatches = @($results | Where-Object { -not $_.matched })
[pscustomobject]@{
    validator = "Jing"
    excludedKinds = @($ExcludedKinds)
    fixtureCount = $results.Count
    passed = $results.Count - $mismatches.Count
    failed = $mismatches.Count
    results = @($results)
} | ConvertTo-Json -Depth 10

if ($mismatches.Count -gt 0) {
    throw "Jing RELAX NG baseline found $($mismatches.Count) classification mismatch(es)."
}
