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
			public LogglyStoreLogsInBuffer _storeEventsInBuffer = new LogglyStoreLogsInBuffer();
		  
	   public void sendBufferedLogsToLoggly(ILogglyAppenderConfig config, bool isBulk)
		{
			if (_storeEventsInBuffer.arrBufferedMessage.Count > 0)
			{
				int bulkModeBunch = 100;
				int inputModeBunch = 1;
				int logInBunch = isBulk ? bulkModeBunch : inputModeBunch;
				arrayMessage = _storeEventsInBuffer.arrBufferedMessage.Take(logInBunch).ToList();
				message = isBulk ? String.Join(System.Environment.NewLine, arrayMessage) : arrayMessage[0];
					try
					{
						Client.Send(config, message, isBulk);
						var tempList = _storeEventsInBuffer.arrBufferedMessage;
						if (_storeEventsInBuffer.arrBufferedMessage.Count < arrayMessage.Count)
						{
							_storeEventsInBuffer.arrBufferedMessage.Clear();
						}
						else
						{
							tempList.RemoveRange(0, arrayMessage.Count);
						}
						_storeEventsInBuffer.arrBufferedMessage = tempList;
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

