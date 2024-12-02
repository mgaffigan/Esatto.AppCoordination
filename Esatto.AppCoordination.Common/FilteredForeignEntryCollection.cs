using Esatto.Utilities;
using System.Collections.ObjectModel;

namespace Esatto.AppCoordination;

public class FilteredForeignEntryCollection : ReadOnlyObservableCollection<ForeignEntry>, IDisposable
{
    private readonly ForeignEntryCollection Parent;
    internal readonly Func<string, bool> Predicate;
    private readonly ObservableCollection<ForeignEntry> BaseList;

    internal FilteredForeignEntryCollection(ForeignEntryCollection parent, Func<string, bool> predicate)
        : this(parent, predicate, new ObservableCollection<ForeignEntry>())
    {
        // nop, just here to create the base list
    }

    private FilteredForeignEntryCollection(ForeignEntryCollection parent, Func<string, bool> predicate, ObservableCollection<ForeignEntry> baseList)
        : base(baseList)
    {
        this.Parent = parent;
        this.Predicate = predicate;
        this.BaseList = baseList;
        Invalidate();
    }

    public void Dispose()
    {
        this.Parent.RemoveProjection(this);
    }

    internal void Invalidate()
    {
        BaseList.MakeEqualTo(this.Parent.ToList(Predicate));
    }
}