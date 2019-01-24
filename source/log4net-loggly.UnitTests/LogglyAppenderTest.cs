using System;
using FluentAssertions;
using log4net.Core;
using log4net.loggly;
using Moq;
using Xunit;

namespace log4net_loggly.UnitTests
{
    public class LogglyAppenderTest
    {
        [Fact]
        public void Append_BuffersMessageToBeSent()
        {
            var formatterMock = new Mock<ILogglyFormatter>();
            formatterMock.Setup(x => x.ToJson(It.IsAny<LoggingEvent>(), It.IsAny<string>()))
                .Returns<LoggingEvent, string>((e, m) => $"Formatted: {e.RenderedMessage}");
            var bufferMock = new Mock<ILogglyAsyncBuffer>();

            var appender = new LogglyAppender(new Config(), formatterMock.Object, bufferMock.Object);

            var evt = new LoggingEvent(new LoggingEventData
            {
                Message = "test log"
            });
            appender.DoAppend(evt);

            bufferMock.Verify(x => x.BufferForSend("Formatted: test log"), "message wasn't enqueued to be sent");
        }

        [Fact]
        public void Flush_FlushesBuffer()
        {
            var bufferMock = new Mock<ILogglyAsyncBuffer>();
            bufferMock.Setup(x => x.Flush(It.IsAny<TimeSpan>())).Returns(true);

            var appender = new LogglyAppender(new Config(), Mock.Of<ILogglyFormatter>(), bufferMock.Object);

            var result = appender.Flush(100);

            result.Should().BeTrue("flush should be successful");
            bufferMock.Verify(x => x.Flush(TimeSpan.FromMilliseconds(100)));
        }

        [Fact]
        public void AppenderPropertiesSetConfigurationValues()
        {
            var config = new Config();
            var appender = new LogglyAppender(config, Mock.Of<ILogglyFormatter>(), Mock.Of<ILogglyAsyncBuffer>());

            appender.RootUrl = "root-url";
            appender.SendMode = SendMode.Single;
            appender.BufferSize = 123;
            appender.CustomerToken = "test-token";
            appender.GlobalContextKeys = "global-keys";
            appender.LogicalThreadContextKeys = "thread-keys";
            appender.NumberOfInnerExceptions = 123;
            appender.Tag = "test-tag";
            appender.SendInterval = TimeSpan.FromSeconds(123);
            appender.TimeoutInSeconds = 123;
            appender.UserAgent = "user-agent";

            config.Should().BeEquivalentTo(new
            {
                RootUrl = "root-url",
                SendMode = SendMode.Single,
                BufferSize = 123,
                CustomerToken = "test-token",
                GlobalContextKeys = "global-keys",
                LogicalThreadContextKeys = "thread-keys",
                NumberOfInnerExceptions = 123,
                Tag = "test-tag",
                SendInterval = TimeSpan.FromSeconds(123),
                TimeoutInSeconds = 123,
                UserAgent = "user-agent"
            });
        }

        [Theory]
        [InlineData("inputs", SendMode.Single)]
        [InlineData("bulk", SendMode.Bulk)]
        [InlineData("nonsense", SendMode.Bulk)]
        public void LogModeSetsSendMode(string logMode, SendMode expectedSendMode)
        {
            var config = new Config();
            var appender = new LogglyAppender(config, Mock.Of<ILogglyFormatter>(), Mock.Of<ILogglyAsyncBuffer>());

            appender.LogMode = logMode;

            config.SendMode.Should().Be(expectedSendMode);
        }
    }
}
