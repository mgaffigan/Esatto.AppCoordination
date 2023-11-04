using System.Text;

namespace Esatto.AppCoordination;

public static class CPath
{
    public const string Empty = "/";

    public static bool Contains(string path, string node)
    {
        Validate(path);
        if (node.Contains('/')) throw new ArgumentOutOfRangeException(nameof(node));
        return path.IndexOf($"/{node}/", StringComparison.Ordinal) >= 0;
    }

    public static string From(params string[] nodes)
    {
        var sb = new StringBuilder(255);
        sb.Append('/');
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].Contains('/'))
            {
                throw new ArgumentOutOfRangeException(nameof(nodes));
            }

            sb.Append(nodes[i]);
            sb.Append('/');
        }
        return sb.ToString();
    }
    public static string[] Split(string path) => path.Split('/');

    public static (string first, string rest) PopFirst(string path)
    {
        Validate(path);
        if (path.Length < 3 /* /a/ */)
        {
            throw new ArgumentOutOfRangeException("Path must contain at least one element");
        }
        var iSeg2 = path.IndexOf('/', 2 /* /a */);
        var first = path.Substring(1 /* skip the leading '/' */, iSeg2 - 1 /* leading '/' */);
        var rest = path.Substring(iSeg2);
        return (first, rest);
    }

    public static string Prefix(string first, string path)
    {
        if (first.Contains('/')) throw new ArgumentOutOfRangeException(nameof(first));
        Validate(path);

        return $"/{first}{path}";
    }

    public static string Suffix(string path, string last)
    {
        ValidateNode(last);
        Validate(path);

        return $"{path}{last}/";
    }

    private static void ValidateNode(string node)
    {
        if (node.Contains(':')) throw new ArgumentOutOfRangeException(nameof(node));
        if (node.Contains('/')) throw new ArgumentOutOfRangeException(nameof(node));
    }

    public static void Validate(string path)
    {
        if (path.Length < 1)
        {
            throw new ArgumentNullException(nameof(path));
        }
        if (path[0] != '/')
        {
            throw new ArgumentException("Path must start with /", nameof(path));
        }
        if (path[path.Length - 1] != '/')
        {
            throw new ArgumentException("Path must end with /", nameof(path));
        }
    }
}
