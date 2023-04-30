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
    public sealed class ForeignEntityCollection : INotifyCollectionChanged, IReadOnlyCollection<ForeignEntity>, INotifyPropertyChanged, IObservable<ForeignEntity>
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly Dictionary<Guid, ForeignEntity> Entities;
        private ThreadAssert MainThread => Parent.MainThread;
        private readonly CoordinatedApp Parent;

        private Subject<ForeignEntity> Subject;

        public ForeignEntity FocusedEntity { get; private set; }
        public event EventHandler FocusedEntityChanged;

        internal ForeignEntityCollection(CoordinatedApp parent)
        {
            this.Parent = parent;
            this.Entities = new Dictionary<Guid, ForeignEntity>();
        }

        #region IList implementations

        public int Count
        {
            get
            {
                MainThread.Assert();

                return Entities.Count;
            }
        }

        public IEnumerator<ForeignEntity> GetEnumerator()
        {
            MainThread.Assert();

            var copy = Entities.Values.ToArray();
            return ((IEnumerable<ForeignEntity>)copy).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        // time critical, on COM call stack
        public ForeignEntity this[Guid entityUid]
        {
            get
            {
                MainThread.Assert();

                if (entityUid == Guid.Empty)
                {
                    throw new ArgumentNullException(nameof(entityUid));
                }

                ForeignEntity entity;
                Entities.TryGetValue(entityUid, out entity);
                return entity;
            }
        }

        // time critical, on COM call stack
        internal void Add(ForeignEntity foreignEntity)
        {
            if (foreignEntity == null)
            {
                throw new ArgumentNullException(nameof(foreignEntity), "Contract assertion not met: foreignEntity != null");
            }
            MainThread.Assert();

            Entities.Add(foreignEntity.EntityUid, foreignEntity);

            Parent.DispatchCallback(() =>
            {
                Subject?.OnNext(foreignEntity);
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, foreignEntity));
                RaisePropertyChanged(nameof(Count));
            });
        }

        // time critical, on COM call stack
        internal void Remove(ForeignEntity foreignEntity)
        {
            if (foreignEntity == null)
            {
                throw new ArgumentNullException(nameof(foreignEntity), "Contract assertion not met: foreignEntity != null");
            }
            MainThread.Assert();

            if (FocusedEntity == foreignEntity)
            {
                Focus(null);
            }

            if (!(Entities.Remove(foreignEntity.EntityUid)))
            {
                throw new ArgumentException("Contract assertion not met: Entities.Remove(foreignEntity.EntityUid)", nameof(foreignEntity));
            }

            foreach (var action in foreignEntity.Actions.ToArray())
            {
                action.NotifyEntityClosed();
            }

            Parent.DispatchCallback(() =>
            {
                foreignEntity.NotifyClosed();
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, foreignEntity));
                RaisePropertyChanged(nameof(Count));
            });
        }

        // time critical, on COM call stack
        internal void Focus(ForeignEntity focusedEntity)
        {
            // focused entity can be null if the focused entity is closed
            //Contract.Requires(focusedEntity != null);

            foreach (var otherVm in Entities.Values.Where(e => e != focusedEntity && e.IsFocused))
            {
                otherVm.NotifyLostFocus();
            }

            focusedEntity?.NotifyGotFocus();
            this.FocusedEntity = focusedEntity;
            Parent.DispatchCallback(() =>
            {
                RaisePropertyChanged(nameof(FocusedEntity));
                FocusedEntityChanged?.Invoke(this, EventArgs.Empty);
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

        public IDisposable Subscribe(IObserver<ForeignEntity> observer)
        {
            MainThread.Assert();

            bool isNew = Subject == null;
            if (isNew)
            {
                Subject = new Subject<ForeignEntity>();
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
