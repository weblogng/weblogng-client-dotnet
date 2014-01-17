using System;
using System.Net.Sockets;
using System.Net;
using WebSocket4Net;
using SuperSocket.ClientEngine;

namespace weblog
{
	public class Logger
	{
		private String id;
		private String apiUrl;
		private Socket serverSocket;
		private WebSocket websocket;
		private String apiKey;

		public Logger (String apiHost, String apiKey)
		{
			Console.WriteLine("Weblogng: initializing...");
			this.apiKey = apiKey;
			apiUrl = "ws://"+apiHost+"/log/ws";
			websocket = new WebSocket (apiUrl);
			
			websocket.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs> (websocket_Error);
			websocket.Opened += new EventHandler (websocket_Opened);
			websocket.Closed += new EventHandler (websocket_Closed);
			websocket.MessageReceived += new EventHandler<MessageReceivedEventArgs> (websocket_MessageReceived);
			websocket.Open ();
			Console.WriteLine("Weblogng: Websocket version:"+websocket.Version);
		}
		
		private void websocket_Opened (object sender, EventArgs e)
		{
			Console.WriteLine("Weblogng: Connected");
		}
		
		private void websocket_Error(object sender, ErrorEventArgs e) {
			Console.WriteLine("Weblogng: Error ");
		}

		private static void websocket_MessageReceived(object sender, MessageReceivedEventArgs e) {
			Console.WriteLine("Weblogng: Message "+e.Message);
		}
		
		private static void websocket_Closed(object sender, System.EventArgs e) {
			Console.WriteLine("Weblogng: Connection closed");
		}

		/*
		private void createMetricMessage (metricName, metricValue, timestamp = weblog.epochTimeInSeconds()) ->
			sanitizedMetricName = @_sanitizeMetricName(metricName)
		                  return "v1.metric #{@apiKey} #{sanitizedMetricName} #{metricValue} #{timestamp}"
		}
*/


	private long epochTimeInMilliseconds() {
			return 		DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

	}




	/*
_createMetricMessage: (metricName, metricValue, timestamp = weblog.epochTimeInSeconds()) ->
    sanitizedMetricName = @_sanitizeMetricName(metricName)
    return "v1.metric #{@apiKey} #{sanitizedMetricName} #{metricValue} #{timestamp}"

	 */ 


		private String sanitizedMetricName;

		private String createMetricMessage(String metricName, String metricValue) {
			return "v1.metric "+apiKey+" "+sanitizedMetricName+" "+metricValue+" ";

		}

		public void sendMetric(String metricName, String metricValue) {
			String metricMessage = createMetricMessage (metricName, metricValue);
			websocket.Send (metricMessage);
		}


		/*
		sendMetric: (metricName, metricValue) ->
		metricMessage = @_createMetricMessage(metricName, metricValue)
		            @webSocket.send(metricMessage)
*/


	}
	
		
}

