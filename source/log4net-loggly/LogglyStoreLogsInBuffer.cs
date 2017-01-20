﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace log4net.loggly
{
    class LogglyStoreLogsInBuffer
    {
        public static int bufferSize = 500;
        public static List<string> arrBufferedMessage = new List<string>();
        public static List<string> tempList = new List<string>();

        public static void storeBulkLogs(ILogglyAppenderConfig config, List<string> logs, bool isBulk)
        {
            if (logs.Count == 0) return;
            int numberOfLogsToBeRemoved = (arrBufferedMessage.Count + logs.Count) - bufferSize;
            if (numberOfLogsToBeRemoved > 0) arrBufferedMessage.RemoveRange(0, numberOfLogsToBeRemoved);   
       
            arrBufferedMessage = logs.Concat(arrBufferedMessage).ToList();
        }

        public static void storeInputLogs(ILogglyAppenderConfig config, string message, bool isBulk)
        {
            if (message == String.Empty) return;
            int numberOfLogsToBeRemoved = (arrBufferedMessage.Count + 1) - bufferSize;
            if (numberOfLogsToBeRemoved > 0) arrBufferedMessage.RemoveRange(0, numberOfLogsToBeRemoved);
            arrBufferedMessage.Add(message);
        }
    }
}