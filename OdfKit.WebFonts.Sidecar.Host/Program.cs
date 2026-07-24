using OdfKit.Compliance;
using OdfKit.WebFonts.OpenType;
using OdfKit.WebFonts.Sidecar.Server;
using OdfKit.WebFonts.Worker;

namespace OdfKit.WebFonts.Sidecar.Host;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await RunAsync(args).ConfigureAwait(false);
        }
        catch (Exception)
        {
            Console.Error.WriteLine(OdfLocalizer.GetMessage("Err_WebFont_ProcessFailed"));
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        HostConfiguration configuration;
        try
        {
            configuration = HostConfiguration.Parse(args);
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
            return 2;
        }

        if (configuration.ProbeOnly)
        {
            Console.WriteLine(
                $"protocol={SidecarProtocol.Version};woff2={configuration.Server.IsWoff2Available};"
                + $"rid={configuration.Server.RuntimeIdentifier}");
            return configuration.Server.IsWoff2Available ? 0 : 3;
        }

        Directory.CreateDirectory(configuration.Server.AssetRootPath);
        if (configuration.Worker.DurableCacheDirectory is not null)
        {
            Directory.CreateDirectory(configuration.Worker.DurableCacheDirectory);
        }

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

        var subsetEngine = new ManagedOpenTypeWebFontSubsetEngine(configuration.Engine);
        await using var worker = new WebFontGenerationWorker(subsetEngine, configuration.Worker);
        var server = new WebFontSidecarServer(worker, configuration.Server);
        try
        {
            await server.RunAsync(shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            shutdown.Cancel();
            await server.DrainAsync().ConfigureAwait(false);
        }

        return 0;
    }
}
