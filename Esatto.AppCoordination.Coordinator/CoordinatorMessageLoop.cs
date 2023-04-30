using Esatto.Win32.Com;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Esatto.AppCoordination
{
    internal sealed class CoordinatorMessageLoop : ApplicationContext
    {
        private readonly Coordinator Coordinator;
        private CoordinatorHookHost CoordinatorHooks;
        private readonly ClassObjectRegistration CoordinatorRegistration;

        public CoordinatorMessageLoop()
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            this.Coordinator = new Coordinator();

            // load scraping providers
            try
            {
                this.CoordinatorHooks = new CoordinatorHookHost(Coordinator);
                this.Coordinator.NewDeploymentCreated += (_1, args) => CoordinatorHooks.NotifyNewDeployment(args.Coordinator);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to initialize hooks\r\n{ex}", 1095);
            }

            // register COM objects
            this.CoordinatorRegistration = new ClassObjectRegistration(typeof(Coordinator).GUID,
                ComInterop.CreateStaClassFactoryFor(() => this.Coordinator), 
                CLSCTX.LOCAL_SERVER, REGCLS.MULTIPLEUSE | REGCLS.SUSPENDED);
            ComInterop.CoResumeClassObjects();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                this.CoordinatorHooks.Dispose();
                this.CoordinatorRegistration.Dispose();
            }
        }
    }
}
