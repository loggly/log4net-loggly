namespace log4net.loggly
{
    internal interface ILogglyClient
    {
        /// <summary>
        /// Send array of messages to Loggly
        /// </summary>
        /// <param name="messagesBuffer">Buffer containing messages to send. Buffer does not have to be full.
        /// Number of valid messages in buffer is passed via <paramref name="numberOfMessages"/> parameters.
        /// Anything past this number should be ignored.
        /// </param>
        /// <param name="numberOfMessages">Number of messages from buffer to send.</param>
        void Send(string[] messagesBuffer, int numberOfMessages);
    }
}