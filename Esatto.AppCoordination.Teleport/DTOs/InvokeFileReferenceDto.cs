namespace Esatto.AppCoordination.Teleport;

class InvokeFileReferenceDto
{
    public string FileName { get; set; }
    public byte[]? Contents { get; set; }
    public string? StreamKey { get; set; }

#nullable disable
    public InvokeFileReferenceDto()
    {
        // for deserialization
    }
#nullable restore

    private InvokeFileReferenceDto(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentNullException(nameof(filename));

        this.FileName = filename;
    }

    public InvokeFileReferenceDto(string filename, byte[] contents)
        : this(filename)
    {
        this.Contents = contents ?? throw new ArgumentNullException(nameof(contents));
    }

    public InvokeFileReferenceDto(string filename, string streamKey)
        : this(filename)
    {
        this.StreamKey = streamKey ?? throw new ArgumentNullException(nameof(streamKey));
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FileName)) throw new ArgumentNullException(nameof(FileName));
        if (Contents == null && string.IsNullOrWhiteSpace(StreamKey))
        {
            throw new ArgumentNullException(nameof(Contents));
        }
    }
}
