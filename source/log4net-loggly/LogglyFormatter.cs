using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using log4net.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace log4net.loggly
{
    internal class LogglyFormatter : ILogglyFormatter
    {
        private readonly Config _config;
        private readonly Process _currentProcess;
        private readonly JsonSerializer _jsonSerializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
        {
            PreserveReferencesHandling = PreserveReferencesHandling.Arrays,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });

        public LogglyFormatter(Config config)
        {
            _config = config;
            _currentProcess = Process.GetCurrentProcess();
        }

        public string ToJson(LoggingEvent loggingEvent, string renderedMessage)
        {
            // formatting base logging info
            JObject loggingInfo = new JObject
            {
                ["timestamp"] = loggingEvent.TimeStamp.ToString(@"yyyy-MM-ddTHH\:mm\:ss.fffzzz"),
                ["level"] = loggingEvent.Level?.DisplayName,
                ["hostName"] = Environment.MachineName,
                ["process"] = _currentProcess.ProcessName,
                ["threadName"] = loggingEvent.ThreadName,
                ["loggerName"] = loggingEvent.LoggerName
            };

            AddMessageOrObjectProperties(loggingInfo, loggingEvent, renderedMessage);
            AddExceptionIfPresent(loggingInfo, loggingEvent);
            AddContextProperties(loggingInfo, loggingEvent);

            string resultEvent = ToJsonString(loggingInfo);

            int eventSize = Encoding.UTF8.GetByteCount(resultEvent);

            // Be optimistic regarding max event size, first serialize and then check against the limit.
            // Only if the event is bigger than allowed go back and try to trim exceeding data.
            if (eventSize > _config.MaxEventSizeBytes)
            {
                int bytesOver = eventSize - _config.MaxEventSizeBytes;
                // ok, we are over, try to look at plain "message" and cut that down if possible
                if (loggingInfo["message"] != null)
                {
                    var fullMessage = loggingInfo["message"].Value<string>();
                    var originalMessageLength = fullMessage.Length;
                    var newMessageLength = Math.Max(0, originalMessageLength - bytesOver);
                    loggingInfo["message"] = fullMessage.Substring(0, newMessageLength);
                    bytesOver -= originalMessageLength - newMessageLength;
                }

                // Message cut and still over? We can't shorten this event further, drop it,
                // otherwise it will be rejected down the line anyway and we won't be able to identify it so easily.
                if (bytesOver > 0)
                {
                    ErrorReporter.ReportError(
                        $"LogglyFormatter: Dropping log event exceeding allowed limit of {_config.MaxEventSizeBytes} bytes. " +
                        $"First 500 bytes of dropped event are: {resultEvent.Substring(0, Math.Min(500, resultEvent.Length))}");
                    return null;
                }

                resultEvent = ToJsonString(loggingInfo);
            }

            return resultEvent;
        }

        private static string ToJsonString(JObject loggingInfo)
        {
            return JsonConvert.SerializeObject(
                loggingInfo,
                new JsonSerializerSettings
                {
                    Formatting = Formatting.None,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                });
        }

        private void AddContextProperties(JObject loggingInfo, LoggingEvent loggingEvent)
        {
            if (_config.GlobalContextKeys != null)
            {
                var globalContextProperties = _config.GlobalContextKeys.Split(',');
                foreach (var key in globalContextProperties)
                {
                    if (TryGetPropertyValue(GlobalContext.Properties[key], out var propertyValue))
                    {
                        loggingInfo[key] = JToken.FromObject(propertyValue);
                    }
                }
            }

            var threadContextProperties = ThreadContext.Properties.GetKeys();
            if (threadContextProperties != null && threadContextProperties.Any())
            {
                foreach (var key in threadContextProperties)
                {
                    if (TryGetPropertyValue(ThreadContext.Properties[key], out var propertyValue))
                    {
                        loggingInfo[key] = JToken.FromObject(propertyValue);
                    }
                }
            }

            if (_config.LogicalThreadContextKeys != null)
            {
                var logicalThreadContextProperties = _config.LogicalThreadContextKeys.Split(',');
                foreach (var key in logicalThreadContextProperties)
                {
                    if (TryGetPropertyValue(LogicalThreadContext.Properties[key], out var propertyValue))
                    {
                        loggingInfo[key] = JToken.FromObject(propertyValue);
                    }
                }
            }

            if (loggingEvent.Properties.Count > 0)
            {
                foreach (DictionaryEntry property in loggingEvent.Properties)
                {
                    if (TryGetPropertyValue(property.Value, out var propertyValue))
                    {
                        loggingInfo[(string)property.Key] = JToken.FromObject(propertyValue);
                    }
                }
            }
        }

        private void AddExceptionIfPresent(JObject loggingInfo, LoggingEvent loggingEvent)
        {
            dynamic exceptionInfo = GetExceptionInfo(loggingEvent);
            if (exceptionInfo != null)
            {
                loggingInfo["exception"] = exceptionInfo;
            }
        }

        private void AddMessageOrObjectProperties(JObject loggingInfo, LoggingEvent loggingEvent, string renderedMessage)
        {
            if (loggingEvent.MessageObject is string messageString)
            {
                if (CanBeJson(messageString))
                {
                    // try parse as JSON, otherwise use rendered message passed to this method
                    try
                    {
                        var json = JObject.Parse(messageString);
                        loggingInfo.Merge(json,
                            new JsonMergeSettings
                            {
                                MergeArrayHandling = MergeArrayHandling.Union
                            });
                        // we have all we need
                        return;
                    }
                    catch (JsonReaderException)
                    {
                        // no JSON, handle it as plain string
                    }
                }

                // plain string, use rendered message
                loggingInfo["message"] = GetStringFormLog(renderedMessage);
            }
            else if (loggingEvent.MessageObject == null
                    // log4net.Util.SystemStringFormat is object used when someone calls log.*Format(...)
                    // and in that case renderedMessage is what we want
                    || loggingEvent.MessageObject is Util.SystemStringFormat
                    // legacy code, it looks that there are cases when the object is StringFormatFormattedMessage,
                    // but then it should be already in renderedMessage
                    || (loggingEvent.MessageObject.GetType().FullName?.Contains("StringFormatFormattedMessage") ?? false))
            {
                loggingInfo["message"] = GetStringFormLog(renderedMessage);
            }
            else
            {
                // serialize object to JSON and add it's properties to loggingInfo
                var json = JObject.FromObject(loggingEvent.MessageObject, _jsonSerializer);
                loggingInfo.Merge(json,
                    new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Union
                    });
            }
        }

        private static bool TryGetPropertyValue(object property, out object propertyValue)
        {
            if (property is IFixingRequired fixedProperty && fixedProperty.GetFixedObject() != null)
            {
                propertyValue = fixedProperty.GetFixedObject();
            }
            else
            {
                propertyValue = property;
            }

            return propertyValue != null;
        }

        /// <summary>
        /// Returns the exception information. Also takes care of the InnerException.
        /// </summary>
        /// <param name="loggingEvent"></param>
        /// <returns></returns>
        private JObject GetExceptionInfo(LoggingEvent loggingEvent)
        {
            if (loggingEvent.ExceptionObject == null)
            {
                return null;
            }

            return GetExceptionInfo(loggingEvent.ExceptionObject, _config.NumberOfInnerExceptions);
        }

        /// <summary>
        /// Return exception as JObject
        /// </summary>
        /// <param name="exception">Exception to serialize</param>
        /// <param name="deep">The number of inner exceptions that should be included.</param>
        private JObject GetExceptionInfo(Exception exception, int deep)
        {
            if (exception == null || deep < 0)
                return null;

            var result = new JObject
            {
                ["exceptionType"] = exception.GetType().FullName,
                ["exceptionMessage"] = exception.Message,
                ["stacktrace"] = exception.StackTrace,
                ["innerException"] = deep-- > 0 ? GetExceptionInfo(exception.InnerException, deep) : null
            };
            if (!result["innerException"].HasValues)
            {
                result.Remove("innerException");
            }
            return result;
        }

        private bool CanBeJson(string message)
        {
            // This loop is about 2x faster than message.TrimStart().StartsWith("{") and about 4x faster than Regex("^\s*\{")
            foreach (var t in message)
            {
                // skip leading whitespaces
                if (char.IsWhiteSpace(t))
                {
                    continue;
                }
                // if first character after whitespace is { then this can be a JSON, otherwise not
                if (t == '{')
                {
                    return true;
                }

                return false;
            }

            return false;
        }

        private string GetStringFormLog(string value)
        {
            return !string.IsNullOrEmpty(value) ? value : "null";
        }
    }
}
