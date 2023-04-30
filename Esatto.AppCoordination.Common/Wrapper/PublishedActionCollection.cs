using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    public sealed class PublishedActionCollection : ObservableCollection<PublishedAction>
    {
        private readonly CoordinatedApp App;
        private readonly ForeignEntity Entity;

        internal PublishedActionCollection(CoordinatedApp app, ForeignEntity entity)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app), "Contract assertion not met: app != null");
            }
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "Contract assertion not met: entity != null");
            }

            this.App = app;
            this.Entity = entity;
        }

        protected override void InsertItem(int index, PublishedAction item)
        {
            App.MainThread.Assert();

            item.Attach(App, Entity);

            // item.Attach can premept, collection could have been modified to make
            // index invalid
            base.InsertItem(Math.Min(Count, index), item);
        }

        protected override void RemoveItem(int index)
        {
            throw new NotSupportedException("Use PublishedAction.Dispose to remove");
        }

        internal void RemoveItemInternal(PublishedAction action)
        {
            App.MainThread.Assert();
            int idx = this.IndexOf(action);
            if (idx < 0)
            {
                throw new InvalidOperationException("Not added to list");
            }

            base.RemoveItem(idx);
        }

        protected override void SetItem(int index, PublishedAction item)
        {
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            throw new NotSupportedException();
        }

        protected override void ClearItems()
        {
            throw new NotSupportedException();
        }
    }
}
