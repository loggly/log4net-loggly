using log4net.Util;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using AutoFixture;
using FluentAssertions;
using FluentAssertions.Json;
using JetBrains.Annotations;
using log4net;
using log4net.Core;
using log4net.loggly;
using log4net.ObjectRenderer;
using log4net.Repository;
using log4net_loggly.UnitTests.Models;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace log4net_loggly.UnitTests
{
    [UsedImplicitly]
    public class LogglyFormatterTest
    {
        private readonly Fixture _fixture;

        public LogglyFormatterTest()
        {
            _fixture = new Fixture();
            _fixture.Customize(new SupportMutableValueTypesCustomization());

            ThreadContext.Properties.Clear();
        }

        [Fact]
        public void ShouldAddAHostNameProperty()
        {
            var evt = _fixture.Create<LoggingEvent>();
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var hostName = (string)json.hostName;

            hostName.Should().NotBeNullOrEmpty("because the machine name is used to set the hostname");
        }

        [Fact]
        public void ShouldAddALevelProperty()
        {
            var evt = _fixture.Create<LoggingEvent>();
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var level = (string)json.level;

            level.Should().StartWith("levelName", "because the level name property on the event is used");
        }

        [Fact]
        public void ShouldAddALoggerNameProperty()
        {
            var evt = _fixture.Create<LoggingEvent>();
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var loggerName = (string)json.loggerName;

            loggerName.Should().StartWith("LoggerName", "because this is the value of the LoggerName property on the event");
        }

        [Fact]
        public void ShouldAddAMessagePropertyForEventsWithoutMessages()
        {
            var evt = new LoggingEvent(new LoggingEventData { Level = Level.Info });
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var message = (string)json.message;

            message.Should()
                .Be("null", "because the MessageObject property is null but we want to log \"null\" to show this explicitly");
        }

        [Fact]
        public void ShouldAddAProcessProperty()
        {
            var evt = _fixture.Create<LoggingEvent>();
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var process = (string)json.process;

            process.Should().NotBeNullOrEmpty("because the value is taken from the current process which should always be set");
        }

        [Fact]
        public void ShouldAddAThreadNameProperty()
        {
            var evt = _fixture.Create<LoggingEvent>();
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var threadName = (string)json.threadName;

            threadName.Should().StartWith("ThreadName", "because this is the value of the ThreadName property on the event");
        }

        [Fact]
        public void ShouldAddAValidTimestampProperty()
        {
            var evt = _fixture.Create<LoggingEvent>();
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var timestamp = (string)json.timestamp;

            timestamp.Should().NotBeNullOrEmpty("because the timestamp property should always be set");
            DateTime.TryParse(timestamp, out _).Should().BeTrue("because the timestamp should always be a valid date");
        }

        [Fact]
        public void ShouldAddExtraPropertiesWhenMessageObjectIsAComplexType()
        {
            var evt = new LoggingEvent(
                GetType(),
                null,
                _fixture.Create("loggerName"),
                _fixture.Create<Level>(),
                _fixture.Create<ComplexType>(),
                null);
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var propertyOne = (string)json.PropertyOne;
            var propertyTwo = (int)json.PropertyTwo;

            propertyOne.Should().StartWith("PropertyOne", "because the value from the PropertyOne property on the complex type is used");
            propertyTwo.Should().BeGreaterThan(0, "because the value of the PropertyTwo property on the complex type is used");
        }

        [Fact]
        public void ShouldReturnValidJson()
        {
            var evt = _fixture.Create<LoggingEvent>();
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            Action act = () => JObject.Parse(result);

            act.Should().NotThrow<JsonException>("because the result should be a valid json document");
        }

        [Fact]
        public void ShouldSerializeEventProperties()
        {
            var evt = _fixture.Create<LoggingEvent>();

            evt.Properties["Key1"] = _fixture.Create("value1");
            evt.Properties["Key2"] = _fixture.Create<int>();
            evt.Properties["Key3"] = _fixture.Create<ComplexType>();
            evt.Properties["Key4"] = _fixture.Create<FixedComplexType>();
            evt.Properties["Key5"] = null;

            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var key1 = json.Key1;
            var key2 = json.Key2;
            var key3 = json.Key3;
            var key4 = json.Key4;
            var key5 = json.Key5;

            ((string)key1).Should().StartWith("value1", "because that's the value of the event property with this key");
            ((int)key2).Should().BeGreaterThan(0, "because the key is set to a positive value in the event properties");
            ((object)key3).Should().NotBeNull("because the key is set in the event properties");
            ((string)key3.PropertyOne).Should().StartWith("PropertyOne", "because the value of the complex type should be serialized");
            ((int)key3.PropertyTwo).Should().BeGreaterThan(0, "because the value of the complex type should be serialized");
            ((object)key4).Should().NotBeNull("because the key is set in the event properties");
            ((string)key4).Should().Be("I'm a fixed type!", "because the type of this property requires fixing");
            ((object)key5).Should().BeNull("because the key is set but the value is null");
        }

        [Fact]
        public void ShouldSerializeGlobalThreadContextProperties()
        {
            var evt = _fixture.Create<LoggingEvent>();

            PopulateSixContextProperties(GlobalContext.Properties);

            var instance = new LogglyFormatter(new Config
            {
                GlobalContextKeys = "Key1,Key2,Key3,Key4,Key5"
            });

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            VerifyContextPropertiesInJson(json);
        }

        [Fact]
        public void ShouldSerializeLogicalThreadContextProperties()
        {
            var evt = _fixture.Create<LoggingEvent>();

            PopulateSixContextProperties(LogicalThreadContext.Properties);

            var instance = new LogglyFormatter(new Config
            {
                LogicalThreadContextKeys = "Key1,Key2,Key3,Key4,Key5"
            });

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            VerifyContextPropertiesInJson(json);
        }

        [Fact]
        public void ShouldSerializeTheException()
        {
            // In order to populate the stacktrace.
            Exception ex;
            try
            {
                throw new ArgumentException();
            }
            catch (Exception e)
            {
                ex = e;
            }

            var evt = new LoggingEvent(
                GetType(),
                CreateMockRepository(),
                _fixture.Create("loggerName"),
                _fixture.Create<Level>(),
                _fixture.Create("message"),
                ex);
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var exception = json.exception;

            ((object)exception).Should().NotBeNull("because an exception was specified in the event");

            var message = (string)exception.exceptionMessage;
            var type = (string)exception.exceptionType;
            var stacktrace = (string)exception.stacktrace;

            message.Should().NotBeNullOrEmpty("because an argument exception has a default message");
            type.Should().Be(typeof(ArgumentException).FullName, "because we logged an argument exception");
            stacktrace.Should().NotBeNull("because the exception has a stacktrace");
        }

        [Theory]
        [InlineData(0, 0, 1)]
        [InlineData(1, 1, 1)]
        [InlineData(2, 1, 1)]
        [InlineData(2, 2, 2)]
        [InlineData(2, 2, 3)]
        [InlineData(3, 3, 3)]
        [InlineData(5, 5, 5)]
        [InlineData(5, 5, 10)]
        public void ShouldSerializeInnerExceptions(int configurationNumberOfInnerExceptions, int expectedNumberOfInnerExceptions, int innerExceptionsToCreate)
        {
            Exception ex = GetArgumentException(innerExceptionsToCreate + 1);

            var evt = new LoggingEvent(
                GetType(),
                CreateMockRepository(),
                _fixture.Create("loggerName"),
                _fixture.Create<Level>(),
                _fixture.Create("message"),
                ex);
            var instance = new LogglyFormatter(new Config
            {
                NumberOfInnerExceptions = configurationNumberOfInnerExceptions
            });

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var exception = json.exception;

            ((object)exception).Should().NotBeNull("because an exception was specified in the event");

            var count = 0;
            while (exception != null)
            {
                var message = (string)exception.exceptionMessage;
                var type = (string)exception.exceptionType;
                var stacktrace = (string)exception.stacktrace;
                AssertException(message, type, stacktrace, count);
                exception = exception.innerException;
                if (exception != null)
                {
                    count++;
                }
            }

            count.Should().Be(expectedNumberOfInnerExceptions, "Expects all stacktraces");
        }

        [Fact]
        public void ShouldSerializeThreadContextProperties()
        {
            var evt = _fixture.Create<LoggingEvent>();

            ThreadContext.Properties["Key1"] = _fixture.Create("value1");
            ThreadContext.Properties["Key2"] = _fixture.Create<int>();
            ThreadContext.Properties["Key3"] = _fixture.Create<ComplexType>();
            ThreadContext.Properties["Key4"] = _fixture.Create<FixedComplexType>();
            ThreadContext.Properties["Key5"] = null;

            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var key1 = json.Key1;
            var key2 = json.Key2;
            var key3 = json.Key3;
            var key4 = json.Key4;
            var key5 = json.Key5;

            ((string)key1).Should().StartWith("value1", "because that's the value of the event property with this key");
            ((int)key2).Should().BeGreaterThan(0, "because the key is set to a positive value in the event properties");
            ((object)key3).Should().NotBeNull("because the key is set in the event properties");
            ((string)key3.PropertyOne).Should().StartWith("PropertyOne", "because the value of the complex type should be serialized");
            ((int)key3.PropertyTwo).Should().BeGreaterThan(0, "because the value of the complex type should be serialized");
            ((object)key4).Should().NotBeNull("because the key is set in the event properties");
            ((string)key4).Should().Be("I'm a fixed type!", "because the type of this property requires fixing");
            ((object)key5).Should().BeNull("because the key is set but the value is null");
        }

        [Fact]
        public void ShouldSetMessagePropertyWhenMessageObjectIsString()
        {
            var evt = new LoggingEvent(
                GetType(),
                CreateMockRepository(),
                _fixture.Create("loggerName"),
                _fixture.Create<Level>(),
                _fixture.Create("message"),
                null);
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            dynamic json = JObject.Parse(result);

            var message = (string)json.message;

            message.Should().StartWith("message", "because the MessageObject property value is used");
        }

        [Fact]
        public void ShouldParseJsonString()
        {
            var evt = new LoggingEvent(
                GetType(),
                CreateMockRepository(),
                _fixture.Create("loggerName"),
                _fixture.Create<Level>(),
                "   { \"property1\": \"value1\" }",
                null);
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            var json = JObject.Parse(result);

            json.Should().HaveElement("property1").Which.Should().HaveValue("value1");
        }

        [Fact]
        public void ShouldSerializeCustomObjectToOutput()
        {
            var evt = new LoggingEvent(
                GetType(),
                CreateMockRepository(),
                _fixture.Create("loggerName"),
                _fixture.Create<Level>(),
                new { property1 = "value1" },
                null);
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            var json = JObject.Parse(result);

            json.Should().HaveElement("property1").Which.Should().HaveValue("value1");
        }

        [Fact]
        public void ShouldSerializeFormattedString()
        {
            var evt = new LoggingEvent(
                GetType(),
                CreateMockRepository(),
                _fixture.Create("loggerName"),
                _fixture.Create<Level>(),
                new SystemStringFormat(CultureInfo.InvariantCulture, "test: {0}{1}{2}", 1, 2, 3),
                null);
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            var json = JObject.Parse(result);

            json.Should().HaveElement("message").Which.Should().HaveValue("test: 123");
        }

        [Fact]
        public void ShouldSerializeRenderedMessageIfNoMessageObjectIsSet()
        {
            var evt = new LoggingEvent(
                GetType(),
                CreateMockRepository(),
                new LoggingEventData
                {
                    Message = "test message"
                });
            var instance = new LogglyFormatter(new Config());

            var result = instance.ToJson(evt, evt.RenderedMessage);
            var json = JObject.Parse(result);

            json.Should().HaveElement("message").Which.Should().HaveValue("test message");
        }

        [Fact]
        public void MessageOverTheLimitIsCut()
        {
            var config = new Config
            {
                // there is over 150 bytes of fixed JSON unrelated to "message" value (hostname, timestamp, logger name, ...)
                // so this value should leave part of the message and cut the rest.
                MaxEventSizeBytes = 200
            };
            var formatter = new LogglyFormatter(config);
            var evt = new LoggingEvent(
                GetType(),
                CreateMockRepository(),
                "logger",
                Level.Info,
                null,
                null);

            var originalMessage =
                "This is very long test log message that will be cut down. This message will be shorter in result " +
                "event because it will be cut to fit into max event limit. Actual result size can vary based on values " +
                "such as hostname, thread name, process name. It's therefore not possible to check against fixed value, " +
                "the test just needs to check that result message is shorter than this one and that the whole event size " +
                "fits into max allowed size";

            var result = formatter.ToJson(evt, originalMessage);

            result.Length.Should().Be(config.MaxEventSizeBytes, "result event size should be within the limit");

            dynamic json = JObject.Parse(result);

            var message = (string)json.message;
            message.Length.Should().BeLessThan(originalMessage.Length, "result message should be shorter than original message");
            originalMessage.Should().StartWith(message, "result message should be beginning of the original message");
        }

        [Fact]
        public void EventWithoutMessageExceedingMaxSizeIsDropped()
        {
            var config = new Config
            {
                // there is 155 bytes of fixed JSON (for "evt" values below) unrelated to "message" value
                // so this value should cut message to first 20 characters 
                MaxEventSizeBytes = 175
            };
            var formatter = new LogglyFormatter(config);
            var evt = new LoggingEvent(
                GetType(),
                CreateMockRepository(),
                "logger",
                Level.Info,
                new {Property1 = "value1", Property2 = "value2", Property3 = "value3" },
                null);

            var result = formatter.ToJson(evt, "<anonymous_type>");

            result.Should().BeNull();
        }

        private static ArgumentException GetArgumentException(int numberOfExceptions)
        {
            try
            {
                if (--numberOfExceptions > 0)
                {
                    try
                    {
                        GetNestedArgumentException(numberOfExceptions);
                    }
                    catch (ArgumentException e)
                    {
                        throw new ArgumentException("Exception 0", e);
                    }
                }
                else
                {
                    throw new ArgumentException("Exception 0");
                }
            }
            catch (ArgumentException e)
            {
                return e;
            }
            return null;
        }

        private static void GetNestedArgumentException(int numberOfExceptions, int deep = 0)
        {
            deep++;
            if (--numberOfExceptions > 0)
            {
                try
                {
                    GetNestedArgumentException(numberOfExceptions, deep);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException($"Exception {deep}", e);
                }
            }
            else
            {
                throw new ArgumentException($"Exception {deep}");
            }
        }

        private static void AssertException(string message, string type, string stacktrace, int stackLevel)
        {
            message.Should().Be($"Exception {stackLevel}", "because an argument exception has a default message");
            type.Should().Be(typeof(ArgumentException).FullName, "because we logged an argument exception");
            stacktrace.Should().NotBeNull("because the exception has a stacktrace");
        }

        private void PopulateSixContextProperties(ContextPropertiesBase context)
        {
            context["Key1"] = _fixture.Create("value1");
            context["Key2"] = _fixture.Create<int>();
            context["Key3"] = _fixture.Create<ComplexType>();
            context["Key4"] = _fixture.Create<FixedComplexType>();
            context["Key5"] = null;
            context["Key6"] = _fixture.Create("value1");
        }

        private void VerifyContextPropertiesInJson(dynamic json)
        {
            var key1 = json.Key1;
            var key2 = json.Key2;
            var key3 = json.Key3;
            var key4 = json.Key4;
            var key5 = json.Key5;

            ((string)key1).Should().StartWith("value1", "because that's the value of the event property with this key");
            ((int)key2).Should().BeGreaterThan(0, "because the key is set to a positive value in the event properties");
            ((object)key3).Should().NotBeNull("because the key is set in the event properties");
            ((string)key3.PropertyOne).Should().StartWith("PropertyOne", "because the value of the complex type should be serialized");
            ((int)key3.PropertyTwo).Should().BeGreaterThan(0, "because the value of the complex type should be serialized");
            ((object)key4).Should().NotBeNull("because the key is set in the event properties");
            ((string)key4).Should().Be("I'm a fixed type!", "because the type of this property requires fixing");
            ((object)key5).Should().BeNull("because the key is set but the value is null");
            ((object)json.Key6).Should().BeNull("because this key was not marked for serialization");
        }

        private ILoggerRepository CreateMockRepository()
        {
            var mock = new Mock<ILoggerRepository>();
            mock.SetupGet(x => x.RendererMap).Returns(new RendererMap());
            return mock.Object;
        }
    }
}
