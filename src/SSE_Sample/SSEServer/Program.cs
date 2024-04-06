using System.Text;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var bld = WebApplication.CreateBuilder();

// enable HTTP2
bld.WebHost.ConfigureKestrel(options =>
{
    // configure the endpoint to use HTTP2, backwards compatible with HTTP1.1
    options.ListenLocalhost(5079, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2 | HttpProtocols.Http1;
    });
    
});

bld.Services
    //.AddFastEndpoints()
    .AddCors(
        // allow all origins
        options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin())
    )
    .SwaggerDocument(); //define a swagger document

var app = bld.Build();
app.UseCors();
// app.UseSwaggerGen();

// force stop all connections when the app is stopped:
// 1. Create Global Cancellation Token
// 2. Register the cancellation token to the app's lifetime
// 3. When the app is stopping, cancel the token
var cts = new CancellationTokenSource();
app.Lifetime.ApplicationStopping.Register(() =>
{
    cts.Cancel();
});

app.MapGet("/sse", async context =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    // if api_key in the query is not '123', then return 401
    // if (context.Request.Query["api_key"] != "123")
    // {
    //     context.Response.StatusCode = 401;
    //     return;
    // }
    
    await context.Response.Body.WriteAsync("data: Connected\n\n"u8.ToArray());
    
    // merge the request's cancellation token with the global cancellation token
    var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, cts.Token).Token;
    
    // set to stop the connection after 30 seconds, using the merged cancellation token
    // create new cancellation token which will be cancelled after 30 seconds
    var timeoutCts = new CancellationTokenSource();
    timeoutCts.CancelAfter(30000);
    var timeoutToken = timeoutCts.Token;
    
    // create a linked token source with the merged cancellation token and the timeout token
    var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken);
    
    while (!linkedTokenSource.IsCancellationRequested)
    {
        // Simulate sending messages every 5 seconds
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {DateTime.Now}\n\n"));
        try
        {
            await Task.Delay(2000, linkedTokenSource.Token);

        }
        catch (TaskCanceledException)
        {
            // if the task is cancelled, then the loop is stopped
            break;
        }
    }
    
    // if the loop is stopped, then the connection is stopped
    // send a message to the client
    await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("data: [DONE]\n\n"));
    //context.Response.Body.Close();
})
.WithOpenApi();


app.Run();