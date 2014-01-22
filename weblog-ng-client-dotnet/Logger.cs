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
		private IDictionary<String, Timer> inProgressTimers = new Dictionary<String, Timer> ();

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
			this.finishedMetricsFlusher.AddToFinishedTimers (timer);
		}

		//fixme: should be internal
		public LinkedList<Timer> GetFinishedTimers()
		{
			return this.finishedMetricsFlusher.GetFinishedTimers ();
		}


		public Timer RecordStart (String metricName)
		{
			Timer timer = CreateTimer (metricName);
			this.inProgressTimers.Add (metricName, timer);
			return timer;
		}

		public bool HasTimer(String metricName)
		{
			return this.inProgressTimers.ContainsKey (metricName);
		}
		

		/**
		 * Records that the timer for the provided metricName has finished.
		 * 
		 * Returns the timer that was finished, null if one by that name was not 
		 * present in logger's collection.
		 */
		public Timer RecordFinish (String metricName)
		{
			Timer timer = this.inProgressTimers [metricName];
			if (timer != null) {
				this.inProgressTimers.Remove (metricName);
				timer.Dispose ();
			}
			return timer;
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

		//fixme: should be internal
		void AddToFinishedTimers (Timer timer);
		//fixme: should be internal
		LinkedList<Timer> GetFinishedTimers ();
	}

	public abstract class BaseFinishedMetricsFlusher : FinishedMetricsFlusher {

		private Object finishedTimersLock = new Object();
		private LinkedList<Timer> FinishedTimers = new LinkedList<Timer>();

		abstract public void Flush (Object stateInfo);


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
	}


	public class AsyncFinishedMetricsFlusher : BaseFinishedMetricsFlusher
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

			TimerCallback callback = this.Flush;
			new System.Threading.Timer (callback, new object(), 10000, 10000);
		}

		public LoggerAPIConnection LoggerAPIConnection 
		{
			get { return this.apiConnection; }
		}

		override public void Flush(Object stateInfo)
		{
			Console.WriteLine ("AsyncFinishedMetricsFlusher.Flush called; stateInfo: {0}", stateInfo);

			//todo: retrieve logger from stateInfo
			//this method must be re-entrant safe.  it is possible for this method to be executed simultaneously on two threads
			LinkedList<Timer> timersToFlush = DrainFinishedTimersForFlush ();

			if (timersToFlush.Count > 0) {
				Task.Factory.StartNew (() => apiConnection.sendMetrics(timersToFlush));
			}

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
			Console.WriteLine ("WeblogNG: WebSocket version: " + websocket.Version);
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
			foreach(Timer timer in timers){
				websocket.Send(createMetricMessage(timer.MetricName, timer.TimeElapsedMilliseconds.ToString()));
			}
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

