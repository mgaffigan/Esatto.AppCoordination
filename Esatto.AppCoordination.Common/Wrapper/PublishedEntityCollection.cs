using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    public sealed class PublishedEntityCollection : ObservableCollection<PublishedEntity>
    {
        private readonly CoordinatedApp App;

        internal PublishedEntityCollection(CoordinatedApp parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent), "Contract assertion not met: parent != null");
            }

            this.App = parent;
        }

        protected override void InsertItem(int index, PublishedEntity item)
        {
            App.MainThread.Assert();

            item.Attach(App);

            // item.Attach can premept, collection could have been modified to make
            // index invalid
            base.InsertItem(Math.Min(Count, index), item);
        }

        protected override void RemoveItem(int index)
        {
            throw new NotSupportedException("Use PublishedEntity.Dispose to remove");
        }

        internal void RemoveItemInternal(PublishedEntity entity)
        {
            App.MainThread.Assert();
            int idx = this.IndexOf(entity);
            if (idx < 0)
            {
                throw new InvalidOperationException("Not added to list");
            }

            base.RemoveItem(idx);
        }

        protected override void SetItem(int index, PublishedEntity item)
        {
            throw new NotSupportedException();
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
