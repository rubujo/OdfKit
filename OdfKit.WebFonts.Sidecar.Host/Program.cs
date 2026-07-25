using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OdfKit.Compliance;
using OdfKit.WebFonts.Sidecar.Server;

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

        HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                Args = [],
                ApplicationName = "OdfKit.WebFonts.Sidecar.Host"
            });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = configuration.ServiceName;
        });
        builder.Services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton(configuration);
        builder.Services.AddHostedService<SidecarHostedService>();

        using IHost host = builder.Build();
        try
        {
            await host.RunAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }

        return Environment.ExitCode;
    }
}
