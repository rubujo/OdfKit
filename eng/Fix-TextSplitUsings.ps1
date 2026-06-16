#Requires -Version 7.0
$textDir = Join-Path $PSScriptRoot '..\OdfKit\Text'
Get-ChildItem $textDir -Filter '*.cs' | ForEach-Object {
    $lines = [System.Collections.Generic.List[string]]@(Get-Content $_.FullName -Encoding UTF8)
    $text = $lines -join "`n"
    if ($text -notmatch 'OdfNode|OdfNodeFactory|OdfNodeType') { return }
    if ($text -match 'using OdfKit\.DOM;') { return }
    $insertAt = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^namespace ') {
            $insertAt = $i
            break
        }
    }
    $lines.Insert($insertAt, '')
    $lines.Insert($insertAt, 'using OdfKit.DOM;')
    Set-Content -Path $_.FullName -Value $lines -Encoding UTF8
    Write-Host "Fixed $($_.Name)"
}