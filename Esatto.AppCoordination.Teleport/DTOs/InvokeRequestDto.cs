namespace Esatto.AppCoordination.Teleport;

class InvokeRequestDto
{
    public string Registration { get; set; }
    public string? Url { get; set; }
    public InvokeFileReferenceDto? File { get; set; }
    public int Depth { get; set; }

#nullable disable
    public InvokeRequestDto()
    {
        // for deserialization
    }
#nullable restore

    public InvokeRequestDto(string scheme, string url, int depth)
    {
        this.Registration = scheme + ":";
        this.Url = url ?? throw new ArgumentNullException(nameof(url));
        this.Depth = depth;
    }

    public InvokeRequestDto(string ext, InvokeFileReferenceDto file, int depth)
    {
        this.Registration = "." + ext;
        this.File = file ?? throw new ArgumentNullException(nameof(file));
        this.Depth = depth;
    }

    public void Validate()
    {
        if (Url is null && File is null)
        {
            throw new InvalidOperationException("No target specified");
        }
        if (Url is not null && File is not null)
        {
            throw new InvalidOperationException("Multiple targets specified");
        }
        if (Depth < 0)
        {
            throw new InvalidOperationException("Depth must not be negative");
        }
    }
}
