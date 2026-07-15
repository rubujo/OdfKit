#Requires -Version 7.0
[CmdletBinding()]
param([switch]$Verify)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projects = @(
    'OdfKit.WebFonts.Abstractions/OdfKit.WebFonts.Abstractions.csproj',
    'OdfKit.WebFonts.Encoding.Legacy/OdfKit.WebFonts.Encoding.Legacy.csproj',
    'OdfKit.WebFonts.Data.SqlServer/OdfKit.WebFonts.Data.SqlServer.csproj',
    'OdfKit.WebFonts.OpenType/OdfKit.WebFonts.OpenType.csproj',
    'OdfKit.WebFonts.Build/OdfKit.WebFonts.Build.csproj',
    'OdfKit.WebFonts.Worker/OdfKit.WebFonts.Worker.csproj',
    'OdfKit.WebFonts.Profiles/OdfKit.WebFonts.Profiles.csproj',
    'OdfKit.WebFonts.Hosting.AspNetCore/OdfKit.WebFonts.Hosting.AspNetCore.csproj',
    'OdfKit.WebFonts.Hosting.SystemWeb/OdfKit.WebFonts.Hosting.SystemWeb.csproj',
    'OdfKit.Extensions.Html.WebFonts/OdfKit.Extensions.Html.WebFonts.csproj'
)
$previousBaseline = $env:ODFKIT_PUBLICAPI_BASELINE
$previousCi = $env:CI
$utf8Bom = [System.Text.UTF8Encoding]::new($true)

try {
    $env:ODFKIT_PUBLICAPI_BASELINE = '1'
    $env:CI = 'true'
    foreach ($relativeProject in $projects) {
        $project = Join-Path $repoRoot $relativeProject
        $projectRoot = Split-Path -Parent $project
        $original = [System.IO.File]::ReadAllText($project)
        & dotnet restore $project -p:NuGetAudit=false --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "還原失敗：$relativeProject"
        }
        $frameworkMatch = [regex]::Match($original, '<TargetFrameworks?>([^<]+)</TargetFrameworks?>')
        if (-not $frameworkMatch.Success) {
            throw "找不到 TargetFramework：$relativeProject"
        }

        $frameworks = $frameworkMatch.Groups[1].Value -split ';'
        try {
            foreach ($framework in $frameworks) {
                $apiRoot = Join-Path $projectRoot "PublicAPI/$framework"
                New-Item -ItemType Directory -Force -Path $apiRoot | Out-Null
                $shipped = Join-Path $apiRoot 'PublicAPI.Shipped.txt'
                $unshipped = Join-Path $apiRoot 'PublicAPI.Unshipped.txt'
                [System.IO.File]::WriteAllText($shipped, "#nullable enable`r`n", $utf8Bom)
                [System.IO.File]::WriteAllText($unshipped, "#nullable enable`r`n", $utf8Bom)

                $singleFramework = "<TargetFramework>$framework</TargetFramework>"
                $withoutFramework = $original.Remove($frameworkMatch.Index, $frameworkMatch.Length)
                $patched = $withoutFramework.Insert($frameworkMatch.Index, $singleFramework)
                [System.IO.File]::WriteAllText($project, $patched, $utf8Bom)
                Write-Host "產生 $relativeProject / $framework Public API 基線…"
                & dotnet format analyzers $project --diagnostics RS0016 --severity warn --include-generated --verbosity minimal --no-restore
                if ($LASTEXITCODE -ne 0) {
                    throw "Public API code fix 失敗：$relativeProject / $framework"
                }

                if ((Get-Item $unshipped).Length -le 20) {
                    throw "Public API 基線是空的：$relativeProject / $framework"
                }

                [System.IO.File]::WriteAllText($project, $original, $utf8Bom)
            }
        }
        finally {
            [System.IO.File]::WriteAllText($project, $original, $utf8Bom)
        }
    }
}
finally {
    $env:ODFKIT_PUBLICAPI_BASELINE = $previousBaseline
    $env:CI = $previousCi
}

if ($Verify) {
    try {
        $env:CI = 'true'
        foreach ($relativeProject in $projects) {
            & dotnet build (Join-Path $repoRoot $relativeProject) -c Release --nologo /p:RunAnalyzersDuringBuild=true
            if ($LASTEXITCODE -ne 0) {
                throw "Public API 驗證失敗：$relativeProject"
            }
        }
    }
    finally {
        $env:CI = $previousCi
    }
}

Write-Host 'PASS：WebFont Package Public API 基線完成。'
