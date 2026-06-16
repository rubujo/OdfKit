#Requires -Version 7.0
param([int]$MinLines = 400, [int]$Top = 20)
$root = Join-Path $PSScriptRoot '..\OdfKit'
Get-ChildItem -Path $root -Recurse -Filter '*.cs' |
    ForEach-Object {
        $lineCount = (Get-Content -LiteralPath $_.FullName).Count
        if ($lineCount -ge $MinLines) {
            [PSCustomObject]@{
                Lines = $lineCount
                Path  = $_.FullName.Substring($root.Length + 1)
            }
        }
    } |
    Sort-Object Lines -Descending |
    Select-Object -First $Top |
    Format-Table -AutoSize