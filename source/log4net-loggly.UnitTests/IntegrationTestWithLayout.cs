using FluentAssertions;
using log4net.Layout;
using Xunit;

namespace log4net_loggly.UnitTests
{
    public class IntegrationTestWithLayout : IntegrationTest
    {
        public IntegrationTestWithLayout()
        {
            _logglyAppender.Layout = new PatternLayout("[%thread] %level - %message");
        }

        [Fact]
        public void LogContainsMessageFormattedByLayout()
        {
            _log.Info("test message");

            var message = WaitForSentMessage();
            message.Message.Should().Be($"[{TestThreadName}] INFO - test message");
        }

        [Fact]
        public void SendFormattedString_SendsItProperlyFormatted()
        {
            _log.InfoFormat("Test message: {0:D2}", 3);

            var message = WaitForSentMessage();
            message.Message.Should().Be($"[{TestThreadName}] INFO - Test message: 03");
        }

        [Fact]
        public void SendNull_DoesNotPutNullToFormattedString()
        {
            _log.Info(null);

            var message = WaitForSentMessage();
            message.Message.Should().Be($"[{TestThreadName}] INFO - ");
        }
    }
}