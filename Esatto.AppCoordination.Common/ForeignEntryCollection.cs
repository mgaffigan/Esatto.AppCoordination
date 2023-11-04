﻿using Esatto.AppCoordination.IPC;
using Esatto.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Esatto.AppCoordination;

public class ForeignEntryCollection : IReadOnlyList<ForeignEntry>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly Dictionary<CAddress, ForeignEntry> Entries = new();
    private readonly List<FilteredForeignEntryCollection> Projections = new();
    private readonly object SyncRoot = new();
    private CoordinatedApp Parent;

    internal ForeignEntryCollection(CoordinatedApp parent)
    {
        this.Parent = parent;
    }

    // nothrow, called on syncCtx
    internal void Update(EntrySet es)
    {
        // Avoid calling user code from within SyncRoot
        var newEntries = new List<ForeignEntry>();
        var updates = new List<ForeignEntry>();
        var updatedProj = new HashSet<FilteredForeignEntryCollection>();
        List<ForeignEntry> removed;

        lock (SyncRoot)
        {
            removed = this.Entries.Values.ToList();
            foreach (var kvp in es.Entries)
            {
                var address = new CAddress(kvp.Key);
                if (this.Entries.TryGetValue(address, out var fe))
                {
                    if (fe.Update(kvp.Value))
                    {
                        updates.Add(fe);
                        updatedProj.AddRange(Projections.Where(p => p.Predicate(fe.Key)));
                    }
                    removed.Remove(fe);
                }
                else
                {
                    this.Entries.Add(address, fe = new ForeignEntry(Parent, address, kvp.Value));
                    newEntries.Add(fe);
                    updatedProj.AddRange(Projections.Where(p => p.Predicate(fe.Key)));
                }
            }

            foreach (var fe in removed)
            {
                this.Entries.Remove(fe.Address);
                updatedProj.AddRange(Projections.Where(p => p.Predicate(fe.Key)));
            }
        }

        if (removed.Any())
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed));
        }
        foreach (var fe in removed)
        {
            fe.OnRemoved();
        }
        foreach (var fe in updates)
        {
            fe.OnValueChanged();
        }
        if (newEntries.Any())
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newEntries));
        }
        foreach (var proj in updatedProj)
        {
            proj.Invalidate();
        }
        if (removed.Any() || newEntries.Any())
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }
    }

    public int Count { get { lock (SyncRoot) { return Entries.Count; } } }

    public ForeignEntry this[int index] { get { lock (SyncRoot) { return Entries.Values.ElementAt(index); } } }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<ForeignEntry> GetEnumerator() => ToList().GetEnumerator();
    public IReadOnlyList<ForeignEntry> ToList()
    {
        lock (SyncRoot)
        {
            return Entries.Values.ToList();
        }
    }

    public IReadOnlyList<ForeignEntry> ToList(Func<string, bool> predicate)
    {
        lock (SyncRoot)
        {
            return Entries.Values.Where(e => predicate(e.Key)).ToList();
        }
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public FilteredForeignEntryCollection CreateProjection(string key)
        => CreateProjection(k => key == k);
    public FilteredForeignEntryCollection CreateProjection(Func<string, bool> predicate)
    {
        lock (SyncRoot)
        {
            var proj = new FilteredForeignEntryCollection(this, predicate);
            Projections.Add(proj);
            return proj;
        }
    }

    internal void RemoveProjection(FilteredForeignEntryCollection proj)
    {
        lock (SyncRoot)
        {
            Projections.Remove(proj);
        }
    }
}

public class FilteredForeignEntryCollection : ObservableCollection<ForeignEntry>, IDisposable
{
    private readonly ForeignEntryCollection Parent;
    internal readonly Func<string, bool> Predicate;

    internal FilteredForeignEntryCollection(ForeignEntryCollection parent, Func<string, bool> predicate)
    {
        this.Parent = parent;
        this.Predicate = predicate;
        Invalidate();
    }

    public void Dispose()
    {
        this.Parent.RemoveProjection(this);
    }

    internal void Invalidate()
    {
        this.MakeEqualTo(this.Parent.ToList(Predicate));
    }
}