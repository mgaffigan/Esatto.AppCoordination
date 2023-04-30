using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esatto.AppCoordination.IPC;
using System.Diagnostics;
using System.ComponentModel;

namespace Esatto.AppCoordination
{
    public sealed class ForeignEntity : INotifyPropertyChanged
    {
        internal readonly IOpenEntity Entity;
        private CoordinatedApp parent;

        public EntityIdentity Identity { get; }

        private readonly EntityIdentityRecord RawIdentity;
        public Guid EntityUid => RawIdentity.Uid;
        public TimeSpan FocusedTime => FocusedStopwatch.Elapsed;
        public bool IsFocused { get; private set; }
        private readonly Stopwatch FocusedStopwatch;

        public PublishedActionCollection Actions { get; }

        public event EventHandler GotFocus;
        public event EventHandler LostFocus;
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Closed;

        // time critical, on COM call stack
        internal ForeignEntity(IOpenEntity entity, EntityIdentityRecord identity, CoordinatedApp parent)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "Contract assertion not met: entity != null");
            }
            if (!(identity.AppUid != default))
            {
                throw new ArgumentException("Contract assertion not met: identity.AppUid != default(Guid)", nameof(identity));
            }
            if (!(identity.Uid != default))
            {
                throw new ArgumentException("Contract assertion not met: identity.Uid != default(Guid)", nameof(identity));
            }
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent), "Contract assertion not met: parent != null");
            }
            if (!(parent.AppUid != identity.AppUid))
            {
                throw new ArgumentException("Contract assertion not met: parent.AppUid != identity.AppUid", nameof(parent));
            }

            this.Entity = entity;
            this.Identity = new EntityIdentity(identity);
            this.RawIdentity = identity;
            this.parent = parent;
            this.FocusedStopwatch = new Stopwatch();

            this.Actions = new PublishedActionCollection(parent, this);
        }

        // time critical, on COM call stack
        internal void NotifyGotFocus()
        {
            this.FocusedStopwatch.Start();
            this.IsFocused = true;

            this.parent.DispatchCallback(() => OnGotFocus());
        }

        // on DispatchCallback already
        internal void NotifyClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        private void OnGotFocus()
        {
            RaisePropertyChanged(nameof(IsFocused));
            GotFocus?.Invoke(this, EventArgs.Empty);
        }

        // time critical, on COM call stack
        internal void NotifyLostFocus()
        {
            this.FocusedStopwatch.Stop();
            this.IsFocused = false;

            this.parent.DispatchCallback(() => OnLostFocus());
        }

        private void OnLostFocus()
        {
            RaisePropertyChanged(nameof(IsFocused));
            RaisePropertyChanged(nameof(FocusedTime));
            LostFocus?.Invoke(this, EventArgs.Empty);
        }

        private void RaisePropertyChanged(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(propertyName)", nameof(propertyName));
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
