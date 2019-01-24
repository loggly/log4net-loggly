using System;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace log4net.loggly
{
    internal class LogglyClient : ILogglyClient
    {
        private readonly Config _config;
        private bool _isTokenValid = true;
        private readonly string _url;
        private const string SinglePath = "inputs/";
        private const string BulkPath = "bulk/";

        // exposing way how web request is created to allow integration testing
        internal static Func<Config, string, WebRequest> WebRequestFactory = CreateWebRequest;

        public LogglyClient(Config config)
        {
            _config = config;
            _url = BuildUrl(config);
        }

        public void Send(string[] messagesBuffer, int numberOfMessages)
        {
            string message = string.Join(Environment.NewLine, messagesBuffer, 0, numberOfMessages);
            int currentRetry = 0;
            // setting MaxSendRetries means that we retry forever, we never throw away logs without delivering them
            while (_isTokenValid && (_config.MaxSendRetries < 0 || currentRetry <= _config.MaxSendRetries))
            {
                try
                {
                    SendToLoggly(message);
                    break;
                }
                catch (WebException e)
                {
                    var response = (HttpWebResponse)e.Response;
                    if (response != null && response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _isTokenValid = false;
                        ErrorReporter.ReportError($"LogglyClient: Provided Loggly customer token '{_config.CustomerToken}' is invalid. No logs will be sent to Loggly.");
                    }
                    else
                    {
                        ErrorReporter.ReportError($"LogglyClient: Error sending logs to Loggly: {e.Message}");
                    }

                    currentRetry++;
                    if (currentRetry > _config.MaxSendRetries)
                    {
                        ErrorReporter.ReportError($"LogglyClient: Maximal number of retries ({_config.MaxSendRetries}) reached. Discarding current batch of logs and moving on to the next one.");
                    }
                }
            }
        }

        private static string BuildUrl(Config config)
        {
            string tag = config.Tag;

            // keeping userAgent backward compatible
            if (!string.IsNullOrWhiteSpace(config.UserAgent))
            {
                tag = tag + "," + config.UserAgent;
            }

            StringBuilder sb = new StringBuilder(config.RootUrl);
            if (sb.Length > 0 && sb[sb.Length - 1] != '/')
            {
                sb.Append("/");
            }

            sb.Append(config.SendMode == SendMode.Single ? SinglePath : BulkPath);
            sb.Append(config.CustomerToken);
            sb.Append("/tag/");
            sb.Append(tag);
            return sb.ToString();
        }

        private void SendToLoggly(string message)
        {
            var webRequest = WebRequestFactory(_config, _url);
            using (var dataStream = webRequest.GetRequestStream())
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                dataStream.Write(bytes, 0, bytes.Length);
                dataStream.Flush();
                dataStream.Close();
            }
            var webResponse = webRequest.GetResponse();
            webResponse.Close();
        }

        internal static WebRequest CreateWebRequest(Config config, string url)
        {
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
