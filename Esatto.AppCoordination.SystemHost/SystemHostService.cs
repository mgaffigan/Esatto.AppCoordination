using Esatto.Win32.Com;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.SystemHost
{
    internal sealed class SystemHostService : ServiceBase
    {
        private readonly Timer tmrAutoStop;
        private readonly Dispatcher Dispatcher;
        private ClassObjectRegistration ProcessWatcherRegistration;
        private ManagementEventWatcher ProcessStartWatcher;

        public const string SERVICE_NAME = "esAppCoordSystemHost";

        public SystemHostService()
        {
            this.ServiceName = SERVICE_NAME;
            this.tmrAutoStop = new Timer(tmrAutoStop_Tick, null,
#if DEBUG
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)
#else
                TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15)
#endif
                );
            this.Dispatcher = new Dispatcher();
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            try
            {
                ProcessStartWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
                ProcessStartWatcher.EventArrived += new EventArrivedEventHandler(startWatch_EventArrived);
                ProcessStartWatcher.Start();

                this.ProcessWatcherRegistration = new ClassObjectRegistration(typeof(ProcessWatcher).GUID,
                    ComInterop.CreateClassFactoryFor(() => this.Dispatcher), CLSCTX.LOCAL_SERVER, REGCLS.MULTIPLEUSE | REGCLS.SUSPENDED);
                ComInterop.CoResumeClassObjects();
            }
            catch (Exception ex)
            {
                Log.Error($"Could not start esAppCoordSystemHost\r\n{ex}", 1001);
                throw;
            }
        }

        protected override void OnStop()
        {
            base.OnStop();

            try
            {
                ProcessStartWatcher.Stop();
                ProcessWatcherRegistration.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error($"Could not stop esAppCoordSystemHost\r\n{ex}", 1002);
                throw;
            }
        }

        private void startWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var processId = (int)(uint)e.NewEvent.Properties["ProcessID"].Value;
            var sessionId = (int)(uint)e.NewEvent.Properties["SessionID"].Value;
            var name = (string)e.NewEvent.Properties["ProcessName"].Value;

            Dispatcher.Evaluate(processId, name, sessionId);
        }

        private void tmrAutoStop_Tick(object state)
        {
            // since the COM proxies are garbage collected, we have to force a GC to see if they are all dead
            GC.Collect();

            if (!Dispatcher.CheckAlive())
            {
                Stop();
            }
        }
    }
}
