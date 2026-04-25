using Esatto.AppCoordination.IPC;
using Esatto.Utilities;
using Esatto.Win32.RdpDvc;
using Microsoft.Extensions.Logging;
using static Esatto.AppCoordination.Coordinator.RdpDataFormatter;

namespace Esatto.AppCoordination.Coordinator;

internal sealed class WtsServerConnectionProxy : IConnectionCallback, IDisposable
{
    private readonly ILogger Logger;
    private readonly IAsyncDvcChannel Channel;
    private readonly IConnection Connection;
    private readonly AsyncMutex SyncWrite = new();
    private readonly TaskCompletionSource<bool> StartupCompleted = new();
    private bool isShutdown;

    public WtsServerConnectionProxy(ILogger logger, ICoordinator coordinator, IAsyncDvcChannel channel)
    {
        this.Logger = logger;
        this.Channel = channel;
        this.Channel.Disconnected += Channel_Disconnected;

        PreventWritesDuringStartup();

        // NOTE: Connect calls IConnectionCallback.Inform during construction of the connection
        //       which must deal with the partially constructed this.
        this.Connection = coordinator.Connect(this);

        ReadAsync();
    }

    private async void PreventWritesDuringStartup()
    {
        using var _ = await SyncWrite.AcquireImmediateAsync().ConfigureAwait(false);
        await StartupCompleted.Task;
        await Task.Delay(500).ConfigureAwait(false);
    }

    // Called from threadpool - cannot throw.
    public void Dispose()
    {
        if (isShutdown) return;
        isShutdown = true;

        StartupCompleted.TrySetResult(true);
        Channel.Disconnected -= Channel_Disconnected;

        try { DisposableExtensions.DisposeAll(Connection, Channel); }
        catch (Exception ex)
        {
            Logger.LogInformation(ex, "Failed to close RDP Channel");
        }
    }

    private void Channel_Disconnected(object? sender, EventArgs e)
    {
        Logger.LogInformation("Disconnecting RDP Client");
        Dispose();
    }

    private async void ReadAsync()
    {
        try
        {
            while (!isShutdown)
            {
                var readPromise = Channel.ReadMessageAsync();
                StartupCompleted.TrySetResult(true);
                var message = await readPromise.ConfigureAwait(false);
                // Avoid blocking the read loop
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
        catch (OperationCanceledException) when (isShutdown)
        {
            // The underlying channel cancels pending reads during normal disconnect.
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read message from client");
            Dispose();
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
        var (sourcePath, path, key, request) = ReadInvokeRequest(data);
        // Coordinator transforms exceptions into reply messages, we should not see any exceptions here
        Connection.Invoke(sourcePath, path, key, request);
    }

    async void IConnectionCallback.Inform(string sData)
    {
        // Coordinator.Connect calls IConnectionCallback.Inform during construction of the connection
        if (Connection is null)
        {
            // Wait for the connection to be established - writes get lost?
            await Task.Delay(500).ConfigureAwait(false);
        }

        SendMessage(CreateInformRequest(sData));
    }

    // Called by coordinator (by way of ClientConnection), may throw (transformed to faults)
    void IConnectionCallback.HandleInvoke(string? sourcePath, string path, string key, string payload)
    {
        SendMessage(CreateInvokeRequest(sourcePath, path, key, payload));
    }

    // NOTE: Async VOID, so this cannot throw
    private async void SendMessage(byte[] data)
    {
        try
        {
            using var _1 = await SyncWrite.AcquireAsync().ConfigureAwait(false);
            await Channel.SendMessageAsync(data, 0, data.Length).ConfigureAwait(false);
        }
        catch (DvcChannelDisconnectedException)
        {
            Logger.LogInformation("Disconnecting RDP Client");
            Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send message to client");
            Dispose();
        }
    }
}