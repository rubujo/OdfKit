using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Cli;

/// <summary>
/// 提供 OdfKit 命令列工具的命令解析與執行流程。
/// </summary>
public static class OdfKitCli
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// 執行命令列工具。
    /// </summary>
    /// <param name="args">命令列引數。</param>
    /// <param name="output">標準輸出寫入器。</param>
    /// <param name="error">標準錯誤寫入器。</param>
    /// <returns>程序結束碼，0 表示成功。</returns>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is null) throw new ArgumentNullException(nameof(args));
        if (output is null) throw new ArgumentNullException(nameof(output));
        if (error is null) throw new ArgumentNullException(nameof(error));

        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                WriteUsage(output);
                return 0;
            }

            return args[0] switch
            {
                "validate" => Validate(args, output, error),
                "validate-corpus" => ValidateCorpus(args, output, error),
                "info" => Info(args, output, error),
                "metadata" => Metadata(args, output, error),
                "sanitize" => Sanitize(args, output, error),
                "typed-dom-coverage" => TypedDomCoverage(args, output, error),
                "convert-flat" => ConvertFlat(args, output, error),
                "pack" => Pack(args, output, error),
                _ => UnknownCommand(args[0], error)
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or TimeoutException)
        {
            error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static int Validate(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParseValidateOptions(args, error, out ValidateOptions? options))
        {
            return 2;
        }

        ValidateOptions parsedOptions = options ?? throw new InvalidOperationException("validate options were not parsed.");
        IReadOnlyList<string> files = ResolveValidateFiles(parsedOptions.Path, parsedOptions.Recursive);
        if (files.Count == 0)
        {
            error.WriteLine("no ODF files found: " + parsedOptions.Path);
            return 2;
        }

        ValidateBaselineExceptionSet baselineExceptions = ValidateBaselineExceptionSet.Load(parsedOptions.BaselineExceptionsPath);
        List<ValidateFileResult> results = [];
        foreach (string file in files)
        {
            OdfValidationReport report = OdfValidator.Validate(
                file,
                new OdfValidationOptions
                {
                    FileName = file,
                    Profile = parsedOptions.Profile
                });

            ValidateBaselineResult? baseline = ValidateWithBaseline(file, parsedOptions.BaselineOptions);
            bool documentedException = baselineExceptions.Contains(
                file,
                parsedOptions.Baseline,
                report.IsValid,
                baseline?.IsValid,
                parsedOptions.Profile?.Id);
            results.Add(new ValidateFileResult(file, report, baseline, documentedException));
        }

        ValidateSummary summary = ValidateSummary.Create(results);
        if (parsedOptions.Format == ValidateOutputFormat.Json)
        {
            WriteValidateJson(output, summary, results);
        }
        else if (!parsedOptions.Quiet)
        {
            WriteValidateText(output, summary, results);
        }

        return ShouldFail(summary, parsedOptions.FailOn) ? 1 : 0;
    }

    private static int ValidateCorpus(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParseValidateCorpusOptions(args, error, out ValidateCorpusOptions? options))
        {
            return 2;
        }

        ValidateCorpusOptions parsedOptions = options ?? throw new InvalidOperationException("validate-corpus options were not parsed.");
        ValidateCorpusManifest manifest = ValidateCorpusManifest.Load(parsedOptions.ManifestPath);
        ValidateBaselineExceptionSet baselineExceptions = ValidateBaselineExceptionSet.Load(parsedOptions.BaselineExceptionsPath);
        if (parsedOptions.MetadataOnly)
        {
            WriteValidateCorpusMetadataOnly(output, parsedOptions, manifest, baselineExceptions);
            return 0;
        }

        List<ValidateCorpusFixtureResult> results = [];
        foreach (ValidateCorpusFixture fixture in manifest.Fixtures)
        {
            string path = ResolveCorpusFixturePath(parsedOptions.RootPath, fixture.Path);
            if (!File.Exists(path))
            {
                throw new InvalidDataException("corpus fixture not found: " + fixture.Path);
            }

            OdfValidationReport report = OdfValidator.Validate(
                path,
                new OdfValidationOptions
                {
                    FileName = path,
                    Profile = fixture.Profile
                });

            ValidateBaselineResult? baseline = ValidateWithBaseline(path, parsedOptions.BaselineOptions);
            bool documentedException = baselineExceptions.Contains(
                path,
                parsedOptions.Baseline,
                report.IsValid,
                baseline?.IsValid,
                fixture.Profile.Id);
            results.Add(new ValidateCorpusFixtureResult(fixture, path, report, baseline, documentedException));
        }

        ValidateCorpusSummary summary = ValidateCorpusSummary.Create(results);
        if (parsedOptions.Format == ValidateOutputFormat.Json)
        {
            WriteValidateCorpusJson(output, summary, results);
        }
        else if (!parsedOptions.Quiet)
        {
            WriteValidateCorpusText(output, summary, results);
        }

        return summary.FailedCount > 0 || summary.BaselineMismatchCount > 0 ? 1 : 0;
    }

    private static int Info(string[] args, TextWriter output, TextWriter error)
    {
        if (!RequireArity(args, 2, "info file.ods", error))
        {
            return 2;
        }

        using OdfPackage package = OdfPackage.Open(args[1]);
        OdfValidationReport report = OdfValidator.Validate(package, fileName: args[1]);
        output.WriteLine("path: " + args[1]);
        output.WriteLine("kind: " + report.DocumentKind);
        output.WriteLine("mime: " + (package.MimeType ?? string.Empty));
        output.WriteLine("version: " + FormatVersion(package.Version));
        output.WriteLine("flat: " + package.IsFlatXml.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("entries: " + package.GetEntries().Count().ToString(CultureInfo.InvariantCulture));
        return 0;
    }

    private static int Metadata(string[] args, TextWriter output, TextWriter error)
    {
        if (!RequireArity(args, 2, "metadata file.odt", error))
        {
            return 2;
        }

        using OdfDocument document = OdfDocument.Load(args[1]);
        output.WriteLine("title: " + (document.Title ?? string.Empty));
        output.WriteLine("creator: " + (document.Creator ?? string.Empty));
        output.WriteLine("subject: " + (document.Subject ?? string.Empty));
        output.WriteLine("description: " + (document.Description ?? string.Empty));
        return 0;
    }

    private static int TypedDomCoverage(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParseTypedDomCoverageOptions(args, error, out ValidateOutputFormat format))
        {
            return 2;
        }

        OdfTypedDomCoverageReport report = OdfTypedDomCoverage.Build();
        if (format == ValidateOutputFormat.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(report.ToJsonModel(), JsonOptions));
            return 0;
        }

        output.WriteLine("schema-version: " + report.SchemaVersion);
        output.WriteLine("schema-source: " + report.SchemaSourceUrl);
        output.WriteLine("schema-elements: " + report.SchemaElementCount.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("typed-elements: " + report.TypedElementCount.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("fallback-elements: " + report.FallbackElementCount.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("schema-child-element-relations: " + report.SchemaChildElementRelationCount.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("schema-attributes: " + report.SchemaAttributeCount.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("wrapper-properties: " + report.WrapperPropertyCount.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("wrapper-property-types:");
        foreach (KeyValuePair<string, int> pair in report.WrapperPropertyTypeCounts)
        {
            output.WriteLine("  " + pair.Key + ": " + pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        return 0;
    }

    private static int Sanitize(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParseSanitizeOptions(args, error, out SanitizeOptions? options))
        {
            return 2;
        }

        SanitizeOptions parsedOptions = options ?? throw new InvalidOperationException("sanitize options were not parsed.");
        if (!File.Exists(parsedOptions.InputPath))
        {
            error.WriteLine("path not found: " + parsedOptions.InputPath);
            return 2;
        }

        using OdfDocument document = OdfDocument.Load(
            parsedOptions.InputPath,
            new OdfLoadOptions { Password = parsedOptions.InputPassword });
        int artifactCountBefore = CountSanitizableArtifacts(document.Package);
        document.SanitizeMacros();
        int artifactCountAfter = CountSanitizableArtifacts(document.Package);
        document.Save(
            parsedOptions.OutputPath,
            new OdfSaveOptions
            {
                Password = parsedOptions.OutputPassword,
                EncryptionAlgorithm = parsedOptions.EncryptionAlgorithm
            });
        output.WriteLine("wrote: " + parsedOptions.OutputPath);
        output.WriteLine("removed-artifacts: " + Math.Max(0, artifactCountBefore - artifactCountAfter).ToString(CultureInfo.InvariantCulture));
        if (parsedOptions.OutputPassword is not null)
        {
            output.WriteLine("encrypted-output: true");
            output.WriteLine("encryption-algorithm: " + FormatEncryptionAlgorithm(parsedOptions.EncryptionAlgorithm));
        }

        return 0;
    }

    private static int ConvertFlat(string[] args, TextWriter output, TextWriter error)
    {
        if (!RequireArity(args, 3, "convert-flat input.odt output.fodt", error))
        {
            return 2;
        }

        using OdfDocument document = OdfDocument.Load(args[1]);
        document.Package.IsFlatXml = true;
        document.Save(args[2]);
        output.WriteLine("wrote: " + args[2]);
        return 0;
    }

    private static int Pack(string[] args, TextWriter output, TextWriter error)
    {
        if (!RequireArity(args, 3, "pack input.fodt output.odt", error))
        {
            return 2;
        }

        using OdfDocument document = OdfDocument.Load(args[1]);
        document.Package.IsFlatXml = false;
        document.Save(args[2]);
        output.WriteLine("wrote: " + args[2]);
        return 0;
    }

    private static bool RequireArity(string[] args, int expected, string usage, TextWriter error)
    {
        if (args.Length == expected)
        {
            return true;
        }

        error.WriteLine("usage: odfkit " + usage);
        return false;
    }

    private static string ResolveCorpusFixturePath(string rootPath, string fixturePath)
    {
        if (Path.IsPathRooted(fixturePath))
        {
            throw new InvalidDataException("corpus fixture path must be relative: " + fixturePath);
        }

        string root = Path.GetFullPath(rootPath);
        string candidate = Path.GetFullPath(Path.Combine(root, fixturePath));
        string rootWithSeparator = EnsureTrailingDirectorySeparator(root);
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("corpus fixture path escapes the corpus root: " + fixturePath);
        }

        return candidate;
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
            path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static int UnknownCommand(string command, TextWriter error)
    {
        error.WriteLine("unknown command: " + command);
        return 2;
    }

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("usage: odfkit <command> [arguments]");
        output.WriteLine("commands:");
        output.WriteLine("  validate file-or-folder [--format text|json] [--profile id] [--fail-on error|warning] [--recursive] [--quiet] [--baseline odf-validator] [--baseline-jar path] [--baseline-command path] [--baseline-exceptions path]");
        output.WriteLine("  validate-corpus manifest.json [--root path] [--format text|json] [--quiet] [--metadata-only] [--baseline odf-validator] [--baseline-jar path] [--baseline-command path] [--baseline-exceptions path]");
        output.WriteLine("  info file.ods");
        output.WriteLine("  sanitize input.odt output.odt [--password value] [--output-password value] [--encryption aes256|blowfish]");
        output.WriteLine("  typed-dom-coverage [--format text|json]");
        output.WriteLine("  convert-flat input.odt output.fodt");
        output.WriteLine("  pack input.fodt output.odt");
        output.WriteLine("  metadata file.odt");
    }

    private static bool TryParseValidateOptions(string[] args, TextWriter error, out ValidateOptions? options)
    {
        options = null;
        string? path = null;
        ValidateOutputFormat format = ValidateOutputFormat.Text;
        ValidateFailOn failOn = ValidateFailOn.Error;
        OdfComplianceProfile? profile = null;
        bool recursive = false;
        bool quiet = false;
        ValidateBaselineKind baseline = ValidateBaselineKind.None;
        string? baselineJarPath = null;
        string? baselineCommandPath = null;
        string? baselineExceptionsPath = null;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--format":
                    if (!TryReadValue(args, ref i, error, "--format", out string? formatValue) ||
                        !TryParseFormat(formatValue, out format))
                    {
                        error.WriteLine("supported formats: text, json");
                        return false;
                    }
                    break;
                case "--profile":
                    if (!TryReadValue(args, ref i, error, "--profile", out string? profileId))
                    {
                        return false;
                    }

                    profile = OdfComplianceProfiles.Find(profileId!);
                    if (profile is null)
                    {
                        error.WriteLine("unknown profile: " + profileId);
                        return false;
                    }
                    break;
                case "--fail-on":
                    if (!TryReadValue(args, ref i, error, "--fail-on", out string? failOnValue) ||
                        !TryParseFailOn(failOnValue, out failOn))
                    {
                        error.WriteLine("supported fail-on values: error, warning");
                        return false;
                    }
                    break;
                case "--recursive":
                    recursive = true;
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                case "--baseline":
                    if (!TryReadValue(args, ref i, error, "--baseline", out string? baselineValue) ||
                        !TryParseBaseline(baselineValue, out baseline))
                    {
                        error.WriteLine("supported baselines: none, odf-validator, command");
                        return false;
                    }
                    break;
                case "--baseline-jar":
                    if (!TryReadValue(args, ref i, error, "--baseline-jar", out baselineJarPath))
                    {
                        return false;
                    }
                    baseline = ValidateBaselineKind.OdfValidator;
                    break;
                case "--baseline-command":
                    if (!TryReadValue(args, ref i, error, "--baseline-command", out baselineCommandPath))
                    {
                        return false;
                    }
                    baseline = ValidateBaselineKind.Command;
                    break;
                case "--baseline-exceptions":
                    if (!TryReadValue(args, ref i, error, "--baseline-exceptions", out baselineExceptionsPath))
                    {
                        return false;
                    }
                    break;
                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        error.WriteLine("unknown option: " + arg);
                        return false;
                    }

                    if (path is not null)
                    {
                        error.WriteLine("usage: odfkit validate file-or-folder [options]");
                        return false;
                    }

                    path = arg;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error.WriteLine("usage: odfkit validate file-or-folder [options]");
            return false;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            error.WriteLine("path not found: " + path);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(baselineExceptionsPath) && !File.Exists(baselineExceptionsPath))
        {
            error.WriteLine("baseline exceptions file not found: " + baselineExceptionsPath);
            return false;
        }

        options = new ValidateOptions(
            path,
            format,
            failOn,
            profile,
            recursive,
            quiet,
            baseline,
            baselineJarPath,
            baselineCommandPath,
            baselineExceptionsPath);
        return true;
    }

    private static bool TryParseValidateCorpusOptions(string[] args, TextWriter error, out ValidateCorpusOptions? options)
    {
        options = null;
        string? manifestPath = null;
        string? rootPath = null;
        ValidateOutputFormat format = ValidateOutputFormat.Text;
        bool quiet = false;
        bool metadataOnly = false;
        ValidateBaselineKind baseline = ValidateBaselineKind.None;
        string? baselineJarPath = null;
        string? baselineCommandPath = null;
        string? baselineExceptionsPath = null;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--root":
                    if (!TryReadValue(args, ref i, error, "--root", out rootPath))
                    {
                        return false;
                    }
                    break;
                case "--format":
                    if (!TryReadValue(args, ref i, error, "--format", out string? formatValue) ||
                        !TryParseFormat(formatValue, out format))
                    {
                        error.WriteLine("supported formats: text, json");
                        return false;
                    }
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                case "--metadata-only":
                    metadataOnly = true;
                    break;
                case "--baseline":
                    if (!TryReadValue(args, ref i, error, "--baseline", out string? baselineValue) ||
                        !TryParseBaseline(baselineValue, out baseline))
                    {
                        error.WriteLine("supported baselines: none, odf-validator, command");
                        return false;
                    }
                    break;
                case "--baseline-jar":
                    if (!TryReadValue(args, ref i, error, "--baseline-jar", out baselineJarPath))
                    {
                        return false;
                    }
                    baseline = ValidateBaselineKind.OdfValidator;
                    break;
                case "--baseline-command":
                    if (!TryReadValue(args, ref i, error, "--baseline-command", out baselineCommandPath))
                    {
                        return false;
                    }
                    baseline = ValidateBaselineKind.Command;
                    break;
                case "--baseline-exceptions":
                    if (!TryReadValue(args, ref i, error, "--baseline-exceptions", out baselineExceptionsPath))
                    {
                        return false;
                    }
                    break;
                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        error.WriteLine("unknown option: " + arg);
                        return false;
                    }

                    if (manifestPath is not null)
                    {
                        error.WriteLine("usage: odfkit validate-corpus manifest.json [options]");
                        return false;
                    }

                    manifestPath = arg;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            error.WriteLine("usage: odfkit validate-corpus manifest.json [options]");
            return false;
        }

        if (!File.Exists(manifestPath))
        {
            error.WriteLine("manifest not found: " + manifestPath);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rootPath) && !Directory.Exists(rootPath))
        {
            error.WriteLine("root not found: " + rootPath);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(baselineExceptionsPath) && !File.Exists(baselineExceptionsPath))
        {
            error.WriteLine("baseline exceptions file not found: " + baselineExceptionsPath);
            return false;
        }

        string resolvedManifestPath = Path.GetFullPath(manifestPath);
        string resolvedRootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.GetDirectoryName(resolvedManifestPath) ?? Directory.GetCurrentDirectory()
            : Path.GetFullPath(rootPath);
        options = new ValidateCorpusOptions(
            resolvedManifestPath,
            resolvedRootPath,
            format,
            quiet,
            metadataOnly,
            baseline,
            baselineJarPath,
            baselineCommandPath,
            baselineExceptionsPath);
        return true;
    }

    private static bool TryParseTypedDomCoverageOptions(string[] args, TextWriter error, out ValidateOutputFormat format)
    {
        format = ValidateOutputFormat.Text;
        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--format":
                    if (!TryReadValue(args, ref i, error, "--format", out string? formatValue) ||
                        !TryParseFormat(formatValue, out format))
                    {
                        error.WriteLine("supported formats: text, json");
                        return false;
                    }
                    break;
                default:
                    error.WriteLine(arg.StartsWith("-", StringComparison.Ordinal)
                        ? "unknown option: " + arg
                        : "usage: odfkit typed-dom-coverage [--format text|json]");
                    return false;
            }
        }

        return true;
    }

    private static bool TryParseSanitizeOptions(string[] args, TextWriter error, out SanitizeOptions? options)
    {
        options = null;
        string? inputPath = null;
        string? outputPath = null;
        string? inputPassword = null;
        string? outputPassword = null;
        bool outputPasswordSpecified = false;
        OdfEncryptionAlgorithm encryptionAlgorithm = OdfEncryptionAlgorithm.Aes256;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--password":
                    if (!TryReadValue(args, ref i, error, "--password", out inputPassword))
                    {
                        return false;
                    }
                    break;
                case "--output-password":
                    if (!TryReadValue(args, ref i, error, "--output-password", out outputPassword))
                    {
                        return false;
                    }

                    outputPasswordSpecified = true;
                    break;
                case "--encryption":
                    if (!TryReadValue(args, ref i, error, "--encryption", out string? algorithmValue) ||
                        !TryParseEncryptionAlgorithm(algorithmValue, out encryptionAlgorithm))
                    {
                        error.WriteLine("supported encryption algorithms: aes256, blowfish");
                        return false;
                    }
                    break;
                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        error.WriteLine("unknown option: " + arg);
                        return false;
                    }

                    if (inputPath is null)
                    {
                        inputPath = arg;
                    }
                    else if (outputPath is null)
                    {
                        outputPath = arg;
                    }
                    else
                    {
                        error.WriteLine("usage: odfkit sanitize input.odt output.odt [options]");
                        return false;
                    }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            error.WriteLine("usage: odfkit sanitize input.odt output.odt [options]");
            return false;
        }

        options = new SanitizeOptions(
            inputPath,
            outputPath,
            inputPassword,
            outputPasswordSpecified ? outputPassword : inputPassword,
            encryptionAlgorithm);
        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, TextWriter error, string optionName, out string? value)
    {
        value = null;
        if (index + 1 >= args.Length)
        {
            error.WriteLine("missing value for " + optionName);
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryParseFormat(string? value, out ValidateOutputFormat format)
    {
        if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
        {
            format = ValidateOutputFormat.Json;
            return true;
        }

        if (string.Equals(value, "text", StringComparison.OrdinalIgnoreCase))
        {
            format = ValidateOutputFormat.Text;
            return true;
        }

        format = ValidateOutputFormat.Text;
        return false;
    }

    private static bool TryParseEncryptionAlgorithm(string? value, out OdfEncryptionAlgorithm algorithm)
    {
        if (string.Equals(value, "aes256", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "aes-256", StringComparison.OrdinalIgnoreCase))
        {
            algorithm = OdfEncryptionAlgorithm.Aes256;
            return true;
        }

        if (string.Equals(value, "blowfish", StringComparison.OrdinalIgnoreCase))
        {
            algorithm = OdfEncryptionAlgorithm.Blowfish;
            return true;
        }

        algorithm = OdfEncryptionAlgorithm.Aes256;
        return false;
    }

    private static string FormatEncryptionAlgorithm(OdfEncryptionAlgorithm algorithm)
    {
        return algorithm == OdfEncryptionAlgorithm.Blowfish ? "blowfish" : "aes256";
    }

    private static bool TryParseFailOn(string? value, out ValidateFailOn failOn)
    {
        if (string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase))
        {
            failOn = ValidateFailOn.Warning;
            return true;
        }

        if (string.Equals(value, "error", StringComparison.OrdinalIgnoreCase))
        {
            failOn = ValidateFailOn.Error;
            return true;
        }

        failOn = ValidateFailOn.Error;
        return false;
    }

    private static bool TryParseBaseline(string? value, out ValidateBaselineKind baseline)
    {
        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            baseline = ValidateBaselineKind.None;
            return true;
        }

        if (string.Equals(value, "odf-validator", StringComparison.OrdinalIgnoreCase))
        {
            baseline = ValidateBaselineKind.OdfValidator;
            return true;
        }

        if (string.Equals(value, "command", StringComparison.OrdinalIgnoreCase))
        {
            baseline = ValidateBaselineKind.Command;
            return true;
        }

        baseline = ValidateBaselineKind.None;
        return false;
    }

    private static ValidateBaselineResult? ValidateWithBaseline(string file, ValidateBaselineOptions options)
    {
        if (options.Baseline == ValidateBaselineKind.None)
        {
            return null;
        }

        OdfExternalValidatorResult result = options.Baseline switch
        {
            ValidateBaselineKind.OdfValidator => OdfExternalValidator.ValidateWithOdfValidator(file, options.BaselineJarPath),
            ValidateBaselineKind.Command => OdfExternalValidator.ValidateWithCommand(
                options.BaselineCommandPath ?? throw new ArgumentException("未提供 baseline command 路徑。"),
                file),
            _ => throw new ArgumentOutOfRangeException(nameof(options), "不支援的 baseline。")
        };

        return new ValidateBaselineResult(
            options.Baseline.ToString(),
            result.ExitCode,
            result.IsValid,
            result.StandardOutput,
            result.StandardError);
    }

    private static IReadOnlyList<string> ResolveValidateFiles(string path, bool recursive)
    {
        if (File.Exists(path))
        {
            return [Path.GetFullPath(path)];
        }

        SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(path, "*", searchOption)
            .Where(file => OdfDocumentKindDetector.TryGetFormatByFileName(file, out _))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFullPath)
            .ToArray();
    }

    private static int CountSanitizableArtifacts(OdfPackage package)
    {
        return package.GetEntries().Count(entry => IsSanitizableArtifact(entry.Path));
    }

    private static bool IsSanitizableArtifact(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.StartsWith("Basic/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "META-INF/documentsignatures.xml", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteValidateText(
        TextWriter output,
        ValidateSummary summary,
        IReadOnlyList<ValidateFileResult> results)
    {
        foreach (ValidateFileResult result in results)
        {
            OdfValidationReport report = result.Report;
            output.WriteLine(report.IsValid ? "valid" : "invalid");
            output.WriteLine("path: " + result.Path);
            output.WriteLine("kind: " + report.DocumentKind);
            output.WriteLine("version: " + FormatVersion(report.DetectedVersion));
            output.WriteLine("issues: " + report.Issues.Count.ToString(CultureInfo.InvariantCulture));
            if (result.Baseline is not null)
            {
                output.WriteLine("baseline: " + result.Baseline.Kind);
                output.WriteLine("baseline-valid: " + result.Baseline.IsValid.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("baseline-exit-code: " + result.Baseline.ExitCode.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("baseline-matches: " + result.RawBaselineMatches.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("baseline-documented-exception: " + result.BaselineExceptionDocumented.ToString(CultureInfo.InvariantCulture));
            }

            foreach (OdfValidationIssue issue in report.Issues)
            {
                output.WriteLine($"{issue.Severity}: {issue.RuleId} {issue.Message}");
            }
        }

        if (results.Count > 1)
        {
            output.WriteLine("summary: files=" + summary.FileCount.ToString(CultureInfo.InvariantCulture) +
                " valid=" + summary.ValidCount.ToString(CultureInfo.InvariantCulture) +
                " invalid=" + summary.InvalidCount.ToString(CultureInfo.InvariantCulture) +
                " warnings=" + summary.WarningCount.ToString(CultureInfo.InvariantCulture) +
                " errors=" + summary.ErrorCount.ToString(CultureInfo.InvariantCulture) +
                " fatal=" + summary.FatalCount.ToString(CultureInfo.InvariantCulture));
            if (summary.BaselineFileCount > 0)
            {
                output.WriteLine("baseline-summary: files=" + summary.BaselineFileCount.ToString(CultureInfo.InvariantCulture) +
                    " mismatches=" + summary.BaselineMismatchCount.ToString(CultureInfo.InvariantCulture) +
                    " documented-exceptions=" + summary.BaselineDocumentedExceptionCount.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static void WriteValidateJson(
        TextWriter output,
        ValidateSummary summary,
        IReadOnlyList<ValidateFileResult> results)
    {
        var model = new
        {
            summary = new
            {
                fileCount = summary.FileCount,
                validCount = summary.ValidCount,
                invalidCount = summary.InvalidCount,
                infoCount = summary.InfoCount,
                warningCount = summary.WarningCount,
                errorCount = summary.ErrorCount,
                fatalCount = summary.FatalCount,
                blockingIssueCount = summary.BlockingIssueCount,
                baselineFileCount = summary.BaselineFileCount,
                baselineMismatchCount = summary.BaselineMismatchCount,
                baselineDocumentedExceptionCount = summary.BaselineDocumentedExceptionCount
            },
            files = results.Select(result => new
            {
                path = result.Path,
                documentKind = result.Report.DocumentKind.ToString(),
                detectedVersion = result.Report.DetectedVersion.ToString(),
                isValid = result.Report.IsValid,
                infoCount = result.Report.InfoCount,
                warningCount = result.Report.WarningCount,
                errorCount = result.Report.ErrorCount,
                fatalCount = result.Report.FatalCount,
                blockingIssueCount = result.Report.BlockingIssueCount,
                baseline = result.Baseline is null ? null : new
                {
                    kind = result.Baseline.Kind,
                    isValid = result.Baseline.IsValid,
                    exitCode = result.Baseline.ExitCode,
                    matchesOdfKit = result.RawBaselineMatches,
                    documentedException = result.BaselineExceptionDocumented,
                    standardOutput = result.Baseline.StandardOutput,
                    standardError = result.Baseline.StandardError
                },
                issues = result.Report.Issues.Select(issue => new
                {
                    severity = issue.Severity.ToString(),
                    ruleId = issue.RuleId,
                    message = issue.Message,
                    packagePath = issue.PackagePath,
                    xPath = issue.XPath,
                    requiredVersion = issue.RequiredVersion?.ToString(),
                    profileId = issue.ProfileId,
                    suggestedFix = issue.SuggestedFix,
                    details = issue.Details
                }).ToArray()
            }).ToArray()
        };

        output.WriteLine(JsonSerializer.Serialize(model, JsonOptions));
    }

    private static void WriteValidateCorpusText(
        TextWriter output,
        ValidateCorpusSummary summary,
        IReadOnlyList<ValidateCorpusFixtureResult> results)
    {
        foreach (ValidateCorpusFixtureResult result in results)
        {
            output.WriteLine(result.Passed ? "pass" : "fail");
            output.WriteLine("id: " + result.Fixture.Id);
            output.WriteLine("path: " + result.Path);
            output.WriteLine("expected: " + FormatExpected(result.Fixture.Expected));
            output.WriteLine("actual-valid: " + result.Report.IsValid.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("expected-kind: " + result.Fixture.Kind);
            output.WriteLine("actual-kind: " + result.Report.DocumentKind);
            output.WriteLine("kind-matches: " + result.KindMatches.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("expected-version: " + result.Fixture.Version);
            output.WriteLine("actual-version: " + FormatVersion(result.Report.DetectedVersion));
            output.WriteLine("version-matches: " + result.VersionMatches.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("profile: " + result.Fixture.Profile.Id);
            output.WriteLine("issues: " + result.Report.Issues.Count.ToString(CultureInfo.InvariantCulture));
            if (result.Baseline is not null)
            {
                output.WriteLine("baseline: " + result.Baseline.Kind);
                output.WriteLine("baseline-valid: " + result.Baseline.IsValid.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("baseline-matches: " + result.RawBaselineMatches.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("baseline-documented-exception: " + result.BaselineExceptionDocumented.ToString(CultureInfo.InvariantCulture));
            }
        }

        output.WriteLine("summary: fixtures=" + summary.FixtureCount.ToString(CultureInfo.InvariantCulture) +
            " passed=" + summary.PassedCount.ToString(CultureInfo.InvariantCulture) +
            " failed=" + summary.FailedCount.ToString(CultureInfo.InvariantCulture) +
            " kind-mismatches=" + summary.KindMismatchCount.ToString(CultureInfo.InvariantCulture) +
            " version-mismatches=" + summary.VersionMismatchCount.ToString(CultureInfo.InvariantCulture) +
            " baseline-mismatches=" + summary.BaselineMismatchCount.ToString(CultureInfo.InvariantCulture) +
            " baseline-documented-exceptions=" + summary.BaselineDocumentedExceptionCount.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteValidateCorpusJson(
        TextWriter output,
        ValidateCorpusSummary summary,
        IReadOnlyList<ValidateCorpusFixtureResult> results)
    {
        var model = new
        {
            summary = new
            {
                fixtureCount = summary.FixtureCount,
                passedCount = summary.PassedCount,
                failedCount = summary.FailedCount,
                validCount = summary.ValidCount,
                invalidCount = summary.InvalidCount,
                kindMismatchCount = summary.KindMismatchCount,
                versionMismatchCount = summary.VersionMismatchCount,
                baselineFileCount = summary.BaselineFileCount,
                baselineMismatchCount = summary.BaselineMismatchCount,
                baselineDocumentedExceptionCount = summary.BaselineDocumentedExceptionCount
            },
            fixtures = results.Select(result => new
            {
                id = result.Fixture.Id,
                path = result.Path,
                expected = FormatExpected(result.Fixture.Expected),
                passed = result.Passed,
                expectedKind = result.Fixture.Kind,
                documentKind = result.Report.DocumentKind.ToString(),
                kindMatches = result.KindMatches,
                expectedVersion = result.Fixture.Version,
                detectedVersion = result.Report.DetectedVersion.ToString(),
                version = FormatVersion(result.Report.DetectedVersion),
                versionMatches = result.VersionMatches,
                isValid = result.Report.IsValid,
                profileId = result.Fixture.Profile.Id,
                blockingIssueCount = result.Report.BlockingIssueCount,
                baseline = result.Baseline is null ? null : new
                {
                    kind = result.Baseline.Kind,
                    isValid = result.Baseline.IsValid,
                    exitCode = result.Baseline.ExitCode,
                    matchesOdfKit = result.RawBaselineMatches,
                    documentedException = result.BaselineExceptionDocumented,
                    standardOutput = result.Baseline.StandardOutput,
                    standardError = result.Baseline.StandardError
                },
                issues = result.Report.Issues.Select(issue => new
                {
                    severity = issue.Severity.ToString(),
                    ruleId = issue.RuleId,
                    message = issue.Message,
                    packagePath = issue.PackagePath,
                    xPath = issue.XPath,
                    requiredVersion = issue.RequiredVersion?.ToString(),
                    profileId = issue.ProfileId,
                    suggestedFix = issue.SuggestedFix,
                    details = issue.Details
                }).ToArray()
            }).ToArray()
        };

        output.WriteLine(JsonSerializer.Serialize(model, JsonOptions));
    }

    private static void WriteValidateCorpusMetadataOnly(
        TextWriter output,
        ValidateCorpusOptions options,
        ValidateCorpusManifest manifest,
        ValidateBaselineExceptionSet baselineExceptions)
    {
        if (options.Quiet)
        {
            return;
        }

        if (options.Format == ValidateOutputFormat.Json)
        {
            var model = new
            {
                summary = new
                {
                    metadataOnly = true,
                    fixtureCount = manifest.Fixtures.Count,
                    baselineExceptionCount = baselineExceptions.Count
                },
                fixtures = manifest.Fixtures.Select(fixture => new
                {
                    id = fixture.Id,
                    path = fixture.Path,
                    source = fixture.Source,
                    sourceUri = fixture.SourceUri,
                    license = fixture.License,
                    kind = fixture.Kind,
                    version = fixture.Version,
                    profileId = fixture.Profile.Id,
                    expected = FormatExpected(fixture.Expected),
                    roundTrip = fixture.RoundTrip
                }).ToArray()
            };

            output.WriteLine(JsonSerializer.Serialize(model, JsonOptions));
            return;
        }

        output.WriteLine("metadata-only: True");
        output.WriteLine("fixtures: " + manifest.Fixtures.Count.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("baseline-exceptions: " + baselineExceptions.Count.ToString(CultureInfo.InvariantCulture));
    }

    private static bool ShouldFail(ValidateSummary summary, ValidateFailOn failOn)
    {
        if (summary.BaselineMismatchCount > 0)
        {
            return true;
        }

        return failOn == ValidateFailOn.Warning
            ? summary.WarningCount > 0 || summary.ErrorCount > 0 || summary.FatalCount > 0
            : summary.ErrorCount > 0 || summary.FatalCount > 0;
    }

    private static string FormatVersion(OdfVersion version)
    {
        return version switch
        {
            OdfVersion.Odf10 => "1.0",
            OdfVersion.Odf11 => "1.1",
            OdfVersion.Odf12 => "1.2",
            OdfVersion.Odf13 => "1.3",
            OdfVersion.Odf14 => "1.4",
            _ => "unknown"
        };
    }

    private static string FormatExpected(ValidateCorpusExpected expected)
    {
        return expected == ValidateCorpusExpected.Valid ? "valid" : "invalid";
    }

    private enum ValidateOutputFormat
    {
        Text,
        Json
    }

    private enum ValidateFailOn
    {
        Error,
        Warning
    }

    private enum ValidateBaselineKind
    {
        None,
        OdfValidator,
        Command
    }

    private enum ValidateCorpusExpected
    {
        Valid,
        Invalid
    }

    private sealed record ValidateBaselineOptions(
        ValidateBaselineKind Baseline,
        string? BaselineJarPath,
        string? BaselineCommandPath);

    private sealed record ValidateOptions(
        string Path,
        ValidateOutputFormat Format,
        ValidateFailOn FailOn,
        OdfComplianceProfile? Profile,
        bool Recursive,
        bool Quiet,
        ValidateBaselineKind Baseline,
        string? BaselineJarPath,
        string? BaselineCommandPath,
        string? BaselineExceptionsPath)
    {
        public ValidateBaselineOptions BaselineOptions => new(Baseline, BaselineJarPath, BaselineCommandPath);
    }

    private sealed record ValidateCorpusOptions(
        string ManifestPath,
        string RootPath,
        ValidateOutputFormat Format,
        bool Quiet,
        bool MetadataOnly,
        ValidateBaselineKind Baseline,
        string? BaselineJarPath,
        string? BaselineCommandPath,
        string? BaselineExceptionsPath)
    {
        public ValidateBaselineOptions BaselineOptions => new(Baseline, BaselineJarPath, BaselineCommandPath);
    }

    private sealed record SanitizeOptions(
        string InputPath,
        string OutputPath,
        string? InputPassword,
        string? OutputPassword,
        OdfEncryptionAlgorithm EncryptionAlgorithm);

    private sealed record ValidateFileResult(
        string Path,
        OdfValidationReport Report,
        ValidateBaselineResult? Baseline,
        bool BaselineExceptionDocumented)
    {
        public bool RawBaselineMatches => Baseline is null || Baseline.IsValid == Report.IsValid;

        public bool BaselineMatches => RawBaselineMatches || BaselineExceptionDocumented;
    }

    private sealed record ValidateBaselineResult(
        string Kind,
        int ExitCode,
        bool IsValid,
        string StandardOutput,
        string StandardError);

    private sealed class ValidateSummary
    {
        public int FileCount { get; private init; }

        public int ValidCount { get; private init; }

        public int InvalidCount { get; private init; }

        public int InfoCount { get; private init; }

        public int WarningCount { get; private init; }

        public int ErrorCount { get; private init; }

        public int FatalCount { get; private init; }

        public int BlockingIssueCount { get; private init; }

        public int BaselineFileCount { get; private init; }

        public int BaselineMismatchCount { get; private init; }

        public int BaselineDocumentedExceptionCount { get; private init; }

        public static ValidateSummary Create(IReadOnlyList<ValidateFileResult> results)
        {
            return new ValidateSummary
            {
                FileCount = results.Count,
                ValidCount = results.Count(result => result.Report.IsValid),
                InvalidCount = results.Count(result => !result.Report.IsValid),
                InfoCount = results.Sum(result => result.Report.InfoCount),
                WarningCount = results.Sum(result => result.Report.WarningCount),
                ErrorCount = results.Sum(result => result.Report.ErrorCount),
                FatalCount = results.Sum(result => result.Report.FatalCount),
                BlockingIssueCount = results.Sum(result => result.Report.BlockingIssueCount),
                BaselineFileCount = results.Count(result => result.Baseline is not null),
                BaselineMismatchCount = results.Count(result => !result.BaselineMatches),
                BaselineDocumentedExceptionCount = results.Count(result => result.BaselineExceptionDocumented)
            };
        }
    }

    private sealed record ValidateCorpusFixtureResult(
        ValidateCorpusFixture Fixture,
        string Path,
        OdfValidationReport Report,
        ValidateBaselineResult? Baseline,
        bool BaselineExceptionDocumented)
    {
        public bool ClassificationMatches => Fixture.Expected == ValidateCorpusExpected.Valid
            ? Report.IsValid
            : !Report.IsValid;

        public bool KindMatches => MatchesFixtureKind(Fixture.Kind, Report.DocumentKind);

        public bool VersionMatches => string.Equals(
            Fixture.Version,
            FormatVersion(Report.DetectedVersion),
            StringComparison.OrdinalIgnoreCase);

        public bool Passed => ClassificationMatches && KindMatches && VersionMatches && BaselineMatches;

        public bool RawBaselineMatches => Baseline is null || Baseline.IsValid == Report.IsValid;

        public bool BaselineMatches => RawBaselineMatches || BaselineExceptionDocumented;

        private static bool MatchesFixtureKind(string expectedKind, OdfDocumentKind actualKind)
        {
            if (string.Equals(expectedKind, actualKind.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!OdfDocumentKindDetector.TryGetFormatByKind(actualKind, out OdfFormatInfo? format) ||
                format is null)
            {
                return false;
            }

            string normalized = expectedKind.StartsWith(".", StringComparison.Ordinal)
                ? expectedKind
                : "." + expectedKind;
            return string.Equals(normalized, format.Extension, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class ValidateCorpusSummary
    {
        public int FixtureCount { get; private init; }

        public int PassedCount { get; private init; }

        public int FailedCount { get; private init; }

        public int ValidCount { get; private init; }

        public int InvalidCount { get; private init; }

        public int KindMismatchCount { get; private init; }

        public int VersionMismatchCount { get; private init; }

        public int BaselineFileCount { get; private init; }

        public int BaselineMismatchCount { get; private init; }

        public int BaselineDocumentedExceptionCount { get; private init; }

        public static ValidateCorpusSummary Create(IReadOnlyList<ValidateCorpusFixtureResult> results)
        {
            return new ValidateCorpusSummary
            {
                FixtureCount = results.Count,
                PassedCount = results.Count(result => result.Passed),
                FailedCount = results.Count(result => !result.Passed),
                ValidCount = results.Count(result => result.Report.IsValid),
                InvalidCount = results.Count(result => !result.Report.IsValid),
                KindMismatchCount = results.Count(result => !result.KindMatches),
                VersionMismatchCount = results.Count(result => !result.VersionMatches),
                BaselineFileCount = results.Count(result => result.Baseline is not null),
                BaselineMismatchCount = results.Count(result => !result.BaselineMatches),
                BaselineDocumentedExceptionCount = results.Count(result => result.BaselineExceptionDocumented)
            };
        }
    }

    private sealed class ValidateCorpusManifest
    {
        private ValidateCorpusManifest(IReadOnlyList<ValidateCorpusFixture> fixtures)
        {
            Fixtures = fixtures;
        }

        public IReadOnlyList<ValidateCorpusFixture> Fixtures { get; }

        public static ValidateCorpusManifest Load(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("fixtures", out JsonElement fixtures) ||
                fixtures.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("corpus manifest must contain a fixtures array.");
            }

            List<ValidateCorpusFixture> parsed = [];
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement item in fixtures.EnumerateArray())
            {
                ValidateCorpusFixture fixture = ValidateCorpusFixture.Parse(item);
                if (!ids.Add(fixture.Id))
                {
                    throw new InvalidDataException("duplicate corpus fixture id: " + fixture.Id);
                }

                string normalizedPath = fixture.Path.Replace('\\', '/').Trim();
                if (!paths.Add(normalizedPath))
                {
                    throw new InvalidDataException("duplicate corpus fixture path: " + fixture.Path);
                }

                parsed.Add(fixture);
            }

            if (parsed.Count == 0)
            {
                throw new InvalidDataException("corpus manifest must contain at least one fixture.");
            }

            return new ValidateCorpusManifest(parsed);
        }
    }

    private sealed record ValidateCorpusFixture(
        string Id,
        string Path,
        string Source,
        string? SourceUri,
        string License,
        string Kind,
        string Version,
        OdfComplianceProfile Profile,
        ValidateCorpusExpected Expected,
        string RoundTrip)
    {
        public static ValidateCorpusFixture Parse(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("corpus fixture entries must be objects.");
            }

            string id = ReadRequiredString(item, "id");
            string path = ReadRequiredString(item, "path");
            string source = ReadRequiredString(item, "source");
            string? sourceUri = ReadOptionalString(item, "sourceUri");
            string license = ReadRequiredString(item, "license");
            string kind = ReadRequiredString(item, "kind");
            string version = ReadRequiredString(item, "version");
            string profileId = ReadRequiredString(item, "profile");
            string expectedValue = ReadRequiredString(item, "expected");
            string roundTrip = ReadRequiredString(item, "roundTrip");
            OdfComplianceProfile profile = OdfComplianceProfiles.Find(profileId) ??
                throw new InvalidDataException("unknown corpus fixture profile: " + profileId);
            ValidateCorpusExpected expected = ParseExpected(expectedValue);
            ValidateRoundTrip(roundTrip);
            ValidateSourceTraceability(source, sourceUri, license);
            return new ValidateCorpusFixture(id, path, source, sourceUri, license, kind, version, profile, expected, roundTrip);
        }

        private static void ValidateSourceTraceability(string source, string? sourceUri, string license)
        {
            if (IsRepoOwnedSource(source, license))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sourceUri) ||
                !Uri.TryCreate(sourceUri, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme is not ("https" or "http"))
            {
                throw new InvalidDataException("external corpus fixture requires absolute http(s) sourceUri.");
            }
        }

        private static bool IsRepoOwnedSource(string source, string license)
        {
            return string.Equals(source, "generated", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "OdfKit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(license, "generated-no-copyright", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(license, "CC0-1.0", StringComparison.OrdinalIgnoreCase);
        }

        private static ValidateCorpusExpected ParseExpected(string value)
        {
            if (string.Equals(value, "valid", StringComparison.OrdinalIgnoreCase))
            {
                return ValidateCorpusExpected.Valid;
            }

            if (string.Equals(value, "invalid", StringComparison.OrdinalIgnoreCase))
            {
                return ValidateCorpusExpected.Invalid;
            }

            throw new InvalidDataException("corpus fixture expected must be valid or invalid.");
        }

        private static void ValidateRoundTrip(string value)
        {
            if (string.Equals(value, "preserve-unknown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "semantic-equivalent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "byte-identical", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new InvalidDataException("corpus fixture roundTrip must be preserve-unknown, semantic-equivalent, or byte-identical.");
        }

        private static string ReadRequiredString(JsonElement item, string propertyName)
        {
            string? value = ReadOptionalString(item, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException("corpus fixture requires " + propertyName + ".");
            }

            return value;
        }

        private static string? ReadOptionalString(JsonElement item, string propertyName)
        {
            if (!item.TryGetProperty(propertyName, out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }

    private sealed class ValidateBaselineExceptionSet
    {
        private static readonly ValidateBaselineExceptionSet Empty = new([]);

        private readonly IReadOnlyList<ValidateBaselineExceptionEntry> entries;

        private ValidateBaselineExceptionSet(IReadOnlyList<ValidateBaselineExceptionEntry> entries)
        {
            this.entries = entries;
        }

        public int Count => entries.Count;

        public static ValidateBaselineExceptionSet Load(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Empty;
            }

            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("exceptions", out JsonElement exceptions) ||
                exceptions.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("baseline exceptions must contain an exceptions array.");
            }

            List<ValidateBaselineExceptionEntry> parsed = [];
            foreach (JsonElement item in exceptions.EnumerateArray())
            {
                parsed.Add(ValidateBaselineExceptionEntry.Parse(item));
            }

            return new ValidateBaselineExceptionSet(parsed);
        }

        public bool Contains(
            string path,
            ValidateBaselineKind baseline,
            bool odfKitIsValid,
            bool? baselineIsValid,
            string? profileId)
        {
            if (baselineIsValid is null)
            {
                return false;
            }

            foreach (ValidateBaselineExceptionEntry entry in entries)
            {
                if (entry.Matches(path, baseline, odfKitIsValid, baselineIsValid.Value, profileId))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed record ValidateBaselineExceptionEntry(
        string Path,
        ValidateBaselineKind Baseline,
        bool OdfKitIsValid,
        bool BaselineIsValid,
        string? ProfileId,
        string Reason)
    {
        public static ValidateBaselineExceptionEntry Parse(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("baseline exception entries must be objects.");
            }

            string path = ReadRequiredString(item, "path");
            string baselineValue = ReadRequiredString(item, "baseline");
            if (!TryParseBaseline(baselineValue, out ValidateBaselineKind baseline) || baseline == ValidateBaselineKind.None)
            {
                throw new InvalidDataException("baseline exception baseline must be odf-validator or command.");
            }

            bool odfKitIsValid = ReadRequiredBoolean(item, "odfKitIsValid");
            bool baselineIsValid = ReadRequiredBoolean(item, "baselineIsValid");
            string? profileId = ReadOptionalString(item, "profileId");
            string reason = ReadRequiredString(item, "reason");
            return new ValidateBaselineExceptionEntry(path, baseline, odfKitIsValid, baselineIsValid, profileId, reason);
        }

        public bool Matches(
            string candidatePath,
            ValidateBaselineKind baseline,
            bool odfKitIsValid,
            bool baselineIsValid,
            string? profileId)
        {
            return Baseline == baseline &&
                OdfKitIsValid == odfKitIsValid &&
                BaselineIsValid == baselineIsValid &&
                PathMatches(candidatePath) &&
                (string.IsNullOrWhiteSpace(ProfileId) || string.Equals(ProfileId, profileId, StringComparison.OrdinalIgnoreCase));
        }

        private bool PathMatches(string candidatePath)
        {
            string expected = NormalizePath(Path);
            string actual = NormalizePath(candidatePath);
            if (expected.IndexOf('/') < 0)
            {
                return string.Equals(expected, System.IO.Path.GetFileName(actual), StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) ||
                actual.EndsWith("/" + expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim();
        }

        private static string ReadRequiredString(JsonElement item, string propertyName)
        {
            string? value = ReadOptionalString(item, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException("baseline exception requires " + propertyName + ".");
            }

            return value;
        }

        private static string? ReadOptionalString(JsonElement item, string propertyName)
        {
            return item.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static bool ReadRequiredBoolean(JsonElement item, string propertyName)
        {
            if (!item.TryGetProperty(propertyName, out JsonElement value) ||
                value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidDataException("baseline exception requires boolean " + propertyName + ".");
            }

            return value.GetBoolean();
        }
    }
}
