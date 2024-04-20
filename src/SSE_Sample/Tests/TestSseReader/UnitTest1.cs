using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ConsoleClient.SSE_Reader.Tests
{
    [TestFixture]
    public class EventSourceExtraTests
    {
        private const string TestUrl = "https://example.com/sse";

        [Test]
        public async Task Stream_ValidUrl_RaisesEventReceivedAndStateChangedEvents()
        {
            // Arrange
            var httpClient = new HttpClient();
            var options = new EventSourceExtraOptions
            {
                Headers = new Dictionary<string, string> { { "Authorization", "Bearer token" } },
                Payload = "{\"key\":\"value\"}",
                Method = "POST",
                Debug = true
            };

            var eventSourceExtra = new EventSourceExtra(TestUrl, httpClient, options);

            var eventReceivedRaised = false;
            var stateChangedRaised = false;

            eventSourceExtra.EventReceived += (sender, args) =>
            {
                eventReceivedRaised = true;
                Assert.AreEqual("message", args.Type);
                Assert.AreEqual("Hello, world!", args.Data);
            };

            eventSourceExtra.StateChanged += (sender, args) =>
            {
                stateChangedRaised = true;
                Assert.AreEqual(ReadyState.Open, args.ReadyState);
            };

            // Act
            await eventSourceExtra.Stream();

            // Assert
            Assert.IsTrue(eventReceivedRaised);
            Assert.IsTrue(stateChangedRaised);
        }

        [Test]
        public void Stream_InvalidUrl_ThrowsException()
        {
            // Arrange
            var httpClient = new HttpClient();
            var invalidUrl = "https://invalid-url.com/sse";
            var eventSourceExtra = new EventSourceExtra(invalidUrl, httpClient);

            // Act & Assert
            Assert.ThrowsAsync<HttpRequestException>(async () => await eventSourceExtra.Stream());
        }

        [Test]
        public void SetReadyState_ChangesReadyStateAndRaisesStateChangedEvent()
        {
            // Arrange
            var httpClient = new HttpClient();
            var eventSourceExtra = new EventSourceExtra(TestUrl, httpClient);

            var stateChangedRaised = false;
            var expectedReadyState = ReadyState.Closed;

            eventSourceExtra.StateChanged += (sender, args) =>
            {
                stateChangedRaised = true;
                Assert.AreEqual(expectedReadyState, args.ReadyState);
            };

            // Act
            eventSourceExtra.SetReadyState(expectedReadyState);

            // Assert
            Assert.IsTrue(stateChangedRaised);
            Assert.AreEqual(expectedReadyState, eventSourceExtra.ReadyState);
        }

        [Test]
        public void ParseEvent_ValidChunk_ReturnsCustomEventArgs()
        {
            // Arrange
            var httpClient = new HttpClient();
            var eventSourceExtra = new EventSourceExtra(TestUrl, httpClient);

            var chunk = "event: message\ndata: Hello, world!\nid: 123\nretry: 5000\n\n";

            // Act
            var result = eventSourceExtra.ParseEvent(chunk);

            // Assert
            Assert.AreEqual("message", result.Type);
            Assert.AreEqual("Hello, world!", result.Data);
            Assert.AreEqual("123", result.Id);
            Assert.AreEqual(5000, result.Retry);
        }
    }
}