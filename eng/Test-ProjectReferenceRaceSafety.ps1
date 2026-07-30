#Requires -Version 7.0
<#
.SYNOPSIS
    掃描 ProjectReference 上會造成 MSBuild 建置競態的 metadata 組合。
.DESCRIPTION
    MSBuild 以（專案路徑, 全域屬性）判定專案實例；同一個被參考專案若被不同消費端以不同的
    全域屬性要求建置，就會被建置兩次並並行寫入同一個 obj 路徑，隨機產生 CS2012（DLL 佔用）
    或 deps.json 的 IOException。
    依據 https://learn.microsoft.com/en-us/visualstudio/msbuild/fix-intermittent-build-failures
    需檢查的 metadata 為 SetTargetFramework、SetConfiguration、SetPlatform、
    AdditionalProperties、RemoveGlobalProperties、GlobalPropertiesToRemove。

    本閘門阻擋三種機械可判定的情形：
    1. 單一 TFM 的被參考專案帶 SetTargetFramework——SDK 本來不會傳 TargetFramework 全域屬性，
       明確指定必然與其他純參考的消費端分裂成兩個實例（多 TFM 專案則相反：SDK 經 nearest-TFM
       協商後本來就會傳，明確釘住不會分裂，且可避免連帶編譯其他 TFM）。
    2. 明確宣告的 GlobalPropertiesToRemove 未包含 PublishAot——會覆蓋
       Directory.Build.props 的預設值，使該消費端與其他消費端不一致。
    3. 同一個被參考專案在不同 ProjectReference 之間的 SetConfiguration、SetPlatform、
       AdditionalProperties、RemoveGlobalProperties 不一致——這些沒有 SDK 協商出的等價值，
       不一致即代表分裂。
    4. 有 Directory.Build.props 未匯入 eng\Shared\ProjectReferenceDefaults.props——巢狀檔案
       會遮蔽上層，其下專案將靜默失去預設 metadata 而與其他消費端不一致。
.PARAMETER Root
    倉庫根目錄，預設為本腳本的上一層。
#>
param(
    [string]$Root = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)

# 不一致即代表全域屬性分裂的 metadata（SetTargetFramework 另由規則 1 單獨處理）。
$splitMetadata = @('SetConfiguration', 'SetPlatform', 'AdditionalProperties', 'RemoveGlobalProperties')

$issues = [System.Collections.Generic.List[object]]::new()
$references = [System.Collections.Generic.List[object]]::new()
$targetFrameworkCache = @{}

function Get-MetadataValue {
    param([System.Xml.XmlElement]$Element, [string]$Name)

    $attribute = $Element.GetAttribute($Name)
    if (-not [string]::IsNullOrWhiteSpace($attribute)) { return $attribute.Trim() }

    foreach ($child in $Element.ChildNodes) {
        if ($child.NodeType -eq 'Element' -and $child.LocalName -eq $Name) {
            return $child.InnerText.Trim()
        }
    }

    return $null
}

function Test-SingleTargetFramework {
    param([string]$ProjectPath)

    if ($targetFrameworkCache.ContainsKey($ProjectPath)) { return $targetFrameworkCache[$ProjectPath] }

    $result = $null
    if (Test-Path -LiteralPath $ProjectPath -PathType Leaf) {
        $xml = [xml](Get-Content -LiteralPath $ProjectPath -Raw -Encoding UTF8)
        $single = $xml.SelectNodes('//*[local-name()="TargetFramework"]').Count -gt 0
        $multiple = $xml.SelectNodes('//*[local-name()="TargetFrameworks"]').Count -gt 0
        # 兩者都存在時視為多 TFM（條件式覆寫），不足以斷定必然分裂。
        $result = $single -and -not $multiple
    }

    $targetFrameworkCache[$ProjectPath] = $result
    return $result
}

$projects = Get-ChildItem -Path $Root -Recurse -Filter '*.csproj' -File |
    Where-Object {
        $_.FullName -notmatch '[\\/]bin[\\/]' -and
        $_.FullName -notmatch '[\\/]obj[\\/]' -and
        $_.FullName -notmatch '[\\/]artifacts[\\/]'
    }

foreach ($project in $projects) {
    $relative = $project.FullName.Substring($Root.Length + 1)
    $xml = [xml](Get-Content -LiteralPath $project.FullName -Raw -Encoding UTF8)

    foreach ($node in $xml.SelectNodes('//*[local-name()="ProjectReference"]')) {
        $include = $node.GetAttribute('Include')
        if ([string]::IsNullOrWhiteSpace($include)) { continue }

        $targetPath = [IO.Path]::GetFullPath(
            (Join-Path $project.DirectoryName ($include -replace '\\', [IO.Path]::DirectorySeparatorChar)))

        $metadata = [ordered]@{}
        foreach ($name in @('SetTargetFramework') + $splitMetadata + @('GlobalPropertiesToRemove')) {
            $metadata[$name] = Get-MetadataValue -Element $node -Name $name
        }

        $references.Add([PSCustomObject]@{
                Consumer = $relative
                Target = $targetPath
                TargetName = [IO.Path]::GetFileNameWithoutExtension($targetPath)
                Metadata = $metadata
            })
    }
}

foreach ($reference in $references) {
    # 規則 1：單一 TFM 被參考專案不得帶 SetTargetFramework。
    if ($reference.Metadata['SetTargetFramework']) {
        $isSingle = Test-SingleTargetFramework -ProjectPath $reference.Target
        if ($isSingle -eq $true) {
            $issues.Add([PSCustomObject]@{
                    Rule = '單一 TFM 不可釘 TFM'
                    Consumer = $reference.Consumer
                    Reference = $reference.TargetName
                    Detail = "SetTargetFramework=$($reference.Metadata['SetTargetFramework'])"
                })
        }
    }

    # 規則 2：明確宣告的 GlobalPropertiesToRemove 必須包含 PublishAot。
    $remove = $reference.Metadata['GlobalPropertiesToRemove']
    if ($remove -and ($remove -split ';' | Where-Object { $_.Trim() -eq 'PublishAot' }).Count -eq 0) {
        $issues.Add([PSCustomObject]@{
                Rule = '覆寫後缺少 PublishAot'
                Consumer = $reference.Consumer
                Reference = $reference.TargetName
                Detail = "GlobalPropertiesToRemove=$remove"
            })
    }
}

# 規則 3：同一個被參考專案的分裂型 metadata 必須跨消費端一致。
foreach ($group in $references | Group-Object -Property Target) {
    foreach ($name in $splitMetadata) {
        $values = @($group.Group | ForEach-Object { $_.Metadata[$name] } | Select-Object -Unique)
        if ($values.Count -gt 1) {
            $rendered = ($values | ForEach-Object { if ($_) { $_ } else { '(未設定)' } }) -join ' / '
            $issues.Add([PSCustomObject]@{
                    Rule = "$name 跨消費端不一致"
                    Consumer = ($group.Group.Consumer -join ', ')
                    Reference = $group.Group[0].TargetName
                    Detail = $rendered
                })
        }
    }
}

# 規則 4：每個 Directory.Build.props 都必須匯入共用預設值（巢狀檔案會遮蔽上層）。
$sharedDefaults = 'ProjectReferenceDefaults.props'
$buildPropsFiles = Get-ChildItem -Path $Root -Recurse -Filter 'Directory.Build.props' -File |
    Where-Object {
        $_.FullName -notmatch '[\\/]bin[\\/]' -and
        $_.FullName -notmatch '[\\/]obj[\\/]' -and
        $_.FullName -notmatch '[\\/]artifacts[\\/]'
    }

foreach ($file in $buildPropsFiles) {
    $xml = [xml](Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8)
    $imports = @($xml.SelectNodes('//*[local-name()="Import"]') |
        ForEach-Object { $_.GetAttribute('Project') } |
        Where-Object { $_ -like "*$sharedDefaults" })

    if ($imports.Count -eq 0) {
        $issues.Add([PSCustomObject]@{
                Rule = '未匯入共用 ProjectReference 預設值'
                Consumer = $file.FullName.Substring($Root.Length + 1)
                Reference = $sharedDefaults
                Detail = '巢狀 Directory.Build.props 會遮蔽上層，其下專案將失去 GlobalPropertiesToRemove 預設值'
            })
    }
}

if ($issues.Count -gt 0) {
    foreach ($issue in $issues) {
        Write-Host "[$($issue.Rule)] $($issue.Reference)"
        Write-Host "  消費端：$($issue.Consumer)"
        Write-Host "  內容：$($issue.Detail)"
    }
    Write-Error "偵測到 $($issues.Count) 項 ProjectReference 建置競態風險。"
    exit 1
}

Write-Host "OK：$($references.Count) 個 ProjectReference 無建置競態風險 metadata。"
exit 0
