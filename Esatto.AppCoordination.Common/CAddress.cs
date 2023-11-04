namespace Esatto.AppCoordination;

public readonly struct CAddress : IEquatable<CAddress>
{
    public readonly string Path;
    public readonly string Key;

    public CAddress(string address)
    {
        var iSep = address.IndexOf(':');
        if (iSep < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        this.Path = address.Substring(0, iSep);
        this.Key = address.Substring(iSep + 1);

        CPath.Validate(Path);
        CPath.Validate(Key);
    }

    public CAddress(string path, string key)
    {
        CPath.Validate(path);
        CPath.Validate(key);

        this.Path = path;
        this.Key = key;
    }

    #region Equality boilerplate
    public override string ToString() => $"{Path}:{Key}";
    public override bool Equals(object? obj) => obj is CAddress fea && Equals(fea);
    public override int GetHashCode() => Path.GetHashCode() ^ Key.GetHashCode();
    public bool Equals(CAddress other) => Path == other.Path && Key == other.Key;
    public static bool operator ==(CAddress left, CAddress right) => left.Equals(right);
    public static bool operator !=(CAddress left, CAddress right) => !(left == right);
    #endregion
}
