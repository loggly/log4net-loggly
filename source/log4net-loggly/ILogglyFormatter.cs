using log4net.Core;

namespace log4net.loggly
{
    internal interface ILogglyFormatter
    {
        /// <summary>
        /// Format event as JSON.
        /// </summary>
        /// <param name="loggingEvent">Event to format</param>
        /// <param name="renderedMessage">Event message rendered by log4net.</param>
        /// <returns></returns>
        string ToJson(LoggingEvent loggingEvent, string renderedMessage);
    }
}