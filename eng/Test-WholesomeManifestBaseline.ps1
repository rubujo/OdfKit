[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$JingJar,

    [Parameter(Mandatory = $true)]
    [string]$LibreOfficeManifestSchema,

    [string]$SourceDocument = "tests/fixtures/corpus/generated/minimal-text.odt",
    [string]$JavaPath = "java",
    [string]$Framework = "net10.0",
    [string]$Configuration = "Release",
    [int]$TimeoutSeconds = 180,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Unable to start $Description."
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            throw "$Description timed out."
        }

        $output = (($stdoutTask.GetAwaiter().GetResult() +
            [Environment]::NewLine +
            $stderrTask.GetAwaiter().GetResult()).Trim())
        if ($process.ExitCode -ne 0) {
            throw "$Description failed with exit code $($process.ExitCode): $output"
        }

        return $output
    }
    finally {
        $process.Dispose()
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedJingJar = Resolve-RepoPath $JingJar
$resolvedSchema = Resolve-RepoPath $LibreOfficeManifestSchema
$resolvedSourceDocument = Resolve-RepoPath $SourceDocument
$cliProject = Join-Path $repoRoot "tools/OdfKit.Cli/OdfKit.Cli.csproj"

foreach ($requiredPath in @($resolvedJingJar, $resolvedSchema, $resolvedSourceDocument, $cliProject)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required file not found: $requiredPath"
    }
}

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("odfkit-wholesome-jing-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
try {
    $encryptedDocument = Join-Path $temporaryRoot "wholesome.odt"
    $manifestPath = Join-Path $temporaryRoot "manifest.xml"
    $dotnetArguments = @(
        "run",
        "--project", $cliProject,
        "--framework", $Framework,
        "--configuration", $Configuration
    )
    if ($NoBuild) {
        $dotnetArguments += @("--no-build", "--no-restore")
    }
    $dotnetArguments += @(
        "--",
        "sanitize",
        $resolvedSourceDocument,
        $encryptedDocument,
        "--output-password", "OdfKit-Jing-Wholesome-Baseline",
        "--encryption", "aes256-gcm"
    )

    [void](Invoke-CheckedProcess -FileName "dotnet" -Arguments $dotnetArguments -Description "wholesome package generation")

    $archive = [System.IO.Compression.ZipFile]::OpenRead($encryptedDocument)
    try {
        $manifestEntry = $archive.GetEntry("META-INF/manifest.xml")
        if ($null -eq $manifestEntry) {
            throw "Generated wholesome package does not contain META-INF/manifest.xml."
        }

        $source = $manifestEntry.Open()
        $target = [System.IO.File]::Create($manifestPath)
        try {
            $source.CopyTo($target)
        }
        finally {
            $target.Dispose()
            $source.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }

    $jingArguments = @("-jar", $resolvedJingJar, "-i", $resolvedSchema, $manifestPath)
    $jingOutput = Invoke-CheckedProcess -FileName $JavaPath -Arguments $jingArguments -Description "wholesome manifest Jing validation"

    [pscustomobject]@{
        validator = "Jing"
        schema = [System.IO.Path]::GetFileName($resolvedSchema)
        packageShape = "wholesome"
        encryption = "AES-256-GCM"
        keyDerivation = "Argon2id"
        passed = $true
        diagnostics = $jingOutput
    } | ConvertTo-Json -Depth 4
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot -PathType Container) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
