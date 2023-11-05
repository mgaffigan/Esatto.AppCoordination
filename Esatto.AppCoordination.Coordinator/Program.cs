using Esatto.Win32.Com;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Esatto.AppCoordination.Coordinator;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<Coordinator>();
                services.AddSingleton<MstscPlugin>();
                services.AddHostedService<WtsSessionChangeWatcher>();

                // if we have statics, register them
                var staticConfig = StaticEntriesConfiguration.FromRegistry();
                if (staticConfig.Entries.Count > 0)
                {
                    services.AddSingleton(staticConfig);
                    services.AddHostedService<StaticEntriesPublisher>();
                }
            })
            .Build();

        var programLogger = host.Services.GetRequiredService<ILogger<Program>>();
        try
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            await host.StartAsync();
            try
            {
                using var coordinatorReg = new ClassObjectRegistration(typeof(Coordinator).GUID,
                    ComInterop.CreateStaClassFactoryFor(host.Services.GetRequiredService<Coordinator>),
                    CLSCTX.LOCAL_SERVER, REGCLS.MULTIPLEUSE | REGCLS.SUSPENDED);
                using var mstscReg = new ClassObjectRegistration(typeof(MstscPlugin).GUID,
                    ComInterop.CreateClassFactoryFor(host.Services.GetRequiredService<MstscPlugin>),
                    CLSCTX.LOCAL_SERVER, REGCLS.MULTIPLEUSE | REGCLS.SUSPENDED);
                ComInterop.CoResumeClassObjects();

                Application.Run();
            }
            finally
            {
                await host.StopAsync();
            }
        }
        catch (Exception ex)
        {
            programLogger.LogCritical(ex, "Unhandled exception");
            throw;
        }
    }
}