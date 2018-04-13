using System;
using System.Collections.Generic;
using System.Linq;

namespace log4net.loggly
{
    public class LogglyStoreLogsInBuffer
    {
        public static List<string> arrBufferedMessage = new List<string>();

        public void storeBulkLogs(ILogglyAppenderConfig config, List<string> logs, bool isBulk)
        {
            if (logs.Count == 0) return;
            int numberOfLogsToBeRemoved = (arrBufferedMessage.Count + logs.Count) - config.BufferSize;
            if (numberOfLogsToBeRemoved > 0) arrBufferedMessage.RemoveRange(0, numberOfLogsToBeRemoved);

            arrBufferedMessage = logs.Concat(arrBufferedMessage).ToList();
        }

        public void storeInputLogs(ILogglyAppenderConfig config, string message, bool isBulk)
        {
            if (message == String.Empty) return;
            int numberOfLogsToBeRemoved = (arrBufferedMessage.Count + 1) - config.BufferSize;
            if (numberOfLogsToBeRemoved > 0) arrBufferedMessage.RemoveRange(0, numberOfLogsToBeRemoved);
            arrBufferedMessage.Add(message);
        }
    }
}
