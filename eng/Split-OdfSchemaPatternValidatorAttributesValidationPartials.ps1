#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$complianceDir = Join-Path $PSScriptRoot '..\OdfKit\Compliance'
$sourcePath = Join-Path $complianceDir 'OdfSchemaPatternValidator.Attributes.Validation.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('System.Collections.Generic')
    [void]$needed.Add('System.Xml.Linq')
    if ($Text -match '\.Linq|\.Any\(|\.All\(|\.Select\(|\.Where\(|\.FirstOrDefault\(') { [void]$needed.Add('System.Linq') }

    $order = @('System', 'System.Collections.Generic', 'System.Linq', 'System.Xml.Linq')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($usingLine in (Get-UsingsForBlock -Text ($BodyLines -join "`n"))) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Compliance;')
    $out.Add('')
    $out.Add('public static partial class OdfSchemaPatternValidator')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$structureBody = $lines[10..241]
$out = [System.Collections.Generic.List[string]]::new()
foreach ($usingLine in (Get-UsingsForBlock -Text ($structureBody -join "`n"))) { $out.Add($usingLine) }
$out.Add('')
$out.Add('namespace OdfKit.Compliance;')
$out.Add('')
$out.Add('public static partial class OdfSchemaPatternValidator')
$out.Add('{')
$out.Add('    #region Attribute Patterns - Validation')
$out.Add('')
foreach ($line in $structureBody) { $out.Add($line) }
$out.Add('')
$out.Add('    #endregion')
$out.Add('}')
Set-Content -Path $sourcePath -Value $out -Encoding UTF8
Write-Host "Core OdfSchemaPatternValidator.Attributes.Validation.cs: $($out.Count) lines"

$matchingBody = $lines[243..419]
Write-PartialFile -Path (Join-Path $complianceDir 'OdfSchemaPatternValidator.Attributes.Validation.Candidates.cs') -RegionName 'Attribute Patterns - Candidate Matching' -BodyLines $matchingBody
Write-Host "  OdfSchemaPatternValidator.Attributes.Validation.Candidates.cs: $($matchingBody.Count) body lines"
Write-Host 'Done.'