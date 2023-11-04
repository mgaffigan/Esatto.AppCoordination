using Esatto.AppCoordination;
using Esatto.AppCoordination.IPC;
using Esatto.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public CoordinatedApp App { get; }
        public ForeignEntry Entity { get; }
        public FilteredForeignEntryCollection Commands { get; }
        private ObservableCollection<MyPublishedAction> Actions { get; }

        public OpenEntityVM(ForeignEntry entity, CoordinatedApp app)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "Contract assertion not met: entity != null");
            }

            this.App = app;
            this.Entity = entity;
            entity.Removed += (_, _) => ClearActions();

            this.Actions = new ObservableCollection<MyPublishedAction>();
        }

        private class MyPublishedAction : IDisposable
        {
            public string ActionName { get; }
            public PublishedEntry Action { get; }

            public MyPublishedAction(string key, string action, CoordinatedApp app)
            {
                this.ActionName = action;
                this.Action = app.Publish(CPath.Suffix(key, "command"),
                    new EntryValue() { { "DisplayName", action } }, OnInvoked);
            }

            private string OnInvoked(string arg)
            {
                var window = Application.Current.MainWindow;
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }
                window.Activate();

                if (ActionName.EndsWith(" 3"))
                {
                    throw new InvalidOperationException("A failure occurred because you asked for it");
                }

                MessageBox.Show(Application.Current.MainWindow, $"Invoked command {ActionName} with param '{arg}'", "Thing");
                return "OK";
            }

            public void Dispose() => Action.Dispose();
        }

        public void AddAction(string action)
        {
            Actions.Add(new MyPublishedAction(Entity.Key, action, App));
        }

        public void ClearActions()
        {
            Actions.DisposeAll();
        }
    }
}
