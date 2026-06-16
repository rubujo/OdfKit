#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfTypedDomCoverage.PropertyTypes.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'OdfCompliance|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfElement') { [void]$needed.Add('OdfKit.DOM') }
    if ($Text -match 'OdfLength|OdfStyle|OdfDraw|OdfText|OdfTable|OdfLine|OdfFont|OdfMediaType') { [void]$needed.Add('OdfKit.Styles') }

    $order = @('System', 'OdfKit.Compliance', 'OdfKit.DOM', 'OdfKit.Styles')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    $out.Add('public static partial class OdfTypedDomCoverage')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$primitiveBody = $lines[22..245]
$extendedBody = $lines[247..620]

$primitiveMethod = @(
    '    private static string? TryResolvePrimitivePropertyTypeName(Type resolvedType)'
    '    {'
) + ($primitiveBody | ForEach-Object { "        $_" }) + @(
    '        return null;'
    '    }'
)

$extendedMethod = @(
    '    private static string? TryResolveExtendedPropertyTypeName(Type resolvedType)'
    '    {'
) + ($extendedBody | ForEach-Object { "        $_" }) + @(
    '        return null;'
    '    }'
)

$coreBody = @(
    '    private static string GetPropertyTypeName(Type type)'
    '    {'
    '        Type? nullableType = Nullable.GetUnderlyingType(type);'
    '        Type resolvedType = nullableType ?? type;'
    '        if (resolvedType.IsGenericType &&'
    '            resolvedType.GetGenericTypeDefinition() == typeof(IEnumerable<>) &&'
    '            typeof(OdfElement).IsAssignableFrom(resolvedType.GetGenericArguments()[0]))'
    '        {'
    '            return "childElementCollection";'
    '        }'
    ''
    '        return TryResolvePrimitivePropertyTypeName(resolvedType)'
    '            ?? TryResolveExtendedPropertyTypeName(resolvedType)'
    '            ?? resolvedType.FullName ?? resolvedType.Name;'
    '    }'
    ''
) + $lines[625..636]

Write-PartialFile -Path $sourcePath -RegionName 'Property Type Resolution' -BodyLines $coreBody
Write-PartialFile -Path (Join-Path $domDir 'OdfTypedDomCoverage.PropertyTypes.Primitive.cs') -RegionName 'Primitive Property Types' -BodyLines $primitiveMethod
Write-PartialFile -Path (Join-Path $domDir 'OdfTypedDomCoverage.PropertyTypes.Extended.cs') -RegionName 'Extended Property Types' -BodyLines $extendedMethod

Write-Host '  OdfTypedDomCoverage.PropertyTypes.cs (core)'
Write-Host '  OdfTypedDomCoverage.PropertyTypes.Primitive.cs'
Write-Host '  OdfTypedDomCoverage.PropertyTypes.Extended.cs'
Write-Host 'Done.'