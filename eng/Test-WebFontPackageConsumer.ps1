#Requires -Version 7.0
<#
.SYNOPSIS
從本次 nupkg 安裝 WebFont library 與 CLI，並以真實 CNS 字型離線產字。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$FontPath,
    [Parameter(Mandatory)][string]$SourceSha256,
    [string]$Destination = "artifacts/webfont-package-consumer",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$destinationPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $Destination))
$repoPrefix = [IO.Path]::GetFullPath($repoRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $destinationPath.StartsWith($repoPrefix, $comparison)) {
    throw "Destination 必須位於方案目錄內。"
}

$resolvedFontPath = (Resolve-Path -LiteralPath $FontPath).Path
$actualSourceSha256 = (Get-FileHash -LiteralPath $resolvedFontPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($SourceSha256 -notmatch "^[0-9a-fA-F]{64}$" -or $actualSourceSha256 -ne $SourceSha256.ToLowerInvariant()) {
    throw "WebFont package consumer 的來源字型 SHA-256 不符合。"
}

$packageVersion = & (Join-Path $PSScriptRoot "Get-PackageVersion.ps1")
$packageRoot = Join-Path $destinationPath "packages"
$consumerRoot = Join-Path $destinationPath "consumer"
$toolRoot = Join-Path $destinationPath "tool"
$cliOutputRoot = Join-Path $destinationPath "cli-output"
$cliReproRoot = Join-Path $destinationPath "cli-output-repro"
$contentRoot = Join-Path $destinationPath "content"
$nugetPackages = Join-Path $destinationPath "nuget-cache"
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
New-Item -ItemType Directory -Path $contentRoot -Force | Out-Null
Set-Content -LiteralPath (Join-Path $contentRoot "corpus.txt") -Value "A𠆩" -Encoding utf8NoBOM

$projects = @(
    "OdfKit/OdfKit.csproj",
    "OdfKit.WebFonts.Abstractions/OdfKit.WebFonts.Abstractions.csproj",
    "OdfKit.WebFonts.Encoding.Legacy/OdfKit.WebFonts.Encoding.Legacy.csproj",
    "OdfKit.WebFonts.Profiles/OdfKit.WebFonts.Profiles.csproj",
    "OdfKit.WebFonts.OpenType/OdfKit.WebFonts.OpenType.csproj",
    "OdfKit.WebFonts.Build/OdfKit.WebFonts.Build.csproj"
)
foreach ($project in $projects) {
    dotnet pack (Join-Path $repoRoot $project) -c $Configuration --nologo -o $packageRoot
    if ($LASTEXITCODE -ne 0) { throw "WebFont package consumer 封裝失敗：$project" }
}

$previousNugetPackages = $env:NUGET_PACKAGES
try {
    $env:NUGET_PACKAGES = $nugetPackages
    Remove-Item -LiteralPath $consumerRoot -Recurse -Force -ErrorAction SilentlyContinue
    dotnet new console -n WebFontPackageConsumer -o $consumerRoot -f net10.0 --force
    if ($LASTEXITCODE -ne 0) { throw "WebFont package consumer 建立失敗。" }
    $nugetConfigPath = Join-Path $destinationPath "NuGet.consumer.config"
    $escapedPackageRoot = [Security.SecurityElement]::Escape($packageRoot)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="odfkit-local" value="$escapedPackageRoot" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfigPath -Encoding utf8NoBOM
    dotnet add $consumerRoot package OdfKit.WebFonts.OpenType `
        --version $packageVersion --no-restore
    if ($LASTEXITCODE -ne 0) { throw "WebFont package consumer 安裝失敗。" }
    dotnet restore $consumerRoot --nologo --configfile $nugetConfigPath
    if ($LASTEXITCODE -ne 0) { throw "WebFont package consumer 還原失敗。" }

    @'
using OdfKit.WebFonts;
using OdfKit.WebFonts.OpenType;

if (args.Length != 3)
{
    return 2;
}

string sourcePath = Path.GetFullPath(args[0]);
string sourceSha256 = args[1];
string outputPath = Path.GetFullPath(args[2]);
var options = new ManagedOpenTypeWebFontEngineOptions
{
    MaxSourceBytes = 256L * 1024 * 1024,
    MaxOutputBytes = 64L * 1024 * 1024,
    MaxUnicodeScalars = 1024
};
options.FontSources["package-consumer"] = sourcePath;
var engine = new ManagedOpenTypeWebFontSubsetEngine(options);
WebFontTextSequence sequence = WebFontTextSequence.Create("A𠆩");
WebFontManifest manifest = await engine.GenerateAsync(
    new WebFontSubsetRequest
    {
        Face = new WebFontFaceIdentity
        {
            FontSourceId = "package-consumer",
            SourceSha256 = sourceSha256
        },
        ProfileId = "package-consumer-v1",
        FontFamily = "OdfKit Package Consumer",
        Sequences = [sequence],
        Formats = [WebFontFormat.TrueType, WebFontFormat.Woff, WebFontFormat.Woff2]
    },
    outputPath);
if (manifest.Assets.Count != 3)
{
    throw new InvalidDataException("The package consumer did not generate all formats.");
}
foreach (WebFontAsset asset in manifest.Assets)
{
    await using FileStream stream = File.OpenRead(Path.Combine(outputPath, asset.Sha256, asset.FileName));
    ManagedOpenTypeWebFontVerifier.VerifyContainsSequences(stream, asset.Format, [sequence]);
}
Console.WriteLine("PASS: clean nupkg library consumer generated and verified three formats.");
return 0;
'@ | Set-Content -LiteralPath (Join-Path $consumerRoot "Program.cs") -Encoding utf8NoBOM

    dotnet build $consumerRoot -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "WebFont package consumer 建置失敗。" }
    $libraryOutput = Join-Path $destinationPath "library-output"
    dotnet run --project $consumerRoot -c $Configuration --no-build --no-restore -- `
        $resolvedFontPath $actualSourceSha256 $libraryOutput
    if ($LASTEXITCODE -ne 0) { throw "WebFont package consumer 產字失敗。" }

    Remove-Item -LiteralPath $toolRoot -Recurse -Force -ErrorAction SilentlyContinue
    dotnet tool install OdfKit.WebFonts.Build --tool-path $toolRoot `
        --version $packageVersion --configfile $nugetConfigPath
    if ($LASTEXITCODE -ne 0) { throw "WebFont CLI nupkg 安裝失敗。" }
    $toolCommand = Get-ChildItem -LiteralPath $toolRoot -File |
        Where-Object { $_.BaseName -eq "odfkit-webfonts" -or $_.Name -eq "odfkit-webfonts" } |
        Select-Object -First 1
    if ($null -eq $toolCommand) { throw "WebFont CLI nupkg 缺少命令入口。" }
    foreach ($output in @($cliOutputRoot, $cliReproRoot)) {
        Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue
        & $toolCommand.FullName build `
            --font $resolvedFontPath `
            --content-root $contentRoot `
            --content-extensions .txt `
            --output $output `
            --profile package-cli-v1 `
            --family "OdfKit Package CLI" `
            --formats woff2,woff,ttf
        if ($LASTEXITCODE -ne 0) { throw "WebFont CLI nupkg 產字失敗。" }
    }

    $first = Get-Content -LiteralPath (Join-Path $cliOutputRoot "webfonts.json") -Raw | ConvertFrom-Json
    $second = Get-Content -LiteralPath (Join-Path $cliReproRoot "webfonts.json") -Raw | ConvertFrom-Json
    $firstHashes = @($first.assets | Sort-Object format | ForEach-Object sha256) -join ","
    $secondHashes = @($second.assets | Sort-Object format | ForEach-Object sha256) -join ","
    if (@($first.assets).Count -ne 3 -or $firstHashes -ne $secondHashes) {
        throw "WebFont CLI nupkg 未產生三格式或結果不具確定性。"
    }
}
finally {
    $env:NUGET_PACKAGES = $previousNugetPackages
}

Write-Host "PASS：同批 0.0.1 nupkg 的 library 與 CLI clean consumer 真實產字成功。"
