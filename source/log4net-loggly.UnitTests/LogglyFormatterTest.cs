namespace log4net_loggly.UnitTests
{
    using System;
    using AutoFixture;
    using FluentAssertions;
    using JetBrains.Annotations;
    using log4net;
    using log4net.Core;
    using log4net.loggly;
    using log4net.Repository;
    using Models;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
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
                _fixture.Customize<LogglyFormatter>(c => c.With(x => x.Config, _fixture.Freeze<Mock<ILogglyAppenderConfig>>().Object));
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
            public void ShouldAddALoggerNameProperty()
            {
                var evt = _fixture.Create<LoggingEvent>();
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var loggerName = (string)json.loggerName;

                loggerName.Should().StartWith("LoggerName", "because this is the value of the LoggerName property on the event");
            }

            [Fact]
            public void ShouldAddAMessagePropertyForEventsWithoutMessages()
            {
                var evt = new LoggingEvent(new LoggingEventData() { Level = Level.Info });
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var message = (string)json.message;

                message.Should()
                    .Be("null", "because the MessageObject property is null but we want to log \"null\" to show this explicitly");
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
            public void ShouldAddExtraPropertiesWhenMessageObjectIsAComplexType()
            {
                var evt = new LoggingEvent(
                    GetType(),
                    null,
                    _fixture.Create("loggerName"),
                    _fixture.Create<Level>(),
                    _fixture.Create<ComplexType>(),
                    null);
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
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
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                Action act = () => JObject.Parse(result);

                act.ShouldNotThrow<JsonException>("because the result should be a valid json document");
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

                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
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

                GlobalContext.Properties["Key1"] = _fixture.Create("value1");
                GlobalContext.Properties["Key2"] = _fixture.Create<int>();
                GlobalContext.Properties["Key3"] = _fixture.Create<ComplexType>();
                GlobalContext.Properties["Key4"] = _fixture.Create<FixedComplexType>();
                GlobalContext.Properties["Key5"] = null;
                GlobalContext.Properties["Key6"] = _fixture.Create("value1");

                var instance = _fixture.Create<LogglyFormatter>();
                _fixture.Freeze<Mock<ILogglyAppenderConfig>>().SetupGet(x => x.GlobalContextKeys).Returns("Key1,Key2,Key3,Key4,Key5");

                var result = instance.ToJson(evt);
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
                ((object)json.Key6).Should().BeNull("because this key was not marked for serialization");
            }

            [Fact]
            public void ShouldSerializeLogicalThreadContextProperties()
            {
                var evt = _fixture.Create<LoggingEvent>();

                LogicalThreadContext.Properties["Key1"] = _fixture.Create("value1");
                LogicalThreadContext.Properties["Key2"] = _fixture.Create<int>();
                LogicalThreadContext.Properties["Key3"] = _fixture.Create<ComplexType>();
                LogicalThreadContext.Properties["Key4"] = _fixture.Create<FixedComplexType>();
                LogicalThreadContext.Properties["Key5"] = null;
                LogicalThreadContext.Properties["Key6"] = _fixture.Create("value1");

                var instance = _fixture.Create<LogglyFormatter>();
                _fixture.Freeze<Mock<ILogglyAppenderConfig>>().SetupGet(x => x.LogicalThreadContextKeys).Returns("Key1,Key2,Key3,Key4,Key5");

                var result = instance.ToJson(evt);
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
                ((object)json.Key6).Should().BeNull("because this key was not marked for serialization");
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
                    Mock.Of<ILoggerRepository>(),
                    _fixture.Create("loggerName"),
                    _fixture.Create<Level>(),
                    _fixture.Create("message"),
                    ex);
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
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
            public void ShouldSerializeInnerExceptions(int configurationNumberOfInnerExceptions, int expectedNumberOfException, int innerExceptionsToCreate)
            {
                Exception ex = GetArgumentException(innerExceptionsToCreate + 1);

                var evt = new LoggingEvent(
                    GetType(),
                    Mock.Of<ILoggerRepository>(),
                    _fixture.Create("loggerName"),
                    _fixture.Create<Level>(),
                    _fixture.Create("message"),
                    ex);
                var instance = _fixture.Create<LogglyFormatter>();
                _fixture.Freeze<Mock<ILogglyAppenderConfig>>().SetupGet(x => x.NumberOfInnerExceptions).Returns(configurationNumberOfInnerExceptions);

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var exception = json.exception;

                ((object)exception).Should().NotBeNull("because an exception was specified in the event");

                // Validate first level
                var message = (string)exception.exceptionMessage;
                var type = (string)exception.exceptionType;
                var stacktrace = (string)exception.stacktrace;
                AssertException(message, type, stacktrace, 0);

                // Validate inner exceptions
                var count = 0;
                var innerException = exception.innerException;
                while (innerException != null)
                {
                    count++;
                    message = (string)innerException.innerExceptionMessage;
                    type = (string)innerException.innerExceptionType;
                    stacktrace = (string)innerException.innerStacktrace;
                    AssertException(message, type, stacktrace, count);
                    innerException = innerException.innerException;
                }

                count.Should().Be(expectedNumberOfException, "Expects all stacktraces");
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

                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
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
                    Mock.Of<ILoggerRepository>(),
                    _fixture.Create("loggerName"),
                    _fixture.Create<Level>(),
                    _fixture.Create("message"),
                    null);
                var instance = _fixture.Create<LogglyFormatter>();

                var result = instance.ToJson(evt);
                dynamic json = JObject.Parse(result);

                var message = (string)json.message;

                message.Should().StartWith("message", "because the MessageObject property value is used");
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
        }
    }
}
