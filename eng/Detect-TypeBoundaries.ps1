#Requires -Version 7.0
param([string]$Path, [string]$Pattern)
$i = 0
Get-Content $Path -Encoding UTF8 | ForEach-Object {
    $i++
    if ($_ -match $Pattern) { "${i}:$_" }
}