#Requires -Version 7.0

function Invoke-WebFontHostedAssetLoad {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [Net.Http.HttpClient]$Client,

        [Parameter(Mandatory)]
        [Uri]$AssetUri,

        [Parameter(Mandatory)]
        [ValidatePattern('^[0-9a-f]{64}$')]
        [string]$ExpectedSha256,

        [Parameter(Mandatory)]
        [ValidateRange(1, [long]::MaxValue)]
        [long]$ExpectedByteLength,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [Diagnostics.Process[]]$HostProcesses,

        [ValidateRange(1, 256)]
        [int]$Concurrency = 16,

        [ValidateRange(1, 10000)]
        [int]$MinimumRequestCount = 256,

        [ValidateRange(1, 10000)]
        [int]$MaximumRequestCount = 1024,

        [ValidateRange(1, 60)]
        [int]$MinimumDurationSeconds = 5
    )

    if ($MaximumRequestCount -lt $MinimumRequestCount) {
        throw "MaximumRequestCount 不得小於 MinimumRequestCount。"
    }

    foreach ($hostProcess in $HostProcesses) {
        $hostProcess.Refresh()
    }
    $initialCpuTicks = [long](($HostProcesses |
            ForEach-Object { $_.TotalProcessorTime.Ticks } |
            Measure-Object -Sum).Sum)
    $initialWorkingSetBytes = [long](($HostProcesses | Measure-Object WorkingSet64 -Sum).Sum)
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $requestCount = 0
    [long]$totalBytes = 0

    do {
        $batchSize = [Math]::Min($Concurrency, $MaximumRequestCount - $requestCount)
        $tasks = [Collections.Generic.List[Threading.Tasks.Task[byte[]]]]::new($batchSize)
        for ($index = 0; $index -lt $batchSize; $index++) {
            $tasks.Add($Client.GetByteArrayAsync($AssetUri))
        }

        $responses = [Threading.Tasks.Task]::WhenAll($tasks.ToArray()).GetAwaiter().GetResult()
        foreach ($bytes in $responses) {
            $actualSha256 = [Convert]::ToHexString(
                [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            if ($bytes.LongLength -ne $ExpectedByteLength -or $actualSha256 -ne $ExpectedSha256) {
                throw "IIS hosted load 回應的長度或 SHA-256 與 manifest 不一致。"
            }
            if ($totalBytes -gt [long]::MaxValue - $bytes.LongLength) {
                throw "IIS hosted load 的總傳輸 bytes 溢位。"
            }
            $totalBytes += $bytes.LongLength
        }
        $requestCount += $batchSize
    }
    while ($requestCount -lt $MinimumRequestCount -or
        ($requestCount -lt $MaximumRequestCount -and $stopwatch.Elapsed.TotalSeconds -lt $MinimumDurationSeconds))

    $stopwatch.Stop()
    foreach ($hostProcess in $HostProcesses) {
        $hostProcess.Refresh()
    }
    $finalCpuTicks = [long](($HostProcesses |
            ForEach-Object { $_.TotalProcessorTime.Ticks } |
            Measure-Object -Sum).Sum)
    $cpuMilliseconds = [long]([TimeSpan]::FromTicks($finalCpuTicks - $initialCpuTicks).TotalMilliseconds)
    # 多程序 hosting 沒有共同的 peak 時間點；相加各程序 peak 是保守上界，適合作為 CI 回歸預算。
    $peakWorkingSetBytes = [long](($HostProcesses | Measure-Object PeakWorkingSet64 -Sum).Sum)
    if ($requestCount -lt $MinimumRequestCount -or
        $requestCount -gt $MaximumRequestCount -or
        $stopwatch.Elapsed -gt [TimeSpan]::FromMinutes(1) -or
        $cpuMilliseconds -gt 90000 -or
        $peakWorkingSetBytes -gt 1536L * 1024 * 1024) {
        throw "IIS hosted load 超出可重現的資源預算。"
    }

    return [ordered]@{
        schemaVersion = 1
        requestCount = $requestCount
        concurrency = $Concurrency
        minimumRequestCount = $MinimumRequestCount
        maximumRequestCount = $MaximumRequestCount
        minimumDurationSeconds = $MinimumDurationSeconds
        totalBytes = $totalBytes
        elapsedMilliseconds = $stopwatch.ElapsedMilliseconds
        cpuMilliseconds = $cpuMilliseconds
        initialWorkingSetBytes = $initialWorkingSetBytes
        peakWorkingSetBytes = $peakWorkingSetBytes
        bytesPerSecond = [long]($totalBytes / [Math]::Max($stopwatch.Elapsed.TotalSeconds, 0.001))
        hostProcesses = @($HostProcesses | ForEach-Object {
                [ordered]@{
                    id = $_.Id
                    name = $_.ProcessName
                }
            })
    }
}
