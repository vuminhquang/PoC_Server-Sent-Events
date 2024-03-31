// See https://aka.ms/new-console-template for more information

using ConsoleClient.SSE_Reader;

Console.WriteLine("Hello, World!");

var cts = new CancellationTokenSource();

// using var sseReader = new SseReader("http://localhost:5079/sse");
using var sseReader = new SseReader("http://localhost:5046/proxy/http://localhost:5079/sse");

var readTask = ReadEventsAsync(sseReader, cts.Token);

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await readTask;
return;

static async Task ReadEventsAsync(SseReader sseReader, CancellationToken cancellationToken)
{
    try
    {
        await foreach (var eventData in sseReader.WithCancellation(cancellationToken))
        {
            Console.WriteLine(eventData);
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Reading events canceled.");
    }
}