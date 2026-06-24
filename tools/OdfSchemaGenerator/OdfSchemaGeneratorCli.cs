using System.Globalization;

namespace OdfKit.Tools.OdfSchemaGenerator;

/// <summary>
/// 確定性 ODF RELAX NG 結構描述中繼資料產生器的命令列進入點。
/// </summary>
public static class OdfSchemaGeneratorCli
{
    private const string Usage = "Usage: OdfSchemaGenerator [--format json|csharp|csharp-provider|dom-wrappers] [--output <file>] [--output-directory <directory>] [--class-name <name>] [--source-url <uri>] [--source-date <date>] [--version 1.0|1.1|1.2|1.3|1.4] <schema.rng>";

    private static readonly string[] SupportedVersionStrings = ["1.0", "1.1", "1.2", "1.3", "1.4"];

    /// <summary>
    /// 執行結構描述產生器命令。
    /// </summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args == null)
            throw new ArgumentNullException(nameof(args));
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (error == null)
            throw new ArgumentNullException(nameof(error));

        if (!TryParse(args, error, out GeneratorOptions? options))
        {
            return 2;
        }

        GeneratorOptions parsedOptions = options ?? throw new InvalidOperationException("Parsed generator options cannot be null.");
        SchemaMetadata metadata;
        try
        {
            metadata = new RelaxNgSchemaMetadataReader().ReadFile(parsedOptions.SchemaPath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
        {
            error.WriteLine("Schema metadata generation failed: " + ex.Message);
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(parsedOptions.SourceUrl))
        {
            metadata.Source = parsedOptions.SourceUrl;
        }

        if (!string.IsNullOrWhiteSpace(parsedOptions.SourceDate))
        {
            metadata.SourceDate = parsedOptions.SourceDate;
        }

        if (!string.IsNullOrWhiteSpace(parsedOptions.Version))
        {
            metadata.Version = parsedOptions.Version;
        }

        if (!string.IsNullOrWhiteSpace(parsedOptions.OutputDirectory))
        {
            if (!string.Equals(parsedOptions.Format, "dom-wrappers", StringComparison.OrdinalIgnoreCase))
            {
                error.WriteLine("--output-directory is only supported when --format dom-wrappers is used.");
                return 2;
            }

            string fullOutputDirectory = Path.GetFullPath(parsedOptions.OutputDirectory);
            Directory.CreateDirectory(fullOutputDirectory);
            new DomWrappersCSharpWriter().WriteToDirectory(metadata, fullOutputDirectory);
            return 0;
        }

        if (string.IsNullOrWhiteSpace(parsedOptions.OutputPath))
        {
            WriteMetadata(metadata, output, parsedOptions);
            return 0;
        }

        string fullOutputPath = Path.GetFullPath(parsedOptions.OutputPath);
        string? directory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(fullOutputPath, append: false, new UTF8EncodingWithoutBom());
        WriteMetadata(metadata, writer, parsedOptions);
        return 0;
    }

    private static bool TryParse(string[] args, TextWriter error, out GeneratorOptions? options)
    {
        string format = "json";
        string outputPath = string.Empty;
        string outputDirectory = string.Empty;
        string className = "GeneratedOdfSchemaMetadata";
        string sourceUrl = string.Empty;
        string sourceDate = string.Empty;
        string version = string.Empty;
        string? schemaPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "--format", StringComparison.Ordinal))
            {
                if (!TryReadValue(args, ref i, error, "--format", out format))
                {
                    options = null;
                    return false;
                }

                continue;
            }

            if (string.Equals(arg, "--output", StringComparison.Ordinal))
            {
                if (!TryReadValue(args, ref i, error, "--output", out outputPath))
                {
                    options = null;
                    return false;
                }

                continue;
            }

            if (string.Equals(arg, "--output-directory", StringComparison.Ordinal))
            {
                if (!TryReadValue(args, ref i, error, "--output-directory", out outputDirectory))
                {
                    options = null;
                    return false;
                }

                continue;
            }

            if (string.Equals(arg, "--class-name", StringComparison.Ordinal))
            {
                if (!TryReadValue(args, ref i, error, "--class-name", out className))
                {
                    options = null;
                    return false;
                }

                continue;
            }

            if (string.Equals(arg, "--source-url", StringComparison.Ordinal))
            {
                if (!TryReadValue(args, ref i, error, "--source-url", out sourceUrl))
                {
                    options = null;
                    return false;
                }

                continue;
            }

            if (string.Equals(arg, "--source-date", StringComparison.Ordinal))
            {
                if (!TryReadValue(args, ref i, error, "--source-date", out sourceDate))
                {
                    options = null;
                    return false;
                }

                continue;
            }

            if (string.Equals(arg, "--version", StringComparison.Ordinal))
            {
                if (!TryReadValue(args, ref i, error, "--version", out version))
                {
                    options = null;
                    return false;
                }

                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                error.WriteLine("Unsupported option: " + arg);
                error.WriteLine(Usage);
                options = null;
                return false;
            }

            if (schemaPath != null)
            {
                error.WriteLine("Only one schema path can be specified.");
                error.WriteLine(Usage);
                options = null;
                return false;
            }

            schemaPath = arg;
        }

        if (string.IsNullOrWhiteSpace(schemaPath))
        {
            error.WriteLine(Usage);
            options = null;
            return false;
        }

        if (!IsSupportedFormat(format))
        {
            error.WriteLine("Unsupported format. Use json, csharp, csharp-provider, or dom-wrappers.");
            options = null;
            return false;
        }

        if (!IsValidClassName(className))
        {
            error.WriteLine("Class name must be a valid C# identifier.");
            options = null;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sourceDate) && !IsValidSourceDate(sourceDate))
        {
            error.WriteLine("Source date must use yyyy-MM-dd format.");
            options = null;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(version) && Array.IndexOf(SupportedVersionStrings, version) < 0)
        {
            error.WriteLine("Version must be one of: " + string.Join(", ", SupportedVersionStrings));
            options = null;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(outputPath) && !string.IsNullOrWhiteSpace(outputDirectory))
        {
            error.WriteLine("Specify either --output or --output-directory, not both.");
            options = null;
            return false;
        }

        options = new GeneratorOptions(format, outputPath, outputDirectory, className, sourceUrl, sourceDate, version, schemaPath);
        return true;
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        TextWriter error,
        string optionName,
        out string value)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error.WriteLine(optionName + " requires a value.");
            error.WriteLine(Usage);
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static bool IsSupportedFormat(string format)
    {
        return string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format, "csharp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format, "csharp-provider", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format, "dom-wrappers", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidClassName(string className)
    {
        if (string.IsNullOrWhiteSpace(className) || !IsIdentifierStart(className[0]))
        {
            return false;
        }

        for (int i = 1; i < className.Length; i++)
        {
            if (!IsIdentifierPart(className[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidSourceDate(string sourceDate)
    {
        return DateTime.TryParseExact(
            sourceDate,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static bool IsIdentifierStart(char ch)
    {
        return ch == '_' ||
            char.GetUnicodeCategory(ch) is UnicodeCategory.UppercaseLetter or
                UnicodeCategory.LowercaseLetter or
                UnicodeCategory.TitlecaseLetter or
                UnicodeCategory.ModifierLetter or
                UnicodeCategory.OtherLetter or
                UnicodeCategory.LetterNumber;
    }

    private static bool IsIdentifierPart(char ch)
    {
        return IsIdentifierStart(ch) ||
            char.GetUnicodeCategory(ch) is UnicodeCategory.DecimalDigitNumber or
                UnicodeCategory.ConnectorPunctuation or
                UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.Format;
    }

    private static void WriteMetadata(SchemaMetadata metadata, TextWriter writer, GeneratorOptions options)
    {
        if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            new SchemaMetadataJsonWriter().Write(metadata, writer);
            return;
        }

        if (string.Equals(options.Format, "csharp", StringComparison.OrdinalIgnoreCase))
        {
            new SchemaMetadataCSharpWriter().Write(metadata, writer, options.ClassName);
            return;
        }

        if (string.Equals(options.Format, "dom-wrappers", StringComparison.OrdinalIgnoreCase))
        {
            new DomWrappersCSharpWriter().Write(metadata, writer);
            return;
        }

        new SchemaMetadataCSharpWriter().WriteProvider(metadata, writer, options.ClassName);
    }

    private sealed class GeneratorOptions
    {
        public GeneratorOptions(
            string format,
            string outputPath,
            string outputDirectory,
            string className,
            string sourceUrl,
            string sourceDate,
            string version,
            string schemaPath)
        {
            Format = format;
            OutputPath = outputPath;
            OutputDirectory = outputDirectory;
            ClassName = className;
            SourceUrl = sourceUrl;
            SourceDate = sourceDate;
            Version = version;
            SchemaPath = schemaPath;
        }

        public string Format { get; }

        public string OutputPath { get; }

        public string OutputDirectory { get; }

        public string ClassName { get; }

        public string SourceUrl { get; }

        public string SourceDate { get; }

        public string Version { get; }

        public string SchemaPath { get; }
    }

    private sealed class UTF8EncodingWithoutBom : System.Text.UTF8Encoding
    {
        public UTF8EncodingWithoutBom()
            : base(encoderShouldEmitUTF8Identifier: false)
        {
        }
    }
}
