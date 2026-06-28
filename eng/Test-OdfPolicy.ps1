[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0"
)

$ErrorActionPreference = "Stop"

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "命令失敗（exit code $LASTEXITCODE）：$FilePath $($ArgumentList -join ' ')"
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    Invoke-NativeCommand "dotnet" @("restore")
    Invoke-NativeCommand "dotnet" @("build", "OdfKit.Tests/OdfKit.Tests.csproj", "-c", $Configuration, "--framework", $Framework, "--no-restore")
    Invoke-NativeCommand "dotnet" @(
        "test",
        "OdfKit.Tests/OdfKit.Tests.csproj",
        "-c",
        $Configuration,
        "--framework",
        $Framework,
        "--no-build",
        "--filter",
        "Category=Policy"
    )
}
finally {
    Pop-Location
}
