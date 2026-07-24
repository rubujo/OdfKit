using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Worker;

internal sealed class FileSystemGenerationCache
{
    private readonly string _cacheDirectory;
    private readonly WebFontWorkerOptions _options;

    public FileSystemGenerationCache(string cacheDirectory, WebFontWorkerOptions options)
    {
        _cacheDirectory = Path.GetFullPath(cacheDirectory);
        _options = options;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<WebFontManifest?> TryLoadAsync(
        string key,
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        string path = GetManifestPath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length <= 0 || stream.Length > _options.MaxCachedManifestBytes)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"), exception);
        }

        WebFontManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(bytes, WebFontJsonContext.Default.WebFontManifest)
                ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"), exception);
        }

        ValidateManifest(manifest, request, destinationDirectory);
        TryTouch(path);
        return manifest;
    }

    public async Task<IAsyncDisposable> AcquireLeaseAsync(string key, CancellationToken cancellationToken)
    {
        string path = Path.Combine(_cacheDirectory, string.Concat(key, ".lock"));
        TimeSpan retryDelay = _options.CacheLockRetryDelay;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                FileStream stream = new(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                return new FileLease(stream);
            }
            // Windows 在 DeleteOnClose 的 handle 關閉後會讓檔案進入 delete pending 狀態；
            // 此時其他處理程序開啟會得到 STATUS_DELETE_PENDING，對應 ERROR_ACCESS_DENIED，
            // .NET 拋出的是 UnauthorizedAccessException 而非 IOException。只攔 IOException
            // 會讓租約在正常競爭下直接失敗，而不是依退避重試。
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException)
            {
                double jitter = 1.0 + (Random.Shared.NextDouble() * 0.25);
                TimeSpan actualDelay = TimeSpan.FromMilliseconds(Math.Min(
                    retryDelay.TotalMilliseconds * jitter,
                    _options.MaxCacheLockRetryDelay.TotalMilliseconds));
                await Task.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
                retryDelay = TimeSpan.FromMilliseconds(Math.Min(
                    retryDelay.TotalMilliseconds * 2,
                    _options.MaxCacheLockRetryDelay.TotalMilliseconds));
            }
        }
    }

    public async Task StoreAsync(
        string key,
        WebFontManifest manifest,
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        ValidateManifest(manifest, request, destinationDirectory);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            manifest,
            WebFontJsonContext.Default.WebFontManifest);
        if (bytes.Length <= 0 || bytes.Length > _options.MaxCachedManifestBytes)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        string path = GetManifestPath(key);
        string temporaryPath = Path.Combine(
            _cacheDirectory,
            string.Concat(key, ".", Guid.NewGuid().ToString("N"), ".tmp"));
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
            PruneDurableManifests(path, destinationDirectory);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void ValidateManifest(
        WebFontManifest manifest,
        WebFontSubsetRequest request,
        string destinationDirectory)
    {
        if (manifest.SchemaVersion != 1
            || !string.Equals(manifest.ProfileId, request.ProfileId, StringComparison.Ordinal)
            || manifest.Assets is not { Count: > 0 } assets
            || assets.Count > _options.MaxCachedAssetCount
            || manifest.StylesheetFileName is not null
            || manifest.StylesheetSha256 is not null)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        string rootPath = Path.GetFullPath(destinationDirectory);
        WebFontFormat[] requestedFormats = request.Formats
            .Distinct()
            .OrderBy(format => format)
            .ToArray();
        IReadOnlyList<string> expectedRanges = CreateUnicodeRanges(request.Sequences
            .SelectMany(sequence => sequence.UnicodeScalars)
            .Where(RequiresGlyph));
        if (assets.Count != requestedFormats.Length
            || !assets.Select(asset => asset?.Format)
                .OrderBy(format => format)
                .SequenceEqual(requestedFormats.Select(format => (WebFontFormat?)format)))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        foreach (WebFontAsset asset in assets)
        {
            if (asset is null
                || !IsPlainFileName(asset.FileName)
                || !IsSha256(asset.Sha256)
                || asset.ByteLength <= 0
                || asset.ByteLength > _options.MaxCachedAssetBytes
                || !string.Equals(asset.FontFamily, request.FontFamily, StringComparison.Ordinal)
                || !request.Formats.Contains(asset.Format)
                || asset.UnicodeRanges is null
                || asset.UnicodeRanges.Count > 4096
                || asset.UnicodeRanges.Any(range => string.IsNullOrEmpty(range) || range.Length is <= 2 or > 64)
                || !asset.UnicodeRanges.SequenceEqual(expectedRanges, StringComparer.Ordinal))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            string assetPath = Path.GetFullPath(Path.Combine(rootPath, asset.Sha256, asset.FileName));
            if (!IsContainedPath(rootPath, assetPath))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            var info = new FileInfo(assetPath);
            if (!info.Exists
                || info.LinkTarget is not null
                || new DirectoryInfo(info.DirectoryName!).LinkTarget is not null
                || info.Length != asset.ByteLength
                || !string.Equals(ComputeSha256(assetPath), asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }
        }
    }

    private string GetManifestPath(string key)
        => Path.Combine(_cacheDirectory, string.Concat(key, ".json"));

    private void PruneDurableManifests(string currentPath, string destinationDirectory)
    {
        string lockPath = Path.Combine(_cacheDirectory, ".cleanup.lock");
        try
        {
            using var cleanupLease = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            DateTime threshold = DateTime.UtcNow - _options.DurableManifestMaxIdle;
            StringComparison pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            FileInfo[] manifests = new DirectoryInfo(_cacheDirectory)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(info => string.Equals(info.FullName, currentPath, pathComparison))
                .ThenByDescending(info => info.LastWriteTimeUtc)
                .ToArray();
            long retainedBytes = 0;
            int retainedCount = 0;
            foreach (FileInfo manifest in manifests)
            {
                bool isCurrent = string.Equals(manifest.FullName, currentPath, pathComparison);
                bool retain = isCurrent
                    || manifest.LastWriteTimeUtc >= threshold
                    && retainedCount < _options.MaxDurableManifestEntries
                    && retainedBytes + manifest.Length <= _options.MaxDurableManifestBytes;
                if (retain)
                {
                    retainedCount++;
                    retainedBytes += manifest.Length;
                    continue;
                }

                TryDelete(manifest.FullName);
            }

            foreach (FileInfo temporary in new DirectoryInfo(_cacheDirectory)
                         .EnumerateFiles("*.tmp", SearchOption.TopDirectoryOnly)
                         .Where(info => info.LastWriteTimeUtc < threshold))
            {
                TryDelete(temporary.FullName);
            }

            PruneUnreferencedAssets(
                manifests,
                destinationDirectory,
                DateTime.UtcNow - _options.DurableAssetMaxIdle);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
            // 清理屬最佳努力；其它處理程序持有 lease 時不可讓成功的產字要求失敗。
        }
    }

    private void PruneUnreferencedAssets(
        IEnumerable<FileInfo> manifests,
        string destinationDirectory,
        DateTime threshold)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (FileInfo manifestFile in manifests.Where(file => file.Exists))
        {
            try
            {
                if (manifestFile.Length <= 0 || manifestFile.Length > _options.MaxCachedManifestBytes)
                {
                    continue;
                }

                WebFontManifest? manifest = JsonSerializer.Deserialize(
                    File.ReadAllBytes(manifestFile.FullName),
                    WebFontJsonContext.Default.WebFontManifest);
                if (manifest?.Assets is null)
                {
                    continue;
                }

                foreach (WebFontAsset asset in manifest.Assets)
                {
                    if (asset is not null && IsSha256(asset.Sha256) && IsPlainFileName(asset.FileName))
                    {
                        referenced.Add(Path.Combine(asset.Sha256, asset.FileName));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or JsonException)
            {
                // 損毀 manifest 由讀取路徑負責拒絕；清理不可影響成功產字。
            }
        }

        string root = Path.GetFullPath(destinationDirectory);
        if (!Directory.Exists(root))
        {
            return;
        }

        FileInfo[] assets = new DirectoryInfo(root)
            .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            .Where(directory => IsSha256(directory.Name) && directory.LinkTarget is null)
            .SelectMany(directory => directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            .Where(file => file.LinkTarget is null)
            .ToArray();
        long retainedBytes = assets.Sum(file => file.Length);
        foreach (FileInfo asset in assets
                     .Where(file => !referenced.Contains(Path.Combine(file.Directory!.Name, file.Name))
                         && file.LastWriteTimeUtc < threshold)
                     .OrderBy(file => file.LastWriteTimeUtc))
        {
            if (retainedBytes <= _options.MaxDurableAssetBytes)
            {
                break;
            }

            long length = asset.Length;
            TryDelete(asset.FullName);
            asset.Refresh();
            if (!asset.Exists)
            {
                retainedBytes -= length;
                TryDeleteEmptyDirectory(asset.DirectoryName!);
            }
        }
    }

    private static void TryTouch(string path)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (!Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
        }
    }

    private static bool IsPlainFileName(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 255
            && string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
            && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F');

    private static bool IsContainedPath(string rootPath, string candidatePath)
    {
        string rootWithSeparator = rootPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return candidatePath.StartsWith(rootWithSeparator, comparison);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool RequiresGlyph(int scalar)
        => WebFontUnicodePolicy.RequiresStandaloneGlyph(scalar);

    private static IReadOnlyList<string> CreateUnicodeRanges(IEnumerable<int> scalars)
    {
        int[] values = scalars.Distinct().OrderBy(value => value).ToArray();
        if (values.Length == 0)
        {
            return Array.Empty<string>();
        }

        var ranges = new List<string>();
        int start = values[0];
        int end = start;
        for (int index = 1; index < values.Length; index++)
        {
            int value = values[index];
            if (value == end + 1)
            {
                end = value;
                continue;
            }

            ranges.Add(FormatUnicodeRange(start, end));
            start = value;
            end = value;
        }

        ranges.Add(FormatUnicodeRange(start, end));
        return ranges;
    }

    private static string FormatUnicodeRange(int start, int end)
        => start == end ? $"U+{start:X}" : $"U+{start:X}-{end:X}";

    private sealed class FileLease(FileStream stream) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }
}
