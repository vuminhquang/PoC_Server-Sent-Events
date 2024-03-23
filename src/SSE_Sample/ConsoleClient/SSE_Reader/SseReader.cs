namespace ConsoleClient.SSE_Reader;

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;

public class SseReader : IDisposable, IAsyncEnumerable<string>
{
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _cts;
    private readonly StreamReader _reader;

    public SseReader(string url)
    {
        _cts = new CancellationTokenSource();
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/event-stream");

        var response = _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _cts.Token).Result;
        response.EnsureSuccessStatusCode();
        _reader = new StreamReader(response.Content.ReadAsStream(), Encoding.UTF8);
    }

    public async IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var buffer = new StringBuilder();

        while (!_cts.Token.IsCancellationRequested && await _reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line == string.Empty)
            {
                var eventData = buffer.ToString().Trim();
                buffer.Clear();

                if (!string.IsNullOrEmpty(eventData))
                {
                    yield return eventData;
                }
            }
            else
            {
                buffer.AppendLine(line);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _reader.Dispose();
        _httpClient.Dispose();
    }
}