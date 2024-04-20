namespace ConsoleClient.SSE_Reader;

public record EventSourceExtraOptions
{
    public Dictionary<string, string> Headers { get; init; } = new();
    public string Payload { get; init; } = string.Empty;
    public string Method { get; init; } = "GET"; // Default to GET unless a payload is provided
    public bool Debug { get; init; }
}