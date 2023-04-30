using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Esatto.AppCoordination.DemoClient
{
    internal sealed class OpenEntityVM : NotificationObject
    {
        public ForeignEntity Entity { get; }

        public OpenEntityVM(ForeignEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "Contract assertion not met: entity != null");
            }

            this.Entity = entity;
        }

        private class MyPublishedAction : PublishedAction
        {
            public MyPublishedAction(string action)
                : base(new EntityActionMetadata(action, "Demo", null))
            {
                if (string.IsNullOrEmpty(action))
                {
                    throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(action)", nameof(action));
                }
            }

            protected override void OnInvoked()
            {
                var window = Application.Current.MainWindow;
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }
                window.Activate();

                MessageBox.Show(Application.Current.MainWindow, $"Invoked command {Metadata.Name}", "Thing");
            }
        }

        public void AddAction(string action)
        {
            Entity.Actions.Add(new MyPublishedAction(action));
        }

        public void ClearActions()
        {
            foreach (var action in Entity.Actions.ToArray())
            {
                Entity.Actions.Remove(action);
            }
        }
    }
}
