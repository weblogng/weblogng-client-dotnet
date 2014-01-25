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
using System.Threading.Tasks.Schedulers;

namespace weblog
{

	public class Logger
	{
		private String id;
		private FinishedMetricsFlusher finishedMetricsFlusher;
		private IDictionary<String, Timer> inProgressTimers = new Dictionary<String, Timer> ();

		internal Logger (FinishedMetricsFlusher flusher)
		{
			Console.WriteLine ("WeblogNG: initializing...");
			this.id = System.Guid.NewGuid ().ToString ();

			if (flusher == null) {
				throw new ArgumentException ("Logger requires a FinishedMetricsFlusher, but was null");
			} else {
				this.finishedMetricsFlusher = flusher;
			}

		}

		internal FinishedMetricsFlusher FinishedMetricsFlusher 
		{
			get { return this.finishedMetricsFlusher; }
		}

		///<summary>
		/// Creates a Timer and returns it.  The Timer's Stopwatch will be started
		/// automatically.  The Timer class is Disposable so it may be used with a
		/// using statment to time a block of code.
		/// </summary>
		/// 
		/// <example>
		/// <code>
		/// using(logger.CreateTimer("operation_a_execution_time"))
		/// { 
		///   // ... perform operation 'a' ...
		/// }
		/// </code>
		/// </example>
		/// 
		/// <returns>
		/// Returns the timer that was created and started.
		/// </returns>
		public Timer CreateTimer(String MetricName) 
		{
			return new Timer(MetricName, this);	
		}

		internal void AddToFinishedTimers(Timer timer)
		{
			this.finishedMetricsFlusher.AddToFinishedTimers (timer);
		}

		internal LinkedList<Timer> GetFinishedTimers()
		{
			return this.finishedMetricsFlusher.GetFinishedTimers ();
		}

		///<summary>
		/// Creates a Timer, stores it in a Dictionary held by the Logger for later refernce, and returns it.  
		/// The Timer's Stopwatch will be started automatically.  The Dictionary is not thread-safe, do not use
		/// RecordStart or its companion, RecordFinish from code where a given metric name may be used concurrently.
		/// </summary>
		/// 
		/// <example>
		/// <code>
		/// logger.RecordStart ("operation_a_execution_time");
		/// // ... perform operation 'a' ...
		/// logger.RecordFinish ("operation_a_execution_time");
		/// </code>
		/// </example>
		/// 
		/// <returns>
		/// Returns the timer that was created and started.
		/// </returns>
		public Timer RecordStart (String metricName)
		{
			Timer timer = CreateTimer (metricName);
			this.inProgressTimers.Add (metricName, timer);
			return timer;
		}

		internal bool HasTimer(String metricName)
		{
			return this.inProgressTimers.ContainsKey (metricName);
		}
		

		///<summary>
		/// Locates and removes the timer with the provided metric name from the Logger's Dictionary and marks it as finished.
		/// The finished timer is queued for flushing.  The Dictionary is not thread-safe, do not use
		/// RecordFinish or its companion, RecordStart from code where a given metric name may be used concurrently.
		/// </summary>
		/// 
		/// <example>
		/// <code>
		/// logger.RecordStart ("operation_a_execution_time");
		/// // ... perform operation 'a' ...
		/// logger.RecordFinish ("operation_a_execution_time");
		/// </code>
		/// </example>
		/// 
		/// <returns>
		/// Returns the timer that was finished, null if one by that name was not present in logger's collection.
		/// </returns>
		public Timer RecordFinish (String metricName)
		{
			Timer timer = this.inProgressTimers [metricName];
			if (timer != null) {
				this.inProgressTimers.Remove (metricName);
				timer.Dispose ();
			}
			return timer;
		}
		
		
		///<summary>
		/// Records that the timer for the provided metricName has finished and triggers a flush 
		/// of the metrics to the API.
		/// </summary>
		/// 
		/// <returns>
		/// Returns the timer that was finished, null if one by that name was not present in logger's collection.
		/// </returns>
		public Timer RecordFinishAndSendMetric (String metricName)
		{
			Timer timer = this.RecordFinish (metricName);

			if(timer != null){
				this.finishedMetricsFlusher.Flush (new object());
			}

			return timer;
		}
		

		private long epochTimeInMilliseconds ()
		{
			return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		}
				

		override public String ToString ()
		{
			return String.Format ("[Logger id: {0}, flusher: {1} ]", id, finishedMetricsFlusher);
		}

		///<summary>
		/// Creates a Logger configured to log to production asynchronously over websockets 
		/// using the provided api key.
		/// </summary>
		/// <returns>
		/// A logger initialized for production.
		/// </returns>
		public static Logger CreateAsyncLogger(String apiKey){
			LoggerAPIConnectionWS apiConnection = new LoggerAPIConnectionWS ("ec2-174-129-123-237.compute-1.amazonaws.com:9000", apiKey);
			AsyncFinishedMetricsFlusher flusher = new AsyncFinishedMetricsFlusher (apiConnection);
			return new Logger(flusher);
		}
	}
	
	internal interface FinishedMetricsFlusher {
		void Flush (Object stateInfo);

		void AddToFinishedTimers (Timer timer);

		LinkedList<Timer> GetFinishedTimers ();
	}

	abstract class BaseFinishedMetricsFlusher : FinishedMetricsFlusher {

		private Object finishedTimersLock = new Object();
		private LinkedList<Timer> FinishedTimers = new LinkedList<Timer>();

		abstract public void Flush (Object stateInfo);

		public void AddToFinishedTimers(Timer timer)
		{
			lock (finishedTimersLock) {
				FinishedTimers.AddLast (timer);
			}
		}

		public LinkedList<Timer> GetFinishedTimers()
		{
			lock (finishedTimersLock) {
				return FinishedTimers;
			}
		}

		internal LinkedList<Timer> DrainFinishedTimersForFlush()
		{
			LinkedList<Timer> oldFinishedTimers;
			lock (finishedTimersLock) {
				oldFinishedTimers = FinishedTimers;
				FinishedTimers = new LinkedList<Timer> ();
			}
			return oldFinishedTimers;
		}
	}


	class AsyncFinishedMetricsFlusher : BaseFinishedMetricsFlusher
	{
		//there are a number of options (understatement) for implementing async operations in C#:
		//best bet currently looks like using a 'Thread Timer:
		//http://msdn.microsoft.com/en-us/library/swx5easy.aspx
		//
		//could also use the AsyncOperationManager, but still need something to kick-off the operation:
		//http://msdn.microsoft.com/en-us/library/9hk12d4y(v=vs.110).aspx

		private LoggerAPIConnection apiConnection;
		private TaskFactory taskFactory;

		internal AsyncFinishedMetricsFlusher(LoggerAPIConnection apiConnection)
		{
			this.apiConnection = apiConnection;

			LimitedConcurrencyLevelTaskScheduler lcts = new LimitedConcurrencyLevelTaskScheduler(1);
			this.taskFactory = new TaskFactory(lcts);

			TimerCallback callback = this.Flush;
			new System.Threading.Timer (callback, new object(), 10000, 10000);
		}

		internal LoggerAPIConnection LoggerAPIConnection 
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
				taskFactory.StartNew (() => apiConnection.sendMetrics(timersToFlush));
			}

		}

	}

	class MetricUtilities 
	{
		private static String INVALID_CHAR_PATTERN = "[^\\w\\d_-]";

		internal static String sanitizeMetricName (String metricName)
		{
			return Regex.Replace(metricName, INVALID_CHAR_PATTERN, "_"); 
		}

	}
	
	internal interface LoggerAPIConnection {
		void sendMetrics(ICollection<Timer> timers);
	}

	internal class LoggerAPIConnectionWS : LoggerAPIConnection {

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

		override public String ToString ()
		{
			return String.Format ("[LoggerAPIConnectionWS apiUrl: {1}, apiKey: #{2}]", apiUrl, apiKey);
		}

		/**
		 * Creates message from metric name and its value
		 */
		private String createMetricMessage (String metricName, String metricValue)
		{
			String sanitizedMetricName = MetricUtilities.sanitizeMetricName (metricName);
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
			Console.WriteLine ("WeblogNG: Connected");
		}

		private void websocket_Error (object sender, ErrorEventArgs e)
		{
			Console.WriteLine ("WeblogNG: Error ");
		}

		private void websocket_MessageReceived (object sender, MessageReceivedEventArgs e)
		{
			Console.WriteLine ("WeblogNG: Message " + e.Message);
		}

		private static void websocket_Closed (object sender, System.EventArgs e)
		{
			Console.WriteLine ("WeblogNG: Connection closed");
		}


	}
}

