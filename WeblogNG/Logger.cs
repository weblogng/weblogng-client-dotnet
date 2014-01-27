using System;
using System.Net.Sockets;
using System.Net;
using WebSocket4Net;
using SuperSocket.ClientEngine;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;

namespace WeblogNG
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

		public LinkedList<Timer> GetFinishedTimers()
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

		public bool HasTimer(String metricName)
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
	
	public interface FinishedMetricsFlusher {
		void Flush (Object stateInfo);

		void AddToFinishedTimers (Timer timer);

		LinkedList<Timer> GetFinishedTimers ();
	}

	public abstract class BaseFinishedMetricsFlusher : FinishedMetricsFlusher {

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

		private LoggerAPIConnection apiConnection;
		private TaskFactory taskFactory;

		public AsyncFinishedMetricsFlusher(LoggerAPIConnection apiConnection)
		{
			this.apiConnection = apiConnection;

			LimitedConcurrencyLevelTaskScheduler lcts = new LimitedConcurrencyLevelTaskScheduler(1);
			this.taskFactory = new TaskFactory(lcts);

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
				taskFactory.StartNew (() => apiConnection.sendMetrics(timersToFlush));
			}

		}

	}

	public class ExceptionUtilities
	{
		/// <summary>
		/// Handles unhandled exceptions by printing details of the exception and whether the runtime is terminating (termination status doesn't appear to mean what you'd think it means, btw).
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="args">Arguments.</param>
		public static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args) 
		{
			Console.WriteLine("Runtime terminating: {0}", args.IsTerminating);
			Exception e = (Exception) args.ExceptionObject;
			Console.WriteLine("UnhandledExceptionHandler caught : " + e.Message);
			Console.WriteLine (e.ToString ());

		}
	}

	public class MetricUtilities 
	{
		private static String INVALID_CHAR_PATTERN = "[^\\w\\d_-]";

		public static String sanitizeMetricName (String metricName)
		{
			return Regex.Replace(metricName, INVALID_CHAR_PATTERN, "_"); 
		}

	}
	
	public interface LoggerAPIConnection {
		void sendMetrics(ICollection<Timer> timers);
	}

	public class CannotSendMetricsException : System.Exception {
		internal CannotSendMetricsException(string message,
			Exception innerException): base(message, innerException)
		{
		}
	}
	
	public class InvalidSocketStateException : System.Exception {
		internal InvalidSocketStateException(string message) :base (message){
		}
	}

	public class OpenConnectionTimeoutException : System.Exception {
		internal OpenConnectionTimeoutException(string message) :base (message){
		}
	}

	public class LoggerAPIConnectionWS : LoggerAPIConnection {

		private String apiKey;
		private String apiUrl;
		private object webSocketLock = new object ();
		private WebSocket webSocket;
		private AutoResetEvent attemptOpenSocketEvent = new AutoResetEvent(false);

		public LoggerAPIConnectionWS(String apiHost, String apiKey){
			this.apiKey = apiKey;
			this.apiUrl = "ws://" + apiHost + "/log/ws";

			//websocket will be created lazily so that complications of websocket management do not occur during construction.
		}

		/// <summary>
		/// Get or create an open web socket connection to the api; will block until either an open socket is available or 
		/// the optionally-specified open timeout is reached.
		/// </summary>
		/// <returns>an web socket open to the api</returns>
		public WebSocket GetOrCreateOpenWebSocket(int openTimeoutInMs=2500)
		{
			lock (this.webSocketLock) {
				if (this.webSocket == null) {
					this.webSocket = new WebSocket (ApiUrl);

					this.attemptOpenSocketEvent = new AutoResetEvent (false);
					this.webSocket.Opened += new EventHandler (websocket_Opened);
					this.webSocket.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs> (websocket_Error);
					this.webSocket.Closed += new EventHandler (websocket_Closed);
					this.webSocket.MessageReceived += new EventHandler<MessageReceivedEventArgs> (websocket_MessageReceived);
				}

				if (WebSocketState.Open == this.webSocket.State) {
					Console.WriteLine ("returning WebSocket in state: {0}", this.webSocket.State);
					return this.webSocket;
				} else if (WebSocketState.None == this.webSocket.State) {

					this.webSocket.Open ();

					if (!attemptOpenSocketEvent.WaitOne (openTimeoutInMs)) {
						throw new OpenConnectionTimeoutException (string.Format ("could not open socket within {0} ms.", openTimeoutInMs));
					}

					Console.WriteLine ("returning WebSocket in state: {0}", this.webSocket.State);
					return this.webSocket;
				} else {
					WebSocketState invalidState = this.webSocket.State;
					this.webSocket = null;
					throw new InvalidSocketStateException (String.Format("Connection to {0} was in state {1}, cannot proceed", ApiUrl, invalidState));
				}
			}
		}

		public String ApiKey
		{
			get { return this.apiKey; }
		}

		public String ApiUrl 
		{
			get { return this.apiUrl; }
		}

		public WebSocket WebSocket
		{
			get { return this.webSocket; }
		}

		override public String ToString ()
		{
			return String.Format ("[LoggerAPIConnectionWS apiUrl: {0}, apiKey: #{1}]", apiUrl, apiKey);
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
			Console.WriteLine ("Sending {0} timers over websocket", timers.Count);

			try {
				WebSocket webSocket = GetOrCreateOpenWebSocket ();
				foreach(Timer timer in timers){
					webSocket.Send(createMetricMessage(timer.MetricName, timer.TimeElapsedMilliseconds.ToString()));
				}
			} catch (Exception e){
				throw new CannotSendMetricsException (String.Format ("Could not send metrics to {0}", ApiUrl), e);
			}

			Console.WriteLine ("Sent {0} timers over websocket", timers.Count);
		}

		private void websocket_Opened (object sender, EventArgs e)
		{
			Console.WriteLine ("WeblogNG: Opened");
			attemptOpenSocketEvent.Set();
		}

		private void DiscardWebSocket()
		{
			lock (webSocketLock) {
				this.webSocket = null;
			}
		}

		private void websocket_MessageReceived (object sender, MessageReceivedEventArgs e)
		{
			Console.WriteLine ("WeblogNG: Message " + e.Message);
		}

		public void websocket_Error (object sender, ErrorEventArgs e)
		{
			Console.WriteLine ("WeblogNG: Error: {0}", e.Exception.Message);
			DiscardWebSocket ();
		}

		public void websocket_Closed (object sender, System.EventArgs e)
		{
			Console.WriteLine ("WeblogNG: Connection closed");
			DiscardWebSocket ();
		}


	}
}

