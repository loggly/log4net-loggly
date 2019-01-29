using System;

namespace log4net.loggly
{
    internal interface ILogglyAsyncBuffer : IDisposable
    {
        /// <summary>
        /// Buffer message to be sent to Loggly
        /// </summary>
        void BufferForSend(string message);

        /// <summary>
        /// Flush any buffered messages right now.
        /// This method returns once all messages are flushed or when timeout expires.
        /// If new messages are coming during flush they will be included and may delay flush operation.
        /// </summary>
        /// <returns>Returns "true" if all messages were flushed, "false" if timeout expired before flushing all.</returns>
        bool Flush(TimeSpan maxWait);
    }
}