using System.Text.Json;

namespace Esatto.AppCoordination.Teleport;

class FileStreamEntryReader : Stream
{
    private readonly ForeignEntry Provider;
    public int MaxReadSize { get; }

    private readonly int _Length;

    public FileStreamEntryReader(ForeignEntry prov)
    {
        this.Provider = prov;
        this.MaxReadSize = (int)(prov.Value["MaxReadSize"]
            ?? throw new ArgumentOutOfRangeException(nameof(prov), "No MaxReadSize specified"));
        this._Length = (int)(prov.Value["Length"]
            ?? throw new ArgumentOutOfRangeException(nameof(prov), "No Length specified"));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Provider.Invoke(JsonSerializer.Serialize(new StreamReadRequestDto(close: true)));
        }
        base.Dispose(disposing);
    }

    public override long Length => _Length;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Position { get; set; }


    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length - offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        if (newPos < 0 || newPos > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        return Position = newPos;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(buffer));
        }
        if (count > MaxReadSize)
        {
            count = MaxReadSize;
        }
        var pos = Position;
        if (pos + count > Length)
        {
            count = (int)(Length - pos);
        }
        if (count == 0)
        {
            return 0;
        }

        var resp = Provider.Invoke(JsonSerializer.Serialize(new StreamReadRequestDto((int)pos, count)));
        var data = Convert.FromBase64String(resp);
        if (data.Length != count)
        {
            throw new InvalidOperationException("Provider returned unexpected length");
        }
        data.CopyTo(buffer, offset);
        Position += count;
        return count;
    }

    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
