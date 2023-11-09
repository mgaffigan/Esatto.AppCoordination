using Esatto.AppCoordination.IPC;
using Esatto.Win32.RdpDvc;
using Microsoft.Extensions.Logging;
using static Esatto.AppCoordination.Coordinator.RdpDataFormatter;

namespace Esatto.AppCoordination.Coordinator;

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
        this.Channel = channel;
        this.Connection = coordinator.Connect(this);

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

    async void IConnectionCallback.Inform(string sData)
    {
        if (Connection is null)
        {
            // Wait for the connection to be established - writes get lost?
            await Task.Delay(500).ConfigureAwait(false);
        }

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