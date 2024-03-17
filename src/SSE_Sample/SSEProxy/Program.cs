
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddCors(
    // allow all origins
    options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin())
);

var app = builder.Build();

app.UseCors();

app.Map("/proxy/{**path}", async (HttpContext context, string path) =>
{
    var request = context.Request;
    var httpClient = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();

    var url = ExtractTargetUrl(path, request);

    var requestMethod = request.Method switch
    {
        "GET" => HttpMethod.Get,
        "POST" => HttpMethod.Post,
        "PUT" => HttpMethod.Put,
        "DELETE" => HttpMethod.Delete,
        "PATCH" => HttpMethod.Patch,
        "HEAD" => HttpMethod.Head,
        "OPTIONS" => HttpMethod.Options,
        "TRACE" => HttpMethod.Trace,
        "CONNECT" => HttpMethod.Connect,
        _ => throw new NotSupportedException()
    };

    var requestMessage = new HttpRequestMessage(requestMethod, url);
    foreach (var header in request.Headers)
    {
        requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    if (requestMethod != HttpMethod.Get && requestMethod != HttpMethod.Head)
    {
        requestMessage.Content = new StreamContent(request.Body);
    }

    var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

    context.Response.StatusCode = (int)response.StatusCode;

    var isStreamingContent = response.Content.Headers.ContentType?.ToString().Equals("text/event-stream") ?? false;

    foreach (var header in response.Headers.Concat(response.Content.Headers))
    {
        if (CommonHeaders.ExcludedHeaders.Contains(header.Key)) { continue; }
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    await using (var stream = await response.Content.ReadAsStreamAsync(context.RequestAborted))
    {
        if (isStreamingContent)
        {
            await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        else
        {
            await stream.CopyToAsync(context.Response.Body);
        }
    }

    await context.Response.Body.FlushAsync();
})
.WithName("Proxy");

app.Run();
return;

static string ExtractTargetUrl(string s, HttpRequest httpRequest)
{
    string url1;
    string protocol;

    if (s.StartsWith("http://"))
    {
        protocol = "http";
        s = s[7..]; // Remove "http://" from the path
    }
    else if (s.StartsWith("https://"))
    {
        protocol = "https";
        s = s[8..]; // Remove "https://" from the path
    }
    else
    {
        protocol = "https"; // Use HTTPS as the default protocol
    }

    if (s.Contains(':'))
    {
        var hostAndPort = s.Split(':');
        url1 = $"{protocol}://{hostAndPort[0]}:{hostAndPort[1]}{httpRequest.QueryString}";
    }
    else
    {
        url1 = $"{protocol}://{s}:80{httpRequest.QueryString}"; // Use port 80 as the default for HTTP
    }

    return url1;
}

public static class CommonHeaders
{
    // Copied from https://github.com/microsoft/reverse-proxy/blob/51d797986b1fea03500a1ad173d13a1176fb5552/src/ReverseProxy/Forwarder/RequestUtilities.cs#L61-L83
    public static readonly HashSet<string> ExcludedHeaders = new HashSet<string>
    {
        HeaderNames.Connection,
        HeaderNames.TransferEncoding,
        HeaderNames.KeepAlive,
        HeaderNames.Upgrade,
        "Proxy-Connection",
        "Proxy-Authenticate",
        "Proxy-Authentication-Info",
        "Proxy-Authorization",
        "Proxy-Features",
        "Proxy-Instruction",
        "Security-Scheme",
        "ALPN",
        "Close",
        "Set-Cookie",
        HeaderNames.TE,
#if NET
        HeaderNames.AltSvc,
#else
            "Alt-Svc",
#endif
    };
}