using Esatto.AppCoordination.IPC;
using Esatto.Win32.RdpDvc;
using Esatto.Win32.RdpDvc.SessionHostApi;
using Esatto.Win32.RdpDvc.TSVirtualChannels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CodeDom;
using System.ComponentModel;

namespace Esatto.AppCoordination.Coordinator;

internal class WtsSessionChangeWatcher : IHostedService
{
    private readonly ILogger Logger;
    private readonly ICoordinator Coordinator;
    private SessionChangeHandler? Handler;

    public WtsSessionChangeWatcher(ILogger<WtsSessionChangeWatcher> logger, Coordinator coordinator)
    {
        this.Logger = logger;
        this.Coordinator = coordinator;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var handler = new SessionChangeHandler();
        handler.SessionChange += Handler_SessionChange;
        this.Handler = handler;

        TryConnect();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.Handler?.Dispose();
        this.Handler = null;

        return Task.CompletedTask;
    }

    private void Handler_SessionChange(object sender, SessionChangeEventArgs e)
    {
        if (e.Event == WTS_EVENT.WTS_REMOTE_CONNECT)
        {
            TryConnect();
        }
    }

    // Noexcept
    private void TryConnect()
    {
        try
        {
            var channel = DvcServerChannel.Open(CoordinationConstants.CoordinatorRdpChannelName);
            try
            {
                _ = new WtsServerConnectionProxy(Logger, Coordinator, channel);
            }
            catch
            {
                channel.Dispose();
                throw;
            }
        }
        catch (Win32Exception wex) when (wex.HResult == -2147467259 /* E_FAIL */)
        {
            Logger.LogInformation("DVC channel not available");
        }
        catch (ChannelNotAvailableException)
        {
            Logger.LogInformation("DVC channel not available");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to coordinator");
        }
    }
}
