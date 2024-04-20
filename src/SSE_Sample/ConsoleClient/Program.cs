// See https://aka.ms/new-console-template for more information

using ConsoleClient.SSE_Reader;
using System.Net.Http;

string url = "http://localhost:5079/sse";

Console.WriteLine("Starting SSE Client...");

var options = new EventSourceExtraOptions
{
    Headers = new Dictionary<string, string>
    {
        { "Accept", "text/event-stream" }
    },
    Debug = true,
    Method = "POST"
};

using var httpClient = new HttpClient();

// Assuming "http://example.com/stream" is your SSE provider
var eventSource = new EventSourceExtra(url, httpClient, options);
        
// Subscribe to events
eventSource.EventReceived += OnEventReceived;
eventSource.StateChanged += OnStateChanged;

// Start streaming
await eventSource.Stream();

// Keep the console alive until the user decides to quit
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
return;

static void OnEventReceived(object sender, CustomEventArgs e)
{
    Console.WriteLine($"Received Event: {e.Type}");
    Console.WriteLine($"Data: {e.Data}");
    Console.WriteLine($"ID: {e.Id}");
    if (e.Retry.HasValue)
    {
        Console.WriteLine($"Retry: {e.Retry.Value}");
    }
}

static void OnStateChanged(object sender, StateChangedEventArgs e)
{
    Console.WriteLine($"State Changed: {e.ReadyState}");
}