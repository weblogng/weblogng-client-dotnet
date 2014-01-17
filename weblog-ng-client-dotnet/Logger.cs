using System;
using System.Net.Sockets;
using System.Net;
using WebSocket4Net;
using SuperSocket.ClientEngine;
using System.Text.RegularExpressions;

namespace weblog
{
	public class Logger
	{
		private static String INVALID_CHAR_PATTERN = "[^\\w\\d_-]";
		private String id;
		private String apiUrl;
		private WebSocket websocket;
		private String apiKey;
		private String apiHost;

		public Logger (String _apiHost, String _apiKey)
		{
			Console.WriteLine ("Weblogng: initializing...");
			this.id = System.Guid.NewGuid ().ToString ();
			this.apiKey = _apiKey;
			this.apiHost = _apiHost;
			apiUrl = "ws://" + apiHost + "/log/ws";
			websocket = new WebSocket (apiUrl);
			
			websocket.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs> (websocket_Error);
			websocket.Opened += new EventHandler (websocket_Opened);
			websocket.Closed += new EventHandler (websocket_Closed);
			websocket.MessageReceived += new EventHandler<MessageReceivedEventArgs> (websocket_MessageReceived);
			websocket.Open ();
			Console.WriteLine ("Weblogng: Websocket version:" + websocket.Version);
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
		
		/**
		 * Sends metric and it's value to the server
		 */
		public void sendMetric (String metricName, String metricValue)
		{
			String metricMessage = createMetricMessage (metricName, metricValue);
			websocket.Send (metricMessage);
		}

		/**
		 * Creates message from metric name and its value
		 */
		private String createMetricMessage (String metricName, String metricValue)
		{
			String sanitizedMetricName = sanitizeMetricName (metricName);
			return String.Format ("v1.metric {0} {1} {2} ", apiKey, sanitizedMetricName, metricValue);
		}
		

		public static String sanitizeMetricName (String metricName)
		{
			return Regex.Replace(metricName, INVALID_CHAR_PATTERN, "_"); 
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
			return String.Format ("[Logger id: {0}, apiHost: {1}, apiKey: #{2} ]", id, apiUrl, apiKey);
		}		
	}
}

