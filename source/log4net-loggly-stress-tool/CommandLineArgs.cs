using System;

namespace log4net_loggly_stress_tool
{
    internal class CommandLineArgs
    {
        public int NumEvents { get; private set; } = 1000;
        public int NumLoggingThreads { get; private set; } = 1;
        public int ExceptionFrequency { get; private set; } = 0;
        public TimeSpan SendDelay { get; private set; } = TimeSpan.Zero;
        public int LogsPerSecond { get; set; } = 0;

        public static CommandLineArgs Parse(string[] args)
        {
            var result = new CommandLineArgs();

            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-n":
                        case "--num-events":
                            i++;
                            result.NumEvents = int.Parse(args[i]);
                            if (result.NumEvents < 1)
                                throw new ArgumentException("Number of events must be >= 1");
                            break;
                        case "-t":
                        case "--num-threads":
                            i++;
                            result.NumLoggingThreads = int.Parse(args[i]);
                            if (result.NumLoggingThreads < 1)
                                throw new ArgumentException("Number of threads must be >= 1");
                            break;
                        case "-d":
                        case "--send-delay":
                        {
                            i++;
                            var value = int.Parse(args[i]);
                            if (value < 0)
                                throw new ArgumentException("Delay must be >= 0");
                            result.SendDelay = TimeSpan.FromMilliseconds(value);
                        }
                            break;
                        case "-l":
                        case "--logs-per-second":
                        {
                            i++;
                            var value = int.Parse(args[i]);
                            if (value <= 0)
                                throw new ArgumentException("Logs-per-second must be > 0");
                                result.LogsPerSecond = value;
                        }
                            break;
                        case "-e":
                        case "--exception-every":
                            i++;
                            result.ExceptionFrequency = int.Parse(args[i]);
                            if (result.ExceptionFrequency < 0)
                                throw new ArgumentException("Exception frequency must be >= 0");
                            break;
                        default:
                            PrintHelp();
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                PrintHelp();
            }

            return result;
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"
Loggly log4net logger stress testing tool.
Tool is generating log messages in one or more threads and logs them to logger. 
Fake HTTP layer is used to fake sending data to Loggly, no data are really sent out.

Usage: log4net-loggly-stress-tool.exe [-n|--num-threads <NUM_THREADS>] [-d|--send-delay <SEND_DELAY_MS>] [-e|--exception-every <NUMBER>]
    -n|--num-events      - Number of events to send. Must be > 0. Default: 1000
    -l|--logs-per-second - How many logs per second should be generated. Total number is still defined by -n, this value just slows down the generator to given frequency.
    -t|--num-threads     - Number of threads used to generate logs. Must be > 0. Default: 1.
    -d|--send-delay      - Delay for one simulated send to Loggly servers in milliseconds. Must be >= 0. Default: 0
    -e|--exception-every - Log error with exception every N logs. Must be >= 0. Default: 0 - never");

            Environment.Exit(0);
        }
    }
}