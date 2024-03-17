namespace BlazorClient.Services;

public class SSEService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private CancellationTokenSource _cts = new();

    public event Action<string> OnMessageReceived;

    public SSEService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task StartAsync()
    {
        await ReceiveEventsAsync(_cts.Token);
    }

    private async Task ReceiveEventsAsync(CancellationToken cancellationToken)
    {
        // set base address to the server
        _httpClient.BaseAddress = new Uri("http://localhost:5079");
        
        var stream = await _httpClient.GetStreamAsync("/sse", cancellationToken);
        var reader = new StreamReader(stream);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line == null)
                    break;

                if (line.StartsWith("data:"))
                {
                    var message = line.Substring(5).Trim();
                    OnMessageReceived?.Invoke(message);
                }
            }
        }
        finally
        {
            reader.Dispose();
            stream.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}