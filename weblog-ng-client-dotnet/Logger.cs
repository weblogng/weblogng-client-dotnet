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
		private String apiKey;
		private String apiHost;
		private FinishedMetricsFlusher finishedMetricsFlusher;
		private Object finishedTimersLock = new Object();

		private LinkedList<Timer> FinishedTimers = new LinkedList<Timer>();

		//retaining a reference to flushFinishedTimersTimer to avoid GC, but is this necessary?
		private System.Threading.Timer flushFinishedTimersTimer;

		public Logger (String _apiHost, String _apiKey, FinishedMetricsFlusher flusher=null)
		{
			Console.WriteLine ("WeblogNG: initializing...");
			this.id = System.Guid.NewGuid ().ToString ();
			this.apiHost = _apiHost;
			this.apiKey = _apiKey;

			if (flusher == null) {
				this.finishedMetricsFlusher = new AsyncFinishedMetricsFlusher (this, new LoggerAPIConnectionWS (this.apiHost, this.apiKey));
				AutoResetEvent autoEvent = new AutoResetEvent (false);
				TimerCallback callback = this.finishedMetricsFlusher.Flush;
				flushFinishedTimersTimer = new System.Threading.Timer (callback, autoEvent, 10000, 10000);
			} else {
				this.finishedMetricsFlusher = flusher;
			}
		}

		public String ApiHost
		{
			get { return apiHost; }
		}

		public String ApiKey
		{
			get { return apiKey; }
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
			return String.Format ("[Logger id: {0}, apiHost: {1}, apiKey: #{2} ]", id, apiHost, apiKey);
		}
	}

	public interface FinishedMetricsFlusher {
		void Flush (Object stateInfo);
	}


	class AsyncFinishedMetricsFlusher : FinishedMetricsFlusher
	{
		private Logger logger;
		private LoggerAPIConnection apiConnection;

		public AsyncFinishedMetricsFlusher(Logger logger, LoggerAPIConnection apiConnection)
		{
			this.logger = logger;
			this.apiConnection = apiConnection;
		}

		public void Flush(Object stateInfo)
		{
			//this method must be re-entrant safe.  it is possible for this method to be executed simultaneously on two threads
			LinkedList<Timer> timersToFlush = logger.DrainFinishedTimersForFlush ();
			Task.Factory.StartNew (() => apiConnection.sendMetrics(timersToFlush));

			AutoResetEvent autoEvent = (AutoResetEvent)stateInfo;
			autoEvent.Set ();
		}

	}

	interface LoggerAPIConnection {
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

