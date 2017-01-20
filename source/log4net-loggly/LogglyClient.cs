using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Linq;

namespace log4net.loggly
{
	public class LogglyClient : ILogglyClient
	{
        static bool isValidToken = true;
        public static void setValidInvalidFlag(bool flag)
        {
           isValidToken = flag;
        }

        public virtual void Send(ILogglyAppenderConfig config, string message)
        {
            int maxRetryAllowed = 5;
            int totalRetries = 0;

            string _tag = config.Tag;
            bool isBulk = config.LogMode.Contains("bulk");

            List<string> messageBulk = new List<string>();
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
                    var webRequest = CreateWebRequest(config, _tag);  
                    
                        using (var dataStream = webRequest.GetRequestStream())
                        {
                            dataStream.Write(bytes, 0, bytes.Length);
                            dataStream.Flush();
                            dataStream.Close();
                        }

                        var webResponse = webRequest.GetResponse();
                        webResponse.Close();
                        break;                    
                }

                catch (WebException e) {
                    var response = (HttpWebResponse)e.Response;
                    if (response != null) 
                    {
                        if (response.StatusCode == HttpStatusCode.Forbidden)  //Check for bad token
                        {
                            setValidInvalidFlag(false);
                        }
                        if (totalRetries == 1) Console.WriteLine("Loggly error: {0}", e.Message);
                    }

                    else if (totalRetries == 1)
                    {
                        if (isBulk)
                        {
                            messageBulk = message.Split('\n').ToList();
                            LogglyStoreLogsInBuffer.storeBulkLogs(config, messageBulk, isBulk);
                        }
                        else
                        {
                            LogglyStoreLogsInBuffer.storeInputLogs(config, message, isBulk);
                        }
                    }
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
			var request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "POST";
			request.ReadWriteTimeout = request.Timeout = config.TimeoutInSeconds * 1000;
			request.UserAgent = config.UserAgent;
			request.KeepAlive = true;
			request.ContentType = "application/json";
			return request;
		}
    }  
}
