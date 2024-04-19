using System.Text;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var bld = WebApplication.CreateBuilder();

// enable HTTP2
bld.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5079, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2 | HttpProtocols.Http1;
    });
});

bld.Services
    .AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin()))
    .SwaggerDocument();

var app = bld.Build();
app.UseCors();

var cts = new CancellationTokenSource();
app.Lifetime.ApplicationStopping.Register(() => cts.Cancel());

async Task HandleSseRequest(HttpContext context)
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    await context.Response.Body.WriteAsync("data: Connected\n\n"u8.ToArray());

    var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, cts.Token).Token;
    var timeoutCts = new CancellationTokenSource();
    timeoutCts.CancelAfter(30000);
    var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

    while (!linkedTokenSource.IsCancellationRequested)
    {
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {DateTime.Now}\n\n"));
        try
        {
            await Task.Delay(2000, linkedTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
            break;
        }
    }

    await context.Response.Body.WriteAsync("data: [DONE]\n\n"u8.ToArray());
}

app.MapGet("/sse", HandleSseRequest).WithOpenApi();
app.MapPost("/sse", HandleSseRequest).WithOpenApi();

app.Run();