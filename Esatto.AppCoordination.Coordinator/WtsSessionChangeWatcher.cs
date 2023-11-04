using Esatto.AppCoordination.IPC;
using Esatto.Win32.RdpDvc;
using Esatto.Win32.RdpDvc.SessionHostApi;
using Esatto.Win32.RdpDvc.TSVirtualChannels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CodeDom;
using static Esatto.AppCoordination.Coordinator.RdpDataFormatter;

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

internal sealed class WtsServerConnectionProxy : IConnectionCallback, IDisposable
{
    private readonly ILogger Logger;
    private readonly IAsyncDvcChannel Channel;
    private readonly IConnection Connection;
    private bool isShutdown;
    private readonly object Sync = new();
    private int NextCorrelation;
    private readonly Dictionary<int, TaskCompletionSource<string>> Correlations = new();

    public WtsServerConnectionProxy(ILogger logger, ICoordinator coordinator, IAsyncDvcChannel channel)
    {
        this.Logger = logger;
        this.Connection = coordinator.Connect(this);
        this.Channel = channel;

        ReadAsync();
    }

    public void Dispose()
    {
        if (isShutdown) return;
        isShutdown = true;

        Connection.Dispose();
        try
        {
            Channel.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogInformation(ex, "Failed to close RDP Channel");
        }
    }

    private async void ReadAsync()
    {
        try
        {
            while (!isShutdown)
            {
                var message = await Channel.ReadMessageAsync();
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        DispatchMessage(message);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to process message from client");
                    }
                }, null);
            }
        }
        catch (DvcChannelDisconnectedException)
        {
            Logger.LogInformation("Disconnecting RDP Client");
            Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read message from client");
        }
    }

    private void DispatchMessage(byte[] data)
    {
        if (data.Length < 8) throw new ProtocolViolationException("Message too short");

        var command = ReadInt32(data, 0);
        switch (command)
        {
            case CMD_INFORM:
                HandleInform(data);
                break;
            case CMD_INVOKE_REQUEST:
                HandleInvokeRequest(data);
                break;
            case CMD_INVOKE_RESPONSE_RESULT:
                HandleInvokeResponse(data, true);
                break;
            case CMD_INVOKE_RESPONSE_ERROR:
                HandleInvokeResponse(data, false);
                break;
            default:
                throw new ProtocolViolationException("Unknown command");
        }
    }

    private void HandleInform(byte[] data)
    {
        string informData = ReadInformRequest(data);
        Connection.Publish(informData);
    }

    private void HandleInvokeRequest(byte[] data)
    {
        var (correlation, path, key, request) = ReadInvokeRequest(data);
        string result;
        int resultType;
        try
        {
            result = Connection.Invoke(path, key, request, out var failed);
            if (failed)
            {
                throw InvokeFaultException.FromJson(result);
            }
            resultType = CMD_INVOKE_RESPONSE_RESULT;
        }
        catch (Exception ex)
        {
            if (ex is not InvokeFaultException)
            {
                Logger.LogWarning(ex, "Exception on invoke");
            }

            result = InvokeFaultException.ToJson(ex);
            resultType = CMD_INVOKE_RESPONSE_ERROR;
        }
        SendMessage(CreateInvokeResponse(resultType, correlation, result));
    }

    private void HandleInvokeResponse(byte[] data, bool success)
    {
        var (correlation, payload) = ReadInvokeResponse(data);
        TaskCompletionSource<string> tcs;
        lock (Sync) { tcs = Correlations[correlation]; }
        if (success)
        {
            tcs.TrySetResult(payload);
        }
        else
        {
            tcs.TrySetException(InvokeFaultException.FromJson(payload));
        }
    }

    void IConnectionCallback.Inform(string sData)
    {
        SendMessage(CreateInformRequest(sData));
    }

    string IConnectionCallback.Invoke(string path, string key, string payload, out bool failed)
    {
        var tcsResult = new TaskCompletionSource<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var reg = cts.Token.Register(() => tcsResult.TrySetCanceled());
        int correlation;
        lock (Sync)
        {
            correlation = NextCorrelation++;
            Correlations.Add(correlation, tcsResult);
        }
        try
        {
            SendMessage(CreateInvokeRequest(correlation, path, key, payload));
            var result = tcsResult.Task.GetAwaiter().GetResult();
            failed = false;
            return result;
        }
        catch (Exception ex)
        {
            failed = true;
            return InvokeFaultException.ToJson(ex);
        }
        finally
        {
            lock (Sync)
            {
                Correlations.Remove(correlation);
            }
        }
    }

    private async void SendMessage(byte[] data)
    {
        try
        {
            await Channel.SendMessageAsync(data, 0, data.Length);
        }
        catch (DvcChannelDisconnectedException)
        {
            Logger.LogInformation("Disconnecting RDP Client");
            Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send message to client");
        }
    }
}