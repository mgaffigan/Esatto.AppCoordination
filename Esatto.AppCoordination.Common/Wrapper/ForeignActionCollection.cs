using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    public sealed class ForeignActionCollection : INotifyCollectionChanged, IReadOnlyCollection<ForeignAction>, INotifyPropertyChanged, IObservable<ForeignAction>
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly Dictionary<Guid, ForeignAction> Actions;
        private ThreadAssert MainThread => Parent.MainThread;
        private readonly CoordinatedApp Parent;
        private readonly PublishedEntity Entity;

        private Subject<ForeignAction> Subject;

        internal ForeignActionCollection(PublishedEntity entity, CoordinatedApp app)
        {
            this.Entity = entity;
            this.Parent = app;
            this.Actions = new Dictionary<Guid, ForeignAction>();
        }

        #region IList implementations

        public int Count
        {
            get
            {
                MainThread.Assert();

                return Actions.Count;
            }
        }

        public IEnumerator<ForeignAction> GetEnumerator()
        {
            MainThread.Assert();

            var copy = Actions.Values.ToArray();
            return ((IEnumerable<ForeignAction>)copy).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        // time critical, on COM call stack
        public ForeignAction this[Guid entityUid]
        {
            get
            {
                MainThread.Assert();

                if (entityUid == Guid.Empty)
                {
                    throw new ArgumentNullException(nameof(entityUid));
                }

                ForeignAction entity;
                Actions.TryGetValue(entityUid, out entity);
                return entity;
            }
        }

        // time critical, on COM call stack
        internal void Add(ForeignAction foreignAction)
        {
            if (foreignAction == null)
            {
                throw new ArgumentNullException(nameof(foreignAction), "Contract assertion not met: foreignAction != null");
            }
            MainThread.Assert();

            Actions.Add(foreignAction.ActionUid, foreignAction);

            Parent.DispatchCallback(() =>
            {
                Subject?.OnNext(foreignAction);
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, foreignAction));
                RaisePropertyChanged(nameof(Count));
            });
        }

        // time critical, on COM call stack
        internal void Remove(ForeignAction foreignAction)
        {
            if (foreignAction == null)
            {
                throw new ArgumentNullException(nameof(foreignAction), "Contract assertion not met: foreignAction != null");
            }
            MainThread.Assert();

            if (!(Actions.Remove(foreignAction.ActionUid)))
            {
                throw new ArgumentException("Contract assertion not met: Actions.Remove(foreignAction.ActionUid)", nameof(foreignAction));
            }

            Parent.DispatchCallback(() =>
            {
                foreignAction.NotifyClosed();
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, foreignAction));
                RaisePropertyChanged(nameof(Count));
            });
        }

        private void RaisePropertyChanged(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(propertyName)", nameof(propertyName));
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public IDisposable Subscribe(IObserver<ForeignAction> observer)
        {
            MainThread.Assert();

            bool isNew = Subject == null;
            if (isNew)
            {
                Subject = new Subject<ForeignAction>();
            }
            var resp = Subject.Subscribe(observer);
            var existing = this.ToArray();
            if (isNew)
            {
                // you may ask yourself, why are we running this in a callback?
                // I have no idea, it just seemed appropriate at the time.
                Parent.DispatchCallback(() =>
                {
                    foreach (var entity in existing)
                    {
                        Subject.OnNext(entity);
                    }
                });
            }
            return resp;
        }
    }
}
