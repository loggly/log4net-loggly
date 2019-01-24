using System;
using System.IO;
using System.Net;
using System.Threading;

namespace log4net_loggly_stress_tool
{
    internal class TestHttpClient : WebRequest
    {
        private readonly TimeSpan _sendDelay;

        public TestHttpClient(TimeSpan sendDelay)
        {
            _sendDelay = sendDelay;
        }

        public override WebResponse GetResponse()
        {
            Thread.Sleep(_sendDelay);
            return new TestResponse();
        }

        public override Stream GetRequestStream()
        {
            return new MemoryStream();
        }

        private class TestResponse : WebResponse
        {
        }
    }
}