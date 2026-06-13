using System.Globalization;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Cli;

/// <summary>
/// 提供 OdfKit 命令列工具的命令解析與執行流程。
/// </summary>
public static class OdfKitCli
{
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
                "info" => Info(args, output, error),
                "metadata" => Metadata(args, output, error),
                "convert-flat" => ConvertFlat(args, output, error),
                "pack" => Pack(args, output, error),
                _ => UnknownCommand(args[0], error)
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException)
        {
            error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static int Validate(string[] args, TextWriter output, TextWriter error)
    {
        if (!RequireArity(args, 2, "validate file.odt", error))
        {
            return 2;
        }

        OdfValidationReport report = OdfValidator.Validate(args[1]);
        output.WriteLine(report.IsValid ? "valid" : "invalid");
        output.WriteLine("kind: " + report.DocumentKind);
        output.WriteLine("version: " + FormatVersion(report.DetectedVersion));
        output.WriteLine("issues: " + report.Issues.Count.ToString(CultureInfo.InvariantCulture));
        foreach (OdfValidationIssue issue in report.Issues)
        {
            output.WriteLine($"{issue.Severity}: {issue.RuleId} {issue.Message}");
        }

        return report.BlockingIssueCount > 0 ? 1 : 0;
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

    private static int UnknownCommand(string command, TextWriter error)
    {
        error.WriteLine("unknown command: " + command);
        return 2;
    }

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("usage: odfkit <command> [arguments]");
        output.WriteLine("commands:");
        output.WriteLine("  validate file.odt");
        output.WriteLine("  info file.ods");
        output.WriteLine("  convert-flat input.odt output.fodt");
        output.WriteLine("  pack input.fodt output.odt");
        output.WriteLine("  metadata file.odt");
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
}
