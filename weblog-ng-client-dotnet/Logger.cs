using System;
using System.Net.Sockets;
using System.Net;
using WebSocket4Net;
using SuperSocket.ClientEngine;
using System.Text.RegularExpressions;
using weblog;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace weblog
{
	public class Logger
	{
		private String id;
		private FinishedMetricsFlusher finishedMetricsFlusher;
		private Object finishedTimersLock = new Object();

		private LinkedList<Timer> FinishedTimers = new LinkedList<Timer>();

		public Logger (FinishedMetricsFlusher flusher)
		{
			Console.WriteLine ("WeblogNG: initializing...");
			this.id = System.Guid.NewGuid ().ToString ();

			if (flusher == null) {
				throw new ArgumentException ("Logger requires a FinishedMetricsFlusher, but was null");
			} else {
				this.finishedMetricsFlusher = flusher;
			}

		}

		public FinishedMetricsFlusher FinishedMetricsFlusher 
		{
			get { return this.finishedMetricsFlusher; }
		}

		public Timer CreateTimer(String MetricName) 
		{
			return new Timer(MetricName, this);	
		}
		
		//fixme: should be internal
		public void AddToFinishedTimers(Timer timer)
		{
			lock (finishedTimersLock) {
				FinishedTimers.AddLast (timer);
			}
		}

		//fixme: should be internal
		public LinkedList<Timer> GetFinishedTimers()
		{
			lock (finishedTimersLock) {
				return FinishedTimers;
			}
		}

		//fixme: should be internal
		public LinkedList<Timer> DrainFinishedTimersForFlush()
		{
			LinkedList<Timer> oldFinishedTimers;
			lock (finishedTimersLock) {
				oldFinishedTimers = FinishedTimers;
				FinishedTimers = new LinkedList<Timer> ();
			}
			return oldFinishedTimers;
		}
		


		public void recordStart (String metricName)
		{
			throw new Exception ("Not implemented yet");
		}
		

		public void recordFinish (String metricName)
		{
			throw new Exception ("Not implemented yet");
		}
		
		

		public void recordFinishAndSendMetric (String metricName)
		{
			throw new Exception ("Not implemented yet");
		}
		
		

		public void executeWithTiming (String metricName, Func<object, object> functionToExecute)
		{
			throw new Exception ("Not implemented yet");				
		}
		
		

		private long epochTimeInMilliseconds ()
		{
			return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		}
				

		override public String ToString ()
		{
			return String.Format ("[Logger id: {0}, flusher: {1} ]", id, finishedMetricsFlusher);
		}

		/**
		 * Creates a Logger configured to log to production asynchronously over websockets using
		 * the provided api key.
		 */
		public static Logger CreateAsyncLogger(String apiKey){
			LoggerAPIConnectionWS apiConnection = new LoggerAPIConnectionWS ("ec2-174-129-123-237.compute-1.amazonaws.com:9000", "93c5a127-e2a4-42cc-9cc6-cf17fdac8a7f");
			AsyncFinishedMetricsFlusher flusher = new AsyncFinishedMetricsFlusher (apiConnection);
			return new Logger(flusher);
		}
	}

	public interface FinishedMetricsFlusher {
		void Flush (Object stateInfo);
	}


	public class AsyncFinishedMetricsFlusher : FinishedMetricsFlusher
	{
		//there are a number of options (understatement) for implementing async operations in C#:
		//best bet currently looks like using a 'Thread Timer:
		//http://msdn.microsoft.com/en-us/library/swx5easy.aspx
		//
		//could also use the AsyncOperationManager, but still need something to kick-off the operation:
		//http://msdn.microsoft.com/en-us/library/9hk12d4y(v=vs.110).aspx

		//the Logger and maybe the LoggerAPIConnection should be made available via the [custom] 'state object'
		//that is passed as a parameter on each timer callback.  providing the Logger instance in the state object
		//will help avoid the currently-awkward construction requirement of the flusher needing a Logger and the Logger wanting a Flusher.

		private LoggerAPIConnection apiConnection;

		public AsyncFinishedMetricsFlusher(LoggerAPIConnection apiConnection)
		{
			this.apiConnection = apiConnection;

			AutoResetEvent autoEvent = new AutoResetEvent (false);
			TimerCallback callback = this.Flush;
			new System.Threading.Timer (callback, autoEvent, 10000, 10000);
		}

		public LoggerAPIConnection LoggerAPIConnection 
		{
			get { return this.apiConnection; }
		}

		public void Flush(Object stateInfo)
		{
			Console.WriteLine ("AsyncFinishedMetricsFlusher.Flush called; stateInfo: {0}", stateInfo);

			//todo: retrieve logger from stateInfo
			//this method must be re-entrant safe.  it is possible for this method to be executed simultaneously on two threads
			//LinkedList<Timer> timersToFlush = logger.DrainFinishedTimersForFlush ();
			//Task.Factory.StartNew (() => apiConnection.sendMetrics(timersToFlush));

			AutoResetEvent autoEvent = (AutoResetEvent)stateInfo;
			autoEvent.Set ();
		}

	}

	//fixme: should be internal
	public interface LoggerAPIConnection {
		void sendMetrics(ICollection<Timer> timers);
	}

	public class LoggerAPIConnectionWS : LoggerAPIConnection {

		private static String INVALID_CHAR_PATTERN = "[^\\w\\d_-]";

		private String apiKey;
		private String apiUrl;
		private WebSocket websocket;

		public LoggerAPIConnectionWS(String apiHost, String apiKey){
			this.apiKey = apiKey;
			this.apiUrl = "ws://" + apiHost + "/log/ws";
			websocket = new WebSocket (apiUrl);

			websocket.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs> (websocket_Error);
			websocket.Opened += new EventHandler (websocket_Opened);
			websocket.Closed += new EventHandler (websocket_Closed);
			websocket.MessageReceived += new EventHandler<MessageReceivedEventArgs> (websocket_MessageReceived);
			websocket.Open ();
			Console.WriteLine ("Weblogng: Websocket version:" + websocket.Version);
		}

		public String ApiKey
		{
			get { return this.apiKey; }
		}

		public String ApiUrl 
		{
			get { return this.apiUrl; }
		}


		public static String sanitizeMetricName (String metricName)
		{
			return Regex.Replace(metricName, INVALID_CHAR_PATTERN, "_"); 
		}

		override public String ToString ()
		{
			return String.Format ("[LoggerAPIConnectionWS apiUrl: {1}, apiKey: #{2}]", apiUrl, apiKey);
		}

		/**
		 * Creates message from metric name and its value
		 */
		private String createMetricMessage (String metricName, String metricValue)
		{
			String sanitizedMetricName = sanitizeMetricName (metricName);
			return String.Format ("v1.metric {0} {1} {2} ", apiKey, sanitizedMetricName, metricValue);
		}

		public void sendMetrics(ICollection<Timer> timers){
			Console.WriteLine ("sending timers over ws: " + timers);
		}

		private void websocket_Opened (object sender, EventArgs e)
		{
			Console.WriteLine ("Weblogng: Connected");
		}

		private void websocket_Error (object sender, ErrorEventArgs e)
		{
			Console.WriteLine ("Weblogng: Error ");
		}

		private void websocket_MessageReceived (object sender, MessageReceivedEventArgs e)
		{
			Console.WriteLine ("Weblogng: Message " + e.Message);
		}

		private static void websocket_Closed (object sender, System.EventArgs e)
		{
			Console.WriteLine ("Weblogng: Connection closed");
		}


	}
}

