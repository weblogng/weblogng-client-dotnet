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
		
		public Logger (String apiHost, String apiKey)
		{
			Console.WriteLine("Weblogng: initializing...");
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
		
	}
	
		
}

