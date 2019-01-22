using System;
using System.Threading;
using log4net.loggly;

namespace log4net_loggly_stress_tool
{
    internal class TestHttpClient : ILogglyHttpClient
    {
        private readonly TimeSpan _sendDelay;

        public TestHttpClient(TimeSpan sendDelay)
        {
            _sendDelay = sendDelay;
        }

        public void Send(ILogglyAppenderConfig config, string tag, string message)
        {
            Thread.Sleep(_sendDelay);
        }
    }
}