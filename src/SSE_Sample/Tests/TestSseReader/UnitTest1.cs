using System.Reflection;
using System.Text;
using ConsoleClient.SSE_Reader;
using Moq;
using Moq.Protected;

namespace TestSseReader
{
    [TestFixture]
    public class StandardEventSourceClientTests
    {
        private Mock<HttpClient> _mockHttpClient;
        private StandardEventSourceClient _standardEventSourceClient;
        private string _testUrl = "http://test.com";
        private EventSourceExtraOptions _options;

        [SetUp]
        public void Setup()
        {
            _mockHttpClient = new Mock<HttpClient>();
            _options = new EventSourceExtraOptions
            {
                Headers = new Dictionary<string, string>
                {
                    { "Authorization", "Bearer token" }
                },
                Payload = "",
                Method = "GET",
                Debug = true
            };
            
            _standardEventSourceClient = new StandardEventSourceClient(_testUrl, _mockHttpClient.Object, _options);
        }
        
        [Test]
        public void ShouldInvokeStateChangedEventOnStateChange()
        {
            var wasCalled = false;
            _standardEventSourceClient.StateChanged += (sender, args) =>
            {
                wasCalled = true;
                Assert.That(args.ReadyState, Is.EqualTo(ReadyState.Connecting));
            };

            _standardEventSourceClient.GetType().GetMethod("SetReadyState", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(_standardEventSourceClient, new object[] { ReadyState.Connecting });

            Assert.That(wasCalled, Is.True);
        }
        
        [Test]
        public async Task ShouldHandleIncomingEvents()
        {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var response = new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent("data: {\"message\": \"Hello\"}\n\n", Encoding.UTF8, "application/json")
            };

            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var client = new HttpClient(mockHttpMessageHandler.Object);
            _standardEventSourceClient = new StandardEventSourceClient(_testUrl, client, _options);

            var eventReceived = false;
            _standardEventSourceClient.EventReceived += (sender, args) =>
            {
                eventReceived = true;
                Assert.That(args.Data, Is.EqualTo("{\"message\": \"Hello\"}"));
            };

            await _standardEventSourceClient.Stream(CancellationToken.None);

            Assert.That(eventReceived, Is.True);
        }
    }
}