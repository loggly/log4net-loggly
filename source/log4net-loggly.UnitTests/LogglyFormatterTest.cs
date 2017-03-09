namespace log4net_loggly.UnitTests
{
    using System;
    using FluentAssertions;
    using JetBrains.Annotations;
    using log4net.Core;
    using log4net.loggly;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Xunit;

    [UsedImplicitly]
    public class LogglyFormatterTest
    {
        public class ToJsonMethod
        {
            private readonly Fixture _fixture;

            public ToJsonMethod()
            {
                _fixture = new Fixture();
                _fixture.Customize(new SupportMutableValueTypesCustomization());
                _fixture.Customize(new AutoMoqCustomization());
                _fixture.Customize<LogglyFormatter>(c => c.With(x => x.Config, Mock.Of<ILogglyAppenderConfig>()));
            }

            [Fact]
            public void ShouldReturnValidJson()
            {
                var evt = _fixture.Create<LoggingEvent>();
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                Action act = () => JObject.Parse(result);

                act.ShouldNotThrow<JsonException>("because the result should be a valid json document");
            }

            [Fact]
            public void ShouldAddAValidTimestampProperty()
            {
                var evt = _fixture.Create<LoggingEvent>();
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var timestamp = (string)json.timestamp;
                DateTime voidDateTime;

                timestamp.Should().NotBeNullOrEmpty("because the timestamp property should always be set");
                DateTime.TryParse(timestamp, out voidDateTime).Should().BeTrue("because the timestamp should always be a valid date");
            }

            [Fact]
            public void ShouldAddALevelProperty()
            {
                var evt = _fixture.Create<LoggingEvent>();
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var level = (string)json.level;

                level.Should().StartWith("levelName", "because the level name property on the event is used");
            }

            [Fact]
            public void ShouldAddAHostNameProperty()
            {
                var evt = _fixture.Create<LoggingEvent>();
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var hostName = (string)json.hostName;

                hostName.Should().NotBeNullOrEmpty("because the machine name is used to set the hostname");
            }

            [Fact]
            public void ShouldAddAProcessProperty()
            {
                var evt = _fixture.Create<LoggingEvent>();
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var process = (string)json.process;

                process.Should().NotBeNullOrEmpty("because the value is taken from the current process which should always be set");
            }

            [Fact]
            public void ShouldAddAThreadNameProperty()
            {
                var evt = _fixture.Create<LoggingEvent>();
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var threadName = (string)json.threadName;

                threadName.Should().StartWith("ThreadName", "because this is the value of the ThreadName property on the event");
            }

            [Fact]
            public void ShouldAddALoggerNameProperty()
            {
                var evt = _fixture.Create<LoggingEvent>();
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var loggerName = (string)json.loggerName;

                loggerName.Should().StartWith("LoggerName", "because this is the value of the LoggerName property on the event");
            }
        }
    }
}
