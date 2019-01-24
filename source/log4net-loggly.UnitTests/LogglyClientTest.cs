using System;
using System.IO;
using System.Net;
using System.Text;
using FluentAssertions;
using log4net.loggly;
using Moq;
using Xunit;

namespace log4net_loggly.UnitTests
{
    public class LogglyClientTest
    {
        private MemoryStream _messageStream;
        private string _usedUrl;
        private Mock<WebRequest> _webRequestMock;

        public LogglyClientTest()
        {
            _webRequestMock = new Mock<WebRequest>();
            _webRequestMock.Setup(x => x.GetRequestStream()).Returns(() =>
            {
                _messageStream = new MemoryStream();
                return _messageStream;
            });
            _webRequestMock.Setup(x => x.GetResponse())
                .Returns(() =>
                {
                    return Mock.Of<WebResponse>();
                });

            LogglyClient.WebRequestFactory = (c, u) =>
            {
                _usedUrl = u;
                return _webRequestMock.Object;
            };
        }

        [Theory]
        [InlineData(SendMode.Single, "inputs")]
        [InlineData(SendMode.Bulk, "bulk")]
        public void SendsToProperUrlBasedOnMode(SendMode mode, string expectedPath)
        {
            var config = new Config
            {
                RootUrl = "https://logs01.loggly.test",
                CustomerToken = "customer-token",
                Tag = "tag1,tag2",
                UserAgent = "user-agent",
                SendMode = mode
            };
            var client = new LogglyClient(config);

            client.Send(new[] { "test message" }, 1);

            _usedUrl.Should().Be($"https://logs01.loggly.test/{expectedPath}/customer-token/tag/tag1,tag2,user-agent");
        }

        [Fact]
        public void DoesNotRetrySendWhenTokenIsInvalid()
        {
            var forbiddenResponse = new Mock<HttpWebResponse>();
            forbiddenResponse.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Forbidden);

            _webRequestMock.Setup(x => x.GetResponse())
                .Throws(new WebException("test-error", null, WebExceptionStatus.ProtocolError,
                    forbiddenResponse.Object));

            var client = new LogglyClient(new Config());

            client.Send(new[] { "test message" }, 1);

            _webRequestMock.Verify(x => x.GetResponse(), Times.Once, "Invalid token should not be retried");
        }

        [Fact]
        public void RetriesSendWhenErrorOccurs()
        {
            var notFoundResponse = new Mock<HttpWebResponse>();
            notFoundResponse.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.NotFound);

            _webRequestMock.Setup(x => x.GetResponse())
                .Throws(new WebException("test-error", null, WebExceptionStatus.ProtocolError,
                    notFoundResponse.Object));

            var config = new Config
            {
                MaxSendRetries = 3
            };
            var client = new LogglyClient(config);

            client.Send(new[] { "test message" }, 1);

            _webRequestMock.Verify(x => x.GetResponse(), Times.Exactly(config.MaxSendRetries + 1),
                "Generic send error should be retried");
        }

        [Fact]
        public void SendsCorrectData()
        {
            var client = new LogglyClient(new Config());

            client.Send(new[] { "Test message to be sent" }, 1);

            var message = Encoding.UTF8.GetString(_messageStream.ToArray());
            message.Should().Be("Test message to be sent");
        }

        [Fact]
        public void SendsOnlyProperPartOfMessageBuffer()
        {
            var client = new LogglyClient(new Config());

            client.Send(new[] { "message 1", "message 2", "message 3" }, 2);

            var message = Encoding.UTF8.GetString(_messageStream.ToArray());
            message.Should().Be($"message 1{Environment.NewLine}message 2");
        }

        [Fact]
        public void CreatesProperWebRequest()
        {
            var config = new Config
            {
                TimeoutInSeconds = 12,
                UserAgent = "test-agent"
            };
            var request = (HttpWebRequest)LogglyClient.CreateWebRequest(config, "http://test-url");

            request.Should().BeEquivalentTo(new
            {
                Method = "POST",
                ReadWriteTimeout = 12000,
                Timeout = 12000,
                ContentType = "application/json",
                UserAgent = "test-agent",
                KeepAlive = true
            }, x => x.ExcludingMissingMembers());
        }
    }
}
