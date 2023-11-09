using Esatto.Utilities;
using System.Text.Json;

namespace Esatto.AppCoordination.Teleport;

class FileStreamProvider : IDisposable
{
    private readonly int MaxReadSize;
    private readonly PublishedEntry StreamEntity;
    private readonly long Length;
    private readonly FileStream Stream;
    public bool IsDisposed { get; private set; }

    public FileStreamProvider(CoordinatedApp app, UniqueDisposable<FileStream> fileStream, long length)
    {
        this.MaxReadSize = TeleportSettings.Instance.MaxMemoryFileSize;
        this.Length = length;
        this.StreamEntity = app.Publish(StreamKey, new()
        {
            { "Length", Length },
            { "MaxReadSize", MaxReadSize }
        }, Read);
        this.Stream = fileStream.Take();
    }

    public event EventHandler? Disposed;
    public event EventHandler? Touched;
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        StreamEntity.Dispose();
        Stream.Dispose();
        Disposed?.Invoke(this, EventArgs.Empty);
    }

    private string Read(string arg)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(FileStreamProvider));

        var req = JsonSerializer.Deserialize<StreamReadRequestDto>(arg)
            ?? throw new ArgumentException("Invalid request");
        req.Validate();

        if (req.Close)
        {
            Dispose();
            return string.Empty;
        }
        else
        {
            if (req.Offset < 0 || req.Length < 0 || req.Length > MaxReadSize || req.Offset + req.Length > Length)
            {
                throw new ArgumentException("Invalid request");
            }

            var buffer = new byte[req.Length];
            Stream.Position = req.Offset;

            int oBuff = 0;
            while (oBuff < req.Length)
            {
                oBuff += Stream.Read(buffer, oBuff, buffer.Length - oBuff);
            }
            Touched?.Invoke(this, EventArgs.Empty);
            return Convert.ToBase64String(buffer);
        }
    }

    public string StreamKey { get; } = CPath.Suffix(TeleportConstants.StreamKeyPrefix, Guid.NewGuid().ToString("n"));
}
