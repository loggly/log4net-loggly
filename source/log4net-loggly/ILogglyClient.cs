namespace log4net.loggly
{
    public interface ILogglyClient
    {
        void Send(ILogglyAppenderConfig config, string message);
        void Send(ILogglyAppenderConfig config, string message, bool isBulk);
    }
}