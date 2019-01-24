using FluentAssertions;
using Xunit;

namespace log4net_loggly.UnitTests
{
    public class IntegrationTestWithoutLayout : IntegrationTest
    {
        public IntegrationTestWithoutLayout()
        {
            _logglyAppender.Layout = null;
        }

        [Fact]
        public void LogContainsMessageAsIs()
        {
            _log.Info("test message");

            var message = WaitForSentMessage();
            message.Message.Should().Be("test message");
        }

        [Fact]
        public void SendFormattedString_SendsItProperlyFormatted()
        {
            _log.InfoFormat("Test message: {0:D2}", 3);

            var message = WaitForSentMessage();
            message.Message.Should().Be("Test message: 03");
        }

        [Fact]
        public void SendNull_SendsNullString()
        {
            _log.Info(null);

            var message = WaitForSentMessage();
            message.Message.Should().Be("null");
        }
    }
}