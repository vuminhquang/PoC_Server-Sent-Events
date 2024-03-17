Sure, here's a generic version of the minimal API that can proxy both Server-Sent Events (SSE) and regular API requests, while preserving the HTTP method, path, and query parameters:

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapMethods("/proxy/{**path}", async (HttpRequest request, HttpClient httpClient, string path) =>
{
    var url = $"https://{path}{request.QueryString}";

    var requestMessage = new HttpRequestMessage(request.Method, url);
    foreach (var header in request.Headers)
    {
        requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
    {
        var requestBody = await request.ReadFromJsonAsync<object>();
        if (requestBody != null)
        {
            requestMessage.Content = JsonContent.Create(requestBody);
        }
    }

    var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
    await response.Content.LoadIntoBufferAsync();

    return Results.StatusCode(
        (int)response.StatusCode,
        response.Content.Headers.ContentType?.MediaType == "text/event-stream"
            ? Results.Stream(response.Content.ReadAsStream(), response.Content.Headers.ContentType?.MediaType)
            : Results.Content(await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentType?.MediaType)
    );
})
.WithName("Proxy");

app.Run();
```

Here's how it works:

1. The `MapMethods` method is used to define a new route handler for the `/proxy/{**path}` endpoint, which accepts all HTTP methods.
2. The route handler method takes three parameters: `HttpRequest`, `HttpClient` (injected from the dependency injection container), and `path` (which captures the remaining path segments after `/proxy/`).
3. Inside the route handler method, the `url` is constructed by prepending `https://` to the `path` parameter and appending the query string from the incoming request.
4. A new `HttpRequestMessage` is created with the constructed `url` and the HTTP method from the incoming request.
5. All headers from the incoming request are copied to the `HttpRequestMessage`.
6. If the HTTP method is not `GET` or `HEAD`, the request body is read from the incoming request and added to the `HttpRequestMessage` as JSON content.
7. The `HttpRequestMessage` is sent using the `HttpClient` instance with `HttpCompletionOption.ResponseHeadersRead`, which allows the response body to be streamed.
8. The response content is loaded into a buffer using `response.Content.LoadIntoBufferAsync()` to ensure the entire response is available for streaming.
9. The response is returned to the client using the `Results.StatusCode` method, with the appropriate content type and response body.
   - If the response content type is `text/event-stream` (indicating an SSE response), the response body is streamed using `Results.Stream`.
   - For all other response types, the response body is returned as a string using `Results.Content`.

With this implementation, you can make requests to the `/proxy/example.com/api/resource?param=value` endpoint, and it will proxy the API request to `https://example.com/api/resource?param=value`, preserving the HTTP method, headers, request body (for non-GET/HEAD requests), and query parameters.

If the target URL returns Server-Sent Events, the implementation will automatically detect the `text/event-stream` content type and stream the response body to the client in real-time.

Note that this implementation assumes that the target URL starts with `https://`. If you need to support other protocols (e.g., `http://`), you can modify the `url` construction accordingly.