using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace log4net.loggly
{
    public class LogglySendBufferedLogs
    {
        public string message = null;
        public List<string> arrayMessage = new List<string>();
        public ILogglyClient Client = new LogglyClient();
        public LogglyClient _logClient = new LogglyClient();

        public void sendBufferedLogsToLoggly(ILogglyAppenderConfig config, bool isBulk)
        {
            if (LogglyStoreLogsInBuffer.arrBufferedMessage.Count > 0)
            {
                int bulkModeBunch = 100;
                int inputModeBunch = 1;
                int logInBunch = isBulk ? bulkModeBunch : inputModeBunch;
                arrayMessage = LogglyStoreLogsInBuffer.arrBufferedMessage.Take(logInBunch).ToList();
                message = isBulk ? String.Join(System.Environment.NewLine, arrayMessage) : arrayMessage[0];
                try
                {
                    Client.Send(config, message, isBulk);
                    var tempList = LogglyStoreLogsInBuffer.arrBufferedMessage;
                    if (LogglyStoreLogsInBuffer.arrBufferedMessage.Count < arrayMessage.Count)
                    {
                        LogglyStoreLogsInBuffer.arrBufferedMessage.Clear();
                    }
                    else
                    {
                        tempList.RemoveRange(0, arrayMessage.Count);
                    }
                    LogglyStoreLogsInBuffer.arrBufferedMessage = tempList;
                }
                catch (WebException e)
                {
                    var response = (HttpWebResponse)e.Response;
                    if (response != null && response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logClient.setTokenValid(false);
                        Console.WriteLine("Loggly error: {0}", e.Message);
                        return;
                    }
                }
                finally
                {
                    arrayMessage.Clear();
                    arrayMessage = null;
                    GC.Collect();
                }
            }
        }
    }
}
