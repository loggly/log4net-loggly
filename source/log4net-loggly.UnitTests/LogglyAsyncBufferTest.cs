using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using log4net.loggly;
using Moq;
using Xunit;

namespace log4net_loggly.UnitTests
{
    public class LogglyAsyncBufferTest
    {
        private readonly Config _config;
        private readonly Mock<ILogglyClient> _clientMock;
        private CountdownEvent _allSentEvent;
        private readonly ManualResetEvent _allowSendingEvent;
        private readonly List<string> _sentMessages;

        private static readonly TimeSpan MaxWaitTime = TimeSpan.FromSeconds(5);

        public LogglyAsyncBufferTest()
        {
            _config = new Config
            {
                // make sure we don't send by time expiration unless explicitly set
                SendInterval = TimeSpan.FromDays(1) 
            };
            _clientMock = new Mock<ILogglyClient>();
            _sentMessages = new List<string>();
            _allowSendingEvent = new ManualResetEvent(true);
        }


        [Fact]
        public void Bulk_SendsMessagesWhenBufferSizeReached()
        {
            _config.BufferSize = 3;
            ExpectBulkSends(1);
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            buffer.BufferForSend("test message 1");
            buffer.BufferForSend("test message 2");
            buffer.BufferForSend("test message 3");

            _allSentEvent.Wait(MaxWaitTime).Should().BeTrue("messages should be sent");
            _sentMessages.Should().BeEquivalentTo(new[]
            {
                "test message 1",
                "test message 2",
                "test message 3"
            }, "correct messages should be sent");
        }

        [Fact]
        public void Bulk_DoesNotSendMessagesWhenBufferIsNotFullAndTimeIsNotExpired()
        {
            _config.SendMode = SendMode.Bulk;
            _config.BufferSize = 3;
            ExpectBulkSends(1);
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            buffer.BufferForSend("test message 1");
            buffer.BufferForSend("test message 2");
            // one message missing for full buffer

            _allSentEvent.Wait(TimeSpan.FromSeconds(3)).Should().BeFalse("messages should not be sent");
            _sentMessages.Should().BeEmpty("no messages should be sent");
        }

        [Fact]
        public void Bulk_SendsMessagesWhenTimeExpires()
        {
            _config.SendMode = SendMode.Bulk;
            _config.BufferSize = 3;
            _config.SendInterval = TimeSpan.FromMilliseconds(500);
            ExpectBulkSends(1);
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            buffer.BufferForSend("test message 1");
            buffer.BufferForSend("test message 2");
            // one message missing for full buffer but this time it sends by timer

            _allSentEvent.Wait(MaxWaitTime).Should().BeTrue("messages should be sent");
            _sentMessages.Should().BeEquivalentTo(new[]
            {
                "test message 1",
                "test message 2"
            }, "correct messages should be sent");
        }

        [Fact]
        public void Bulk_SendsAllAvailableMessages()
        {
            _config.SendMode = SendMode.Bulk;
            _config.BufferSize = 3;
            ExpectBulkSends(3);
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            buffer.BufferForSend("test message 1");
            buffer.BufferForSend("test message 2");
            buffer.BufferForSend("test message 3");
            buffer.BufferForSend("test message 4");
            buffer.BufferForSend("test message 5");
            buffer.BufferForSend("test message 6");
            buffer.BufferForSend("test message 7");
            buffer.BufferForSend("test message 8");
            buffer.BufferForSend("test message 9");

            _allSentEvent.Wait(MaxWaitTime).Should().BeTrue("messages should be sent");
            _sentMessages.Should().BeEquivalentTo(new[]
            {
                "test message 1",
                "test message 2",
                "test message 3",
                "test message 4",
                "test message 5",
                "test message 6",
                "test message 7",
                "test message 8",
                "test message 9"
            }, "correct messages should be sent");
        }

        [Fact]
        public void Single_SendsMessagesOneByOne()
        {
            _config.SendMode = SendMode.Single;
            ExpectSingleSends(3);
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            buffer.BufferForSend("test message 1");
            buffer.BufferForSend("test message 2");
            buffer.BufferForSend("test message 3");

            _allSentEvent.Wait(MaxWaitTime).Should().BeTrue("messages should be sent");
            _sentMessages.Should().BeEquivalentTo(new[]
            {
                "test message 1",
                "test message 2",
                "test message 3"
            }, "correct messages should be sent");
        }

        [Fact]
        public void Single_DoesNotBufferMessages()
        {
            _config.SendMode = SendMode.Single;
            _config.BufferSize = 100;
            ExpectSingleSends(1);
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            buffer.BufferForSend("test message 1");

            _allSentEvent.Wait(MaxWaitTime).Should().BeTrue("message should be sent");
            _sentMessages.Should().BeEquivalentTo(new[]
            {
                "test message 1"
            }, "correct message should be sent");
        }

        [Fact]
        public void DiscardsOldestMessagesIfMaxQueueSizeIsSet()
        {
            _config.SendMode = SendMode.Single;
            _config.MaxLogQueueSize = 5;
            ExpectSingleSends(1);
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            _allowSendingEvent.Reset(); // block after first message sending
            buffer.BufferForSend("test message 1");
            _allSentEvent.Wait(MaxWaitTime).Should().BeTrue("first message should be already sent");

            ExpectSingleSends(_config.MaxLogQueueSize);

            buffer.BufferForSend("test message 2");
            buffer.BufferForSend("test message 3");
            buffer.BufferForSend("test message 4");
            buffer.BufferForSend("test message 5");
            buffer.BufferForSend("test message 6");
            buffer.BufferForSend("test message 7");
            buffer.BufferForSend("test message 8");
            buffer.BufferForSend("test message 9");
            
            // now allow sending the rest
            _allowSendingEvent.Set();

            _allSentEvent.Wait(MaxWaitTime).Should().BeTrue("messages should be sent");
            _sentMessages.Should().BeEquivalentTo(new[]
            {
                // message 1 initiated whole sending so it was sent
                "test message 1",
                // messages 2-4 were dropped because they exceeded buffer size when message 1 was being sent
                "test message 5",
                "test message 6",
                "test message 7",
                "test message 8",
                "test message 9",
            }, "correct messages should be sent");
        }

        [Fact]
        public void Flush_FlushesPendingMessages()
        {
            _config.SendMode = SendMode.Bulk;
            _config.BufferSize = 3;
            ExpectBulkSends(1);
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            buffer.BufferForSend("test message 1");
            buffer.BufferForSend("test message 2");
            
            buffer.Flush(MaxWaitTime);

            _sentMessages.Should().BeEquivalentTo(new[]
            {
                "test message 1",
                "test message 2"
            }, "pending messages should be sent");
        }

        [Fact]
        public void Flush_ReturnsTrueIfAllMessagesAreSent()
        {
            _config.SendMode = SendMode.Bulk;
            _config.BufferSize = 3;
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            buffer.BufferForSend("test message 1");
            buffer.BufferForSend("test message 2");

            var result = buffer.Flush(MaxWaitTime);

            result.Should().BeTrue();
        }

        [Fact]
        public void Flush_ReturnsFalseIfNotAllMessagesAreSent()
        {
            _config.SendMode = SendMode.Bulk;
            _config.BufferSize = 3;
            ExpectBulkSends(1);
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            // block sending
            _allowSendingEvent.Reset();
            buffer.BufferForSend("test message 1");
            buffer.BufferForSend("test message 2");

            var result = buffer.Flush(TimeSpan.FromSeconds(1));

            result.Should().BeFalse();
        }


        [Fact]
        public void SentBulkIsLimitedByMaxBulkSize()
        {
            // message of 10 bytes, buffer for 10 messages = 100 bytes
            // max bulk size se to 30 -> bulk size should be limited by this number
            _config.SendMode = SendMode.Bulk;
            _config.MaxBulkSizeBytes = 30;
            _config.BufferSize = 10;
            var oneMessageSize = 10;
            var oneMessage = new String('x', oneMessageSize);

            ExpectBulkSends(1);
            
            var buffer = new LogglyAsyncBuffer(_config, _clientMock.Object);

            for (int i = 0; i < _config.BufferSize; i++)
            {
                buffer.BufferForSend(oneMessage);
            }

            _allSentEvent.Wait(MaxWaitTime).Should().BeTrue("messages should be sent");
            _sentMessages.Should().BeEquivalentTo(new[]
            {
                oneMessage,
                oneMessage,
                oneMessage
            }, "correct messages should be sent");
        }

        private void ExpectBulkSends(int numberOfSends)
        {
            _allSentEvent = new CountdownEvent(numberOfSends);
            _clientMock.Setup(x => x.Send(It.IsAny<string[]>(), It.IsAny<int>()))
                .Callback<string[], int>((m,c) =>
                {
                    _sentMessages.AddRange(m.Take(c));
                    _allSentEvent.Signal();
                    _allowSendingEvent.WaitOne();
                });
        }

        private void ExpectSingleSends(int numberOfSends)
        {
            _allSentEvent = new CountdownEvent(numberOfSends);
            _clientMock.Setup(x => x.Send(It.IsAny<string>()))
                .Callback<string>(m =>
                {
                    _sentMessages.Add(m);
                    _allSentEvent.Signal();
                    _allowSendingEvent.WaitOne();
                });
        }
    }
}
