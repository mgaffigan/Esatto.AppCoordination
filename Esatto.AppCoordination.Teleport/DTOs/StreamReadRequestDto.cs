using System.Text.Json.Serialization;

namespace Esatto.AppCoordination.Teleport;

class StreamReadRequestDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Close { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Offset { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Length { get; set; }

    public StreamReadRequestDto()
    {
        // for deserialization
    }

    public StreamReadRequestDto(bool close)
    {
        if (!close) throw new ArgumentOutOfRangeException(nameof(close));
        this.Close = close;
    }

    public StreamReadRequestDto(int offset, int length)
    {
        this.Offset = offset;
        this.Length = length;

        Validate();
    }

    public void Validate()
    {
        if (Close)
        {
            if (Offset != 0 || Length != 0) throw new InvalidOperationException("Cannot set close and read at the same time");
        }
        else
        {
            if (Offset < 0) throw new ArgumentOutOfRangeException(nameof(Offset));
            if (Length < 0) throw new ArgumentOutOfRangeException(nameof(Length));
        }
    }
}
