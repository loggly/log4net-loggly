using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.loggly;

namespace log4net_loggly_stress_tool
{
    public class Program
    {
        private static ILog _log;

        private static long _count = 0;
        
        public static void Main(string[] args)
        {
            var commandLine = CommandLineArgs.Parse(args);

            var client = new TestHttpClient(commandLine.SendDelay);
            // use test HTTP layer
            LogglyClient.HttpClient = client;

            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            log4net.Config.XmlConfigurator.Configure(logRepository);

            _log = LogManager.GetLogger(typeof(Program));

            SetupThreadContext();

            var exception = GetTestException();

            Console.WriteLine("Running test in {0} threads with {1} ms delay for send and exception every {2} messages.",
                commandLine.NumLoggingThreads, commandLine.SendDelay, commandLine.ExceptionFrequency);

            var watch = Stopwatch.StartNew();
            var tasks = new List<Task>(commandLine.NumLoggingThreads);
            for (int i = 0; i < commandLine.NumLoggingThreads; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => SendContinuously(commandLine, exception), TaskCreationOptions.LongRunning));
            }

            Task.WaitAll(tasks.ToArray());

            watch.Stop();

            Console.WriteLine("Test finished. Elasped: {0}", watch.Elapsed);
        }

        private static void SendContinuously(CommandLineArgs commandLine, Exception exception)
        {
            long currentCount = 0;
            while ((currentCount = Interlocked.Increment(ref _count)) <= commandLine.NumEvents)
            {
                if (currentCount % 1000 == 0)
                {
                    Console.WriteLine("Sent: {0}", currentCount);
                }

                if (commandLine.ExceptionFrequency > 0 && currentCount % commandLine.ExceptionFrequency == 0)
                {
                    _log.Error(
                        $"Test message {currentCount} Lorem ipsum dolor sit amet, consectetur adipiscing elit. Phasellus fermentum ligula " +
                        "et ante tincidunt venenatis. Ut pretium, mi laoreet fringilla egestas, mauris quam lacinia dolor, " +
                        "eu maximus nisi mauris vel lorem. Duis a ex eu orci consectetur congue sed sit amet ligula. " +
                        "Aenean congue mollis quam volutpat varius.", exception);
                }
                else
                {
                    _log.Info(
                        $"Test message {currentCount}. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Phasellus fermentum ligula " +
                        "et ante tincidunt venenatis. Ut pretium, mi laoreet fringilla egestas, mauris quam lacinia dolor, " +
                        "eu maximus nisi mauris vel lorem. Duis a ex eu orci consectetur congue sed sit amet ligula. " +
                        "Aenean congue mollis quam volutpat varius.");
                }
            }
        }


        private static Exception GetTestException()
        {
            Exception exception;
            try
            {
                try
                {
                    throw new ArgumentException("inner exception");
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("outer exception", e);
                }
            }
            catch (Exception e)
            {
                exception = e;
            }

            return exception;
        }

        private static void SetupThreadContext()
        {
            ThreadContext.Properties["ThreadProperty1"] = DateTime.Now;
            ThreadContext.Properties["ThreadProperty2"] = new TestClass
            {
                IntProperty = 123,
                StringProperty =
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Phasellus fermentum ligula et ante tincidunt venenatis. " +
                    "Ut pretium, mi laoreet fringilla egestas, mauris quam lacinia dolor, eu maximus nisi mauris vel lorem. " +
                    "Duis a ex eu orci consectetur congue sed sit amet ligula. Aenean congue mollis quam volutpat varius.",
                DatetimeProperty = DateTime.Now
            };
        }

        private class TestClass
        {
            public int IntProperty { get; set; }
            public string StringProperty { get; set; }
            public DateTime DatetimeProperty { get; set; }
        }
    }
}
