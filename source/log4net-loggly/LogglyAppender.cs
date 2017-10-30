using log4net.Appender;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Timer = System.Timers;



namespace log4net.loggly
{
	public class LogglyAppender : AppenderSkeleton
	{
		List<string> lstLogs = new List<string>();
		string[] arr = new string[100];
		public readonly string InputKeyProperty = "LogglyInputKey";
		public ILogglyFormatter Formatter = new LogglyFormatter();
		public ILogglyClient Client = new LogglyClient();
		public LogglySendBufferedLogs _sendBufferedLogs = new LogglySendBufferedLogs();
		private ILogglyAppenderConfig Config = new LogglyAppenderConfig();
		public string RootUrl { set { Config.RootUrl = value; } }
		public string InputKey { set { Config.InputKey = value; } }
		public string UserAgent { set { Config.UserAgent = value; } }
		public string LogMode { set { Config.LogMode = value; } }
		public int TimeoutInSeconds { set { Config.TimeoutInSeconds = value; } }
		public string Tag { set { Config.Tag = value; } }
		public string LogicalThreadContextKeys { set { Config.LogicalThreadContextKeys = value; } }
		public string GlobalContextKeys { set { Config.GlobalContextKeys = value; } }
		public int BufferSize { set { Config.BufferSize = value; } }

		private LogglyAsyncHandler LogglyAsync;

		public LogglyAppender()
		{
			LogglyAsync = new LogglyAsyncHandler();
			Timer.Timer t = new Timer.Timer();
			t.Interval = 5000;
			t.Enabled = true;
			t.Elapsed += t_Elapsed;
		}

		void t_Elapsed(object sender, Timer.ElapsedEventArgs e)
		{
			if (lstLogs.Count != 0)
			{
				SendAllEvents(lstLogs.ToArray());
			}
			_sendBufferedLogs.sendBufferedLogsToLoggly(Config, IsBulkMode());
		}

		protected override void Append(LoggingEvent loggingEvent)
		{
			SendLogAction(loggingEvent);
		}

		private void SendLogAction(LoggingEvent loggingEvent)
		{
			//we should always format event in the same thread as 
			//many properties used in the event are associated with the current thread
			//like threadname, ndc stacks, threadcontent properties etc.

			//initializing a string for the formatted log
			string _formattedLog = string.Empty;

			//if Layout is null then format the log from the Loggly Client
			if (this.Layout == null)
			{
				Formatter.AppendAdditionalLoggingInformation(Config, loggingEvent);
				_formattedLog = Formatter.ToJson(loggingEvent);
			}
			else
			{
				_formattedLog = Formatter.ToJson(RenderLoggingEvent(loggingEvent), loggingEvent.TimeStamp);
			}

			//check if logMode is bulk or inputs
			if (IsBulkMode())
			{
				addToBulk(_formattedLog);
			}
			else if (IsInputsMode())
			{
				//sending _formattedLog to the async queue
				LogglyAsync.PostMessage(_formattedLog, Config);
			}
		}

		public void addToBulk(string log)
		{
			// store all events into a array max lenght is 100
			lstLogs.Add(log.Replace("\n", ""));
			if (lstLogs.Count == 100)
			{
				SendAllEvents(lstLogs.ToArray());
			}
		}

		private void SendAllEvents(string[] events)
		{
			lstLogs.Clear();
			String bulkLog = String.Join(System.Environment.NewLine, events);
			LogglyAsync.PostMessage(bulkLog, Config);
		}

	    private bool IsBulkMode()
	    {
	        return Config.LogMode == "bulk/";
	    }

	    private bool IsInputsMode()
	    {
	        return Config.LogMode == "inputs/";
	    }

	    protected override void OnClose()
	    {
	        if (IsBulkMode() && lstLogs.Any())
	        {
	            SendAllEvents(lstLogs.ToArray());
	        }

	        LogglyAsync.Flush();

	        base.OnClose();
	    }
    }
}