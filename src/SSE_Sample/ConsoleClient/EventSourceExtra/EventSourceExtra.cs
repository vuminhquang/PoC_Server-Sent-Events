using System.Runtime.CompilerServices;
using System.Text;

namespace ConsoleClient.SSE_Reader;
    public enum ReadyState
    {
        Initializing = -1,
        Connecting = 0,
        Open = 1,
        Closed = 2
    }

    public class CustomEventArgs : EventArgs
    {
        public string? Type { get; set; }
        public string? Data { get; set; }
        public string? Id { get; set; }
        public int? Retry { get; set; }
    }

    public class StateChangedEventArgs : EventArgs
    {
        public ReadyState ReadyState { get; init; }
    }

    public record EventSourceExtraOptions
    {
        public Dictionary<string, string> Headers { get; init; } = new();
        public string Payload { get; init; } = string.Empty;
        public string Method { get; init; } = "GET"; // Default to GET unless a payload is provided
        public bool Debug { get; init; }
    }

    public class EventSourceExtra : IAsyncEnumerable<CustomEventArgs>
    {
        private readonly string _url;
        private readonly Dictionary<string, string> _headers;
        private readonly string _payload;
        private readonly string _method;
        private readonly bool _debug;

        private readonly HttpClient _httpClient;
        private ReadyState _readyState = ReadyState.Initializing;

        // Events
        public event EventHandler<CustomEventArgs>? EventReceived;
        public event EventHandler<StateChangedEventArgs>? StateChanged;

        public EventSourceExtra(string url, HttpClient httpClient, EventSourceExtraOptions? options = null)
        {
            _url = url;
            _httpClient = httpClient;
            _headers = options?.Headers ?? new Dictionary<string, string>();
            _payload = options?.Payload ?? string.Empty;
            _method = options?.Method ?? (string.IsNullOrEmpty(_payload) ? "GET" : "POST");
            _debug = options?.Debug ?? false;
        }

        protected virtual void OnEventReceived(CustomEventArgs e)
        {
            EventReceived?.Invoke(this, e);
        }

        protected virtual void OnStateChanged(StateChangedEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        private void SetReadyState(ReadyState state)
        {
            _readyState = state;
            OnStateChanged(new StateChangedEventArgs { ReadyState = _readyState });
        }
        
        public async Task Stream(CancellationToken cancellationToken = default)
        {
            // Initiating the streaming process and handling events.
            try
            {
                // Optionally, you can add other setup or logging before starting to process events
                if (_debug)
                {
                    Console.WriteLine("Starting to stream events.");
                }

                await foreach (var eventArgs in StreamEvents(cancellationToken))
                {
                    if (_debug)
                    {
                        Console.WriteLine("Event received: " + eventArgs.Type);
                    }

                    OnEventReceived(eventArgs);
                }
            }
            catch (Exception ex)
            {
                // Optionally, you can add error handling here
                if (_debug)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                }
                throw;
            }
            finally
            {
                // Optionally, you can add cleanup code here
                if (_debug)
                {
                    Console.WriteLine("Streaming has ended.");
                }
            }
        }

        public async IAsyncEnumerator<CustomEventArgs> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            await foreach (var eventArgs in StreamEvents(cancellationToken))
            {
                yield return eventArgs;
            }
        }

        private async IAsyncEnumerable<CustomEventArgs> StreamEvents([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            SetReadyState(ReadyState.Connecting);

            var request = new HttpRequestMessage(new HttpMethod(_method), _url);
            foreach (var header in _headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
            if (!string.IsNullOrEmpty(_payload))
            {
                request.Content = new StringContent(_payload, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            SetReadyState(ReadyState.Open);

            var chunk = "";
            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;

                if (_debug) Console.WriteLine("line data: " + line);

                if (string.IsNullOrEmpty(line)) continue;

                if (":".StartsWith(line)) continue;  // Skip comment lines

                chunk += line + "\n";

                if (string.IsNullOrEmpty(chunk)) continue;
                var eventArgs = ParseEvent(chunk);
                chunk = "";

                yield return eventArgs;
            }

            SetReadyState(ReadyState.Closed);
        }

        private CustomEventArgs ParseEvent(string chunk)
        {
            var eventData = new CustomEventArgs();
            var dataBuilder = new StringBuilder();
            var lines = chunk.Split('\n');

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;

                var index = line.IndexOf(':');
                if (index == -1) continue;

                var field = line[..index].Trim();
                var value = line[(index + 1)..].Trim();

                switch (field.ToLower())
                {
                    case "event":
                        eventData.Type = value;
                        break;
                    case "data":
                        dataBuilder.AppendLine(value);
                        break;
                    case "id":
                        eventData.Id = value;
                        break;
                    case "retry":
                        if (int.TryParse(value, out var retryValue))
                        {
                            eventData.Retry = retryValue;
                        }

                        break;
                }
            }

            eventData.Data = dataBuilder.ToString().Trim();
            return eventData;
        }
    }
