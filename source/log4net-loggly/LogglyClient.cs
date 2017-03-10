using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Linq;

namespace log4net.loggly
{
	public class LogglyClient : ILogglyClient
	{
		bool isValidToken = true;
		public LogglyStoreLogsInBuffer _storeLogsInBuffer = new LogglyStoreLogsInBuffer();

		public void setTokenValid(bool flag) 
		{
		   isValidToken = flag;       
		}

		public void storeLogs(string message, ILogglyAppenderConfig config, bool isBulk) 
		{
			List<string> messageBulk = new List<string>();
			if (isBulk)
				{
					messageBulk = message.Split('\n').ToList();
					_storeLogsInBuffer.storeBulkLogs(config, messageBulk, isBulk);
				}
				else
				{
					_storeLogsInBuffer.storeInputLogs(config, message, isBulk);
				}
		}

		void printErrorMessage(string message) 
		{
			Console.WriteLine("Loggly error: {0}", message);
		}

		public virtual void Send(ILogglyAppenderConfig config, string message)
		{
			int maxRetryAllowed = 5;
			int totalRetries = 0;

			string _tag = config.Tag;
			bool isBulk = config.LogMode.Contains("bulk");

			HttpWebResponse webResponse;
			HttpWebRequest webRequest;

			//keeping userAgent backward compatible
			if (!string.IsNullOrWhiteSpace(config.UserAgent))
			{
				_tag = _tag + "," + config.UserAgent;
			}

			while (isValidToken && totalRetries < maxRetryAllowed)
			{
				totalRetries++;
				try
				{
					var bytes = Encoding.UTF8.GetBytes(message);
					webRequest = CreateWebRequest(config, _tag);

					using (var dataStream = webRequest.GetRequestStream())
					{
						dataStream.Write(bytes, 0, bytes.Length);
						dataStream.Flush();
						dataStream.Close();
					}
					webResponse = (HttpWebResponse)webRequest.GetResponse();
					webResponse.Close();  
					break;
				}

				catch (WebException e)
				{
					if (totalRetries == 1)
					{
						var response = (HttpWebResponse)e.Response;
						if (response != null)
						{
							// Check for bad token
							if (response.StatusCode == HttpStatusCode.Forbidden)
							{
								// set valid token flag to false
								setTokenValid(false);
							}
							else
							{
								// store logs to buffer
								storeLogs(message, config, isBulk);
							}
							printErrorMessage(e.Message);
						}
						else
						{
							// store logs to buffer
							storeLogs(message, config, isBulk);
						}
					}
				}

				finally
				{
					webRequest = null;
					webResponse = null;
					GC.Collect();
				}
			 }
		 }

		public void Send(ILogglyAppenderConfig config, string message, bool isbulk)
		{
			if (isValidToken)
			{
				string _tag = config.Tag;

				//keeping userAgent backward compatible
				if (!string.IsNullOrWhiteSpace(config.UserAgent))
				{
					_tag = _tag + "," + config.UserAgent;
				}
				var bytes = Encoding.UTF8.GetBytes(message);
				var webRequest = CreateWebRequest(config, _tag);

				using (var dataStream = webRequest.GetRequestStream())
				{
					dataStream.Write(bytes, 0, bytes.Length);
					dataStream.Flush();
					dataStream.Close();
				}
				var webResponse = (HttpWebResponse)webRequest.GetResponse();
				webResponse.Close();
			}
		}

		protected virtual HttpWebRequest CreateWebRequest(ILogglyAppenderConfig config, string tag)
		{
			var url = String.Concat(config.RootUrl, config.LogMode, config.InputKey);
				//adding userAgent as tag in the log
				url = String.Concat(url, "/tag/" + tag);
			HttpWebRequest request = null;
			request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "POST";
			request.ReadWriteTimeout = request.Timeout = config.TimeoutInSeconds * 1000;
			request.UserAgent = config.UserAgent;
			request.KeepAlive = true;
			request.ContentType = "application/json";
			return request;
		}
	}  
}
