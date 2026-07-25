using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using OdfKit.WebFonts.OpenType;
using OdfKit.WebFonts.Sidecar.Server;
using OdfKit.WebFonts.Worker;

namespace OdfKit.WebFonts.Sidecar.Host;

internal sealed class SidecarHostedService(
    HostConfiguration configuration,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    private readonly HostConfiguration _configuration = configuration;
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_configuration.Server.AssetRootPath);
        if (_configuration.Worker.DurableCacheDirectory is not null)
        {
            Directory.CreateDirectory(_configuration.Worker.DurableCacheDirectory);
        }

        using var parentMonitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Task? parentMonitor = _configuration.ParentProcessId is int parentProcessId
            ? MonitorParentProcessAsync(parentProcessId, parentMonitorCancellation.Token)
            : null;
        var subsetEngine = new ManagedOpenTypeWebFontSubsetEngine(_configuration.Engine);
        await using var worker = new WebFontGenerationWorker(subsetEngine, _configuration.Worker);
        var server = new WebFontSidecarServer(worker, _configuration.Server);
        try
        {
            await server.RunAsync(stoppingToken).ConfigureAwait(false);
            if (!stoppingToken.IsCancellationRequested)
            {
                Environment.ExitCode = 1;
                _applicationLifetime.StopApplication();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch
        {
            Environment.ExitCode = 1;
            throw;
        }
        finally
        {
            await server.DrainAsync().ConfigureAwait(false);
            parentMonitorCancellation.Cancel();
            if (parentMonitor is not null)
            {
                await parentMonitor.ConfigureAwait(false);
            }
        }
    }

    private async Task MonitorParentProcessAsync(
        int parentProcessId,
        CancellationToken cancellationToken)
    {
        try
        {
            using Process parentProcess = Process.GetProcessById(parentProcessId);
            await parentProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _applicationLifetime.StopApplication();
        }
        catch (ArgumentException)
        {
            _applicationLifetime.StopApplication();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
