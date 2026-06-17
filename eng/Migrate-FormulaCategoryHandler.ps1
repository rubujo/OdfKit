#requires -Version 7.0
<#
.SYNOPSIS
    將 DefaultFormulaEvaluator category partial 遷移至獨立 handler 類別。
#>
param(
    [Parameter(Mandatory)]
    [string] $HandlerName,

    [Parameter(Mandatory)]
    [string] $Description,

    [Parameter(Mandatory)]
    [string[]] $SourceFiles,

    [string] $RepoRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
$formulaDir = Join-Path $RepoRoot 'OdfKit/Formula'
$targetPath = Join-Path $formulaDir "$HandlerName.cs"

$methodBlocks = [System.Collections.Generic.List[string]]::new()

foreach ($rel in $SourceFiles) {
    $path = Join-Path $formulaDir $rel
    if (-not (Test-Path $path)) {
        throw "找不到來源檔案：$path"
    }

    $lines = Get-Content -Path $path -Encoding UTF8
    $inMethods = $false
    $buffer = [System.Collections.Generic.List[string]]::new()

    foreach ($line in $lines) {
        if ($line -match '^\s*#region') {
            $inMethods = $true
            continue
        }

        if ($line -match '^\s*#endregion') {
            if ($buffer.Count -gt 0) {
                $methodBlocks.Add(($buffer -join "`n").TrimEnd())
                $buffer.Clear()
            }
            $inMethods = $false
            continue
        }

        if (-not $inMethods) { continue }

        if ($line -match '^\s*private\s+static\s+') {
            if ($buffer.Count -gt 0) {
                $methodBlocks.Add(($buffer -join "`n").TrimEnd())
                $buffer.Clear()
            }
            $line = $line -replace '^\s*private\s+static\s+', '    internal static '
        }

        $buffer.Add($line)
    }
}

$body = ($methodBlocks -join "`n`n")
$needsLinq = $body -match 'Enumerable\.'
$usingLinq = if ($needsLinq) { "using System.Linq;`r`n" } else { '' }

$content = @"
﻿using System;
using System.Collections.Generic;
$usingLinq using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// $Description
/// </summary>
internal static class $HandlerName
{
$body
}
"@

Set-Content -Path $targetPath -Value $content -Encoding UTF8 -NoNewline
Add-Content -Path $targetPath -Value "`n" -Encoding UTF8

foreach ($rel in $SourceFiles) {
    $stubName = [System.IO.Path]::GetFileNameWithoutExtension($rel)
    $stubPath = Join-Path $formulaDir "$stubName.cs"
    $stub = @"
﻿namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    // 實作已遷移至 $HandlerName。
}
"@
    Set-Content -Path $stubPath -Value $stub -Encoding UTF8
    Write-Host "已更新 stub：$rel"
}

Write-Host "已建立 $HandlerName.cs（$($methodBlocks.Count) 個方法）"