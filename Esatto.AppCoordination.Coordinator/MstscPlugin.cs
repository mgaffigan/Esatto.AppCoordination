using Esatto.AppCoordination.IPC;
using Esatto.Win32.RdpDvc;
using Esatto.Win32.RdpDvc.ClientPluginApi;
using Esatto.Win32.RdpDvc.SessionHostApi;
using Esatto.Win32.RdpDvc.TSVirtualChannels;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Esatto.AppCoordination.Coordinator;

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
[Guid(CoordinationConstants.MstscPluginClsid)]
internal class MstscPlugin : WtsPluginBase
{
    private readonly ILogger Logger;
    private readonly ICoordinator Coordinator;
    private readonly WtsListenerCallback Listener;

    public MstscPlugin(Coordinator coordinator, ILogger<MstscPlugin> logger)
    {
        this.Logger = logger;
        this.Coordinator = coordinator;
        this.Listener = new WtsListenerCallback(CoordinationConstants.CoordinatorRdpChannelName, CreateChannel, logger);
    }

    private void CreateChannel(IAsyncDvcChannel channel)
    {
        try
        {
            _ = new WtsServerConnectionProxy(Logger, Coordinator, channel);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create channel");
            channel.Dispose();
        }
    }

    protected override void Initialize(IWTSVirtualChannelManager pChannelMgr)
    {
        try
        {
            pChannelMgr.CreateListener(Listener.ChannelName, 0, Listener);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create listener");
        }
    }
}
