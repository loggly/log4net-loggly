using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace log4net.loggly
{
    internal class LogglyAsyncBuffer : ILogglyAsyncBuffer
    {
        private readonly Config _config;
        private readonly ConcurrentQueue<string> _messages = new ConcurrentQueue<string>();
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
        private readonly ManualResetEvent _readyToSendEvent = new ManualResetEvent(false);
        private readonly ManualResetEvent _flushingEvent = new ManualResetEvent(false);
        private volatile bool _sendInProgress;
        private readonly ILogglyClient _client;

        public LogglyAsyncBuffer(Config config, ILogglyClient client)
        {
            _config = config;
            _client = client;
            var sendingThread = new Thread(DoSend)
            {
                Name = "LogglySendThread",
                IsBackground = true
            };
            sendingThread.Start();
        }

        /// <summary>
        /// Buffer message to be sent to Loggly
        /// </summary>
        public void BufferForSend(string message)
        {
            _messages.Enqueue(message);

            // initiate send if sending one by one or if there is already enough messages for batch
            if (_messages.Count >= _config.BufferSize)
            {
                _readyToSendEvent.Set();
            }

            // keep the queue size under limit if any limit is set
            if (_config.MaxLogQueueSize > 0)
            {
                while (_messages.Count > _config.MaxLogQueueSize)
                {
                    _messages.TryDequeue(out _);
                }
            }
        }

        /// <summary>
        /// Flush any buffered messages right now.
        /// This method returns once all messages are flushed or when timeout expires.
        /// If new messages are coming during flush they will be included and may delay flush operation.
        /// </summary>
        public bool Flush(TimeSpan maxWait)
        {
            Stopwatch flushWatch = Stopwatch.StartNew();
            SpinWait spinWait = new SpinWait();
            int messagesCount;
            while (((messagesCount = _messages.Count) > 0 || _sendInProgress) && flushWatch.Elapsed < maxWait)
            {
                _flushingEvent.Set();
                spinWait.SpinOnce();
            }

            return messagesCount == 0 && !_sendInProgress;
        }

        private void DoSend()
        {
            var sendBuffer = new string[_config.BufferSize];

            // WaitAny returns index of completed task or WaitTimeout value (number) in case of timeout.
            // We want to continue unless _stopEvent was set, so unless returned value is 0 - index of _stopEvent
            int flushingHandleIndex = 2;
            var handles = new WaitHandle[] { _stopEvent, _readyToSendEvent, _flushingEvent };

            int triggeredBy;
            while ((triggeredBy = WaitHandle.WaitAny(handles, _config.SendInterval)) != 0)
            {
                // allow sending partial buffer only when it was triggered by timeout or flush
                if (triggeredBy != WaitHandle.WaitTimeout && triggeredBy != flushingHandleIndex && _messages.Count < sendBuffer.Length)
                {
                    _readyToSendEvent.Reset();
                    continue;
                }

                _sendInProgress = true;
                int sendBufferIndex = 0;
                int bulkSize = 0;
                while (sendBufferIndex < sendBuffer.Length 
                    && _messages.TryPeek(out var message)
                    && bulkSize + message.Length <= _config.MaxBulkSizeBytes)
                {
                    bulkSize += message.Length;
                    // peek/dequeue happens only in one thread so what we peeked above is what we dequeue here
                    _messages.TryDequeue(out _);
                    sendBuffer[sendBufferIndex++] = message;
                }

                if (sendBufferIndex > 0)
                {
                    _client.Send(sendBuffer, sendBufferIndex);
                }
                _sendInProgress = false;
            }
        }

        public void Dispose()
        {
            _stopEvent.Set();
        }
    }
}