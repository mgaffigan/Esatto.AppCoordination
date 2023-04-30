using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Esatto.AppCoordination.DemoClient
{
    internal sealed class MyEntityActionVM : NotificationObject
    {
        private readonly ForeignAction Action;

        public string Name => Action.Name;

        public MyEntityActionVM(ForeignAction action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action), "Contract assertion not met: action != null");
            }

            this.Action = action;
        }

        public void Invoke()
        {
            try
            {
                Action.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to invoke\r\n" + ex.ToString());
            }
        }
    }
}
