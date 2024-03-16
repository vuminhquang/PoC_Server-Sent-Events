# Create a Minimal API Endpoint
## In your Program.cs file, create a new minimal API endpoint for sending SSE messages:
```
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/sse", async context =>
{
    context.Response.Headers.Add("Content-Type", "text/event-stream");
    context.Response.Headers.Add("Cache-Control", "no-cache");
    context.Response.Headers.Add("Connection", "keep-alive");

    await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("data: Connected\n\n"));

    var cancellationToken = context.RequestAborted;

    while (!cancellationToken.IsCancellationRequested)
    {
        // Simulate sending messages every 5 seconds
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {DateTime.Now}\n\n"));
        await Task.Delay(5000, cancellationToken);
    }
});

app.Run();
```
## In this example, we're creating a new minimal API endpoint /sse that directly sends Server-Sent Events to clients. Here's what's happening:
1. We set the appropriate response headers for SSE (Content-Type, Cache-Control, and Connection).
2. We send an initial "Connected" event to the client.
3. We enter a loop that simulates sending messages every 5 seconds. In a real-world scenario, you would replace this with your actual logic for generating messages.
4. We use the context.RequestAborted cancellation token to gracefully stop sending events when the client disconnects.

# Consuming SSE Messages on the Client
## On the client-side, you can use the EventSource API to consume Server-Sent Events from the API endpoint:
```
<!DOCTYPE html>
<html>
<head>
    <title>SSE Example</title>
</head>
<body>
    <h1>Server-Sent Events</h1>
    <div id="messages"></div>

    <script>
        const source = new EventSource('http://localhost:5000/sse');

        source.addEventListener('message', function(event) {
            const data = event.data;
            const messagesDiv = document.getElementById('messages');
            const messageElement = document.createElement('p');
            messageElement.textContent = data;
            messagesDiv.appendChild(messageElement);
        });

        source.addEventListener('open', function(event) {
            console.log('SSE connection opened');
        });

        source.addEventListener('error', function(event) {
            console.error('SSE connection error:', event);
        });
    </script>
</body>
</html>
```
## In this example, we're using the EventSource API to establish a connection to the /sse endpoint. We listen for the message event, which is triggered whenever a new message is received from the server. We then append the received message to a <div> element on the page.
We also listen for the open and error events to handle the connection state.
With this approach, the client doesn't need to establish a persistent connection or use SignalR. Instead, it directly consumes Server-Sent Events from the minimal API endpoint. The API endpoint is responsible for generating and sending the SSE messages to connected clients.
Note that this example uses a simple HTML file for demonstration purposes. In a real-world scenario, you would likely consume the SSE messages in your Blazor application using JavaScript interop or other techniques.