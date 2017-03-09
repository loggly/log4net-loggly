namespace log4net.loggly
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Dynamic;
    using System.Linq;
    using System.Text;
    using log4net.Core;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class LogglyFormatter : ILogglyFormatter
    {
        private readonly Process _currentProcess;

        public int EVENT_SIZE = 1000 * 1000;

        public LogglyFormatter()
        {
            _currentProcess = Process.GetCurrentProcess();
        }

        public ILogglyAppenderConfig Config { get; set; }

        public virtual void AppendAdditionalLoggingInformation(ILogglyAppenderConfig config, LoggingEvent loggingEvent)
        {
            Config = config;
        }

        public virtual string ToJson(LoggingEvent loggingEvent)
        {
            return PreParse(loggingEvent);
        }

        public virtual string ToJson(IEnumerable<LoggingEvent> loggingEvents)
        {
            return JsonConvert.SerializeObject(
                loggingEvents.Select(PreParse),
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
        }

        public virtual string ToJson(string renderedLog, DateTime timeStamp)
        {
            return ParseRenderedLog(renderedLog, timeStamp);
        }

        private static bool TryGetPropertyValue(object property, out object propertyValue)
        {
            var fixedProperty = property as IFixingRequired;
            if (fixedProperty != null && fixedProperty.GetFixedObject() != null)
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
        private object GetExceptionInfo(LoggingEvent loggingEvent)
        {
            if (loggingEvent.ExceptionObject == null)
            {
                return null;
            }

            dynamic exceptionInfo = new ExpandoObject();
            exceptionInfo.exceptionType = loggingEvent.ExceptionObject.GetType().FullName;
            exceptionInfo.exceptionMessage = loggingEvent.ExceptionObject.Message;
            exceptionInfo.stacktrace = loggingEvent.ExceptionObject.StackTrace;

            //most of the times dotnet exceptions contain important messages in the inner exceptions
            if (loggingEvent.ExceptionObject.InnerException != null)
            {
                dynamic innerException = new
                {
                    innerExceptionType = loggingEvent.ExceptionObject.InnerException.GetType().FullName,
                    innerExceptionMessage = loggingEvent.ExceptionObject.InnerException.Message,
                    innerStacktrace = loggingEvent.ExceptionObject.InnerException.StackTrace
                };
                exceptionInfo.innerException = innerException;
            }

            return exceptionInfo;
        }

        /// <summary>
        /// Returns a string type message if it is not a custom object,
        /// otherwise returns custom object details
        /// </summary>
        /// <param name="loggingEvent"></param>
        /// <param name="objInfo"></param>
        /// <returns></returns>
        private string GetMessageAndObjectInfo(LoggingEvent loggingEvent, out object objInfo)
        {
            var message = string.Empty;
            objInfo = null;
            var bytesLengthAllowedToLoggly = EVENT_SIZE;

            if (loggingEvent.MessageObject != null)
            {
                if (loggingEvent.MessageObject is string
                    //if it is sent by using InfoFormat method then treat it as a string message
                    || loggingEvent.MessageObject.GetType().FullName == "log4net.Util.SystemStringFormat"
                    || loggingEvent.MessageObject.GetType().FullName.Contains("StringFormatFormattedMessage"))
                {
                    message = loggingEvent.MessageObject.ToString();
                    var messageSizeInBytes = Encoding.Default.GetByteCount(message);
                    if (messageSizeInBytes > bytesLengthAllowedToLoggly)
                    {
                        message = message.Substring(0, bytesLengthAllowedToLoggly);
                    }
                }
                else
                {
                    objInfo = loggingEvent.MessageObject;
                }
            }
            else
            {
                //adding message as null so that the Loggly user
                //can know that a null object is logged.
                message = "null";
            }
            return message;
        }

        /// <summary>
        /// Merged Rendered log and formatted timestamp in the single Json object
        /// </summary>
        /// <param name="log"></param>
        /// <param name="timeStamp"></param>
        /// <returns></returns>
        private string ParseRenderedLog(string log, DateTime timeStamp)
        {
            dynamic loggingInfo = new ExpandoObject();
            loggingInfo.timestamp = timeStamp.ToString(@"yyyy-MM-ddTHH\:mm\:ss.fffzzz");

            string jsonMessage;
            if (TryGetParsedJsonFromLog(loggingInfo, log, out jsonMessage))
            {
                return jsonMessage;
            }

            loggingInfo.message = log;
            return JsonConvert.SerializeObject(
                loggingInfo,
                new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Arrays,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                });
        }

        /// <summary>
        /// Formats the log event to various JSON fields that are to be shown in Loggly.
        /// </summary>
        /// <param name="loggingEvent"></param>
        /// <returns></returns>
        private string PreParse(LoggingEvent loggingEvent)
        {
            //formating base logging info
            dynamic loggingInfo = new ExpandoObject();
            loggingInfo.timestamp = loggingEvent.TimeStamp.ToString(@"yyyy-MM-ddTHH\:mm\:ss.fffzzz");
            loggingInfo.level = loggingEvent.Level.DisplayName;
            loggingInfo.hostName = Environment.MachineName;
            loggingInfo.process = _currentProcess.ProcessName;
            loggingInfo.threadName = loggingEvent.ThreadName;
            loggingInfo.loggerName = loggingEvent.LoggerName;

            //handling messages
            object loggedObject;
            var message = GetMessageAndObjectInfo(loggingEvent, out loggedObject);

            if (message != string.Empty)
            {
                loggingInfo.message = message;
            }

            //handling exceptions
            dynamic exceptionInfo = GetExceptionInfo(loggingEvent);
            if (exceptionInfo != null)
            {
                loggingInfo.exception = exceptionInfo;
            }

            var properties = (IDictionary<string, object>) loggingInfo;

            //handling loggingevent properties
            if (loggingEvent.Properties.Count > 0)
            {
                foreach (DictionaryEntry property in loggingEvent.Properties)
                {
                    object propertyValue;
                    if (TryGetPropertyValue(property.Value, out propertyValue))
                    {
                        properties[(string) property.Key] = propertyValue;
                    }
                }
            }

            //handling threadcontext properties
            var threadContextProperties = ThreadContext.Properties.GetKeys();
            if (threadContextProperties != null && threadContextProperties.Any())
            {
                foreach (var key in threadContextProperties)
                {
                    object propertyValue;
                    if (TryGetPropertyValue(ThreadContext.Properties[key], out propertyValue))
                    {
                        properties[key] = propertyValue;
                    }
                }
            }

            //handling logicalthreadcontext properties
            if (Config.LogicalThreadContextKeys != null)
            {
                var logicalThreadContextProperties = Config.LogicalThreadContextKeys.Split(',');
                foreach (var key in logicalThreadContextProperties)
                {
                    object propertyValue;
                    if (TryGetPropertyValue(LogicalThreadContext.Properties[key], out propertyValue))
                    {
                        properties[key] = propertyValue;
                    }
                }
            }

            //handling globalcontext properties
            if (Config.GlobalContextKeys != null)
            {
                var globalContextProperties = Config.GlobalContextKeys.Split(',');
                foreach (var key in globalContextProperties)
                {
                    object propertyValue;
                    if (TryGetPropertyValue(GlobalContext.Properties[key], out propertyValue))
                    {
                        properties[key] = propertyValue;
                    }
                }
            }

            string jsonMessage;
            if (TryGetParsedJsonFromLog(loggingInfo, loggedObject, out jsonMessage))
            {
                return jsonMessage;
            }

            //converting event info to Json string
            return JsonConvert.SerializeObject(
                loggingInfo,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                });
        }

        /// <summary>
        /// Tries to merge log with the logged object or rendered log
        /// and converts to JSON
        /// </summary>
        /// <param name="loggingInfo"></param>
        /// <param name="loggingObject"></param>
        /// <param name="loggingEventJson"></param>
        /// <returns></returns>
        private bool TryGetParsedJsonFromLog(dynamic loggingInfo, object loggingObject, out string loggingEventJson)
        {
            //serialize the dynamic object to string
            loggingEventJson = JsonConvert.SerializeObject(
                loggingInfo,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                });

            //if loggingObject is null then we need to go to further step
            if (loggingObject == null)
            {
                return false;
            }

            try
            {
                string loggedObjectJson;
                if (loggingObject is string)
                {
                    loggedObjectJson = loggingObject.ToString();
                }
                else
                {
                    loggedObjectJson = JsonConvert.SerializeObject(
                        loggingObject,
                        new JsonSerializerSettings
                        {
                            PreserveReferencesHandling = PreserveReferencesHandling.Arrays,
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        });
                }

                //try to parse the logging object
                var jObject = JObject.Parse(loggedObjectJson);
                var jEvent = JObject.Parse(loggingEventJson);

                //merge these two objects into one JSON string
                jEvent.Merge(
                    jObject,
                    new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Union
                    });

                loggingEventJson = jEvent.ToString();

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
