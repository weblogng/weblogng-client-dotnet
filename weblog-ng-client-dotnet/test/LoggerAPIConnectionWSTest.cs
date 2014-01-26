using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using NUnit.Framework;
using SuperSocket.Common;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketBase.Logging;
using SuperSocket.SocketBase;
using SuperSocket.SocketEngine;
using SuperWebSocket.SubProtocol;
using SuperWebSocket;
using WebSocket4Net;
using weblog;



namespace weblog.test
{

	[TestFixture()]
	public class LoggerAPIConnectionWSTest
	{

		private static Random random = new Random();

		private WebSocketServer webSocketServer;
		private LinkedList<String> receivedMessages;
		private AutoResetEvent socketOpenedEvent = new AutoResetEvent(false);
		private AutoResetEvent socketClosedEvent = new AutoResetEvent(false);

		private String serverHost = "localhost";
		private int serverPort = 2424;

		[TestFixtureSetUp]
		public virtual void Setup()
		{
			webSocketServer = new WebSocketServer();
			webSocketServer.NewSessionConnected += new SessionHandler<WebSocketSession>(webSocketServer_NewSessionConnected);
			webSocketServer.NewMessageReceived += new SessionHandler<WebSocketSession, string>(webSocketServer_NewMessageReceived);

			webSocketServer.Setup(new ServerConfig
				{
					Port = serverPort,
					Ip = "Any",
					MaxConnectionNumber = 10,
					Mode = SocketMode.Tcp,
					Name = "WeblogNG Integration Testing Server",
					LogAllSocketException = true,
				}, logFactory: new ConsoleLogFactory());
		}

		void webSocketServer_NewSessionConnected (WebSocketSession session)
		{
			Console.WriteLine ("New session connected to test server. {0}", session.ToString ());
			session.Send ("v1.control");
		}

		void webSocketServer_NewMessageReceived(WebSocketSession session, String message)
		{
			this.receivedMessages.AddLast (message);
			Console.WriteLine (String.Format("integration test server received new message: {0}", receivedMessages.Last.Value));
			Console.WriteLine (String.Format ("integration test server has received {0} message(s)", receivedMessages.Count));
		}

		[SetUp]
		public void SetUp()
		{
			StartServer ();
		}

		[TearDown]
		public void TearDown()
		{
			StopServer ();
		}

		public void StartServer()
		{
			receivedMessages = new LinkedList<String> ();
			webSocketServer.Start();
		}

		public void StopServer()
		{
			webSocketServer.Stop();
		}

		protected void webSocketClient_Opened(object sender, EventArgs e)
		{
			socketOpenedEvent.Set();
		}

		protected void webSocketClient_Closed(object sender, EventArgs e)
		{
			socketClosedEvent.Set();
		}



		[Test()]
		public void should_be_configured_via_constructor_params()
		{

			String expectedKey = "key";
			String expectedHost = "somehost:42";
			String expectedUrl = "ws://" + expectedHost + "/log/ws";
			LoggerAPIConnectionWS apiConn = new LoggerAPIConnectionWS (expectedHost, expectedKey);

			Assert.AreEqual (expectedKey, apiConn.ApiKey);
			Assert.AreEqual (expectedUrl, apiConn.ApiUrl);

			Assert.IsNull (apiConn.WebSocket);
		}

		[Test()]
		public void should_create_a_websocket()
		{
			String expectedKey = "key";
			String expectedHost = string.Format("{0}:{1}", serverHost, serverPort);

			LoggerAPIConnectionWS apiConn = new LoggerAPIConnectionWS (expectedHost, expectedKey);

			Assert.IsNull (apiConn.WebSocket);

			WebSocket client = LoggerAPIConnectionWS.CreateWebSocket (apiConn);
			Assert.AreEqual (WebSocketState.None, client.State);
		
		}

		[Test()]
		public void should_get_or_create_a_properly_configured_open_websocket()
		{
			String expectedKey = "key";
			String expectedHost = string.Format("{0}:{1}", serverHost, serverPort);

			LoggerAPIConnectionWS apiConn = new LoggerAPIConnectionWS (expectedHost, expectedKey);

			Assert.IsNull (apiConn.WebSocket);

			WebSocket client = apiConn.GetOrCreateOpenWebSocket ();

			//verify users of GetOrCreateWebSocket should not have to manage the socket opening process
			//clients should expect GetOrCreateWebSocket to block and return an open conn or throw an exception
			//note: there are no waits between GetOrCreateWebSocket and the assertion that the socket is in an open state

			Assert.AreEqual (WebSocketState.Open, client.State);

			client.Closed += new EventHandler (webSocketClient_Closed);

			client.Close();

			if (!socketClosedEvent.WaitOne(1000))
				Assert.Fail("Failed to close session ontime");

			Assert.AreEqual(WebSocketState.Closed, client.State);
		}

		private LoggerAPIConnectionWS MakeAPIConnToTestServer()
		{
			String expectedKey = "key";
			String expectedHost = string.Format("{0}:{1}", serverHost, serverPort);

			LoggerAPIConnectionWS apiConn = new LoggerAPIConnectionWS (expectedHost, expectedKey);
			return apiConn;
		}

		Logger MakeLogger(){
			return new Logger (new MockFinishedMetricsFlusher());
		}


		private void WaitForMetricsToBeReceived()
		{
			Thread.Sleep (250);
		}

		private LinkedList<Timer> MakeTestTimers(String metricName, int numTimers)
		{
			Logger logger = MakeLogger ();
			LinkedList<Timer> timers = new LinkedList<Timer> ();
			for (int i = 0; i < numTimers; i++)
			{
				Timer t = new Timer (metricName, logger);
				Thread.Sleep (random.Next(0, 25));
				t.Stop ();
				timers.AddLast (t);
			}
			return timers;
		}

		[Test()]
		public void should_send_metrics_to_the_server()
		{
			LoggerAPIConnectionWS apiConn = MakeAPIConnToTestServer ();

			LinkedList<Timer> timers = MakeTestTimers ("should_send_metrics_to_the_server", 50);
			apiConn.sendMetrics (timers);

			WaitForMetricsToBeReceived ();

			Assert.AreEqual (timers.Count, this.receivedMessages.Count);

		}

		[Test()]
		public void should_reconnect_to_server_when_connection_is_broken_for_short_duration()
		{
			LoggerAPIConnectionWS apiConn = MakeAPIConnToTestServer ();

			LinkedList<Timer> timersBeforeFail = MakeTestTimers ("before-failure", 3);
			apiConn.sendMetrics (timersBeforeFail);

			WaitForMetricsToBeReceived ();

			Assert.AreEqual (timersBeforeFail.Count, this.receivedMessages.Count);

			//Simulate a short-duration connectivity problem, e.g.
			// * network hiccup
			// * restart of WeblogNG api
			StopServer ();
			Thread.Sleep (500);
			StartServer ();

			LinkedList<Timer> timersAfterFail = MakeTestTimers ("after-failure", 10);
			apiConn.sendMetrics (timersAfterFail);

			WaitForMetricsToBeReceived ();

			Assert.AreEqual (timersAfterFail.Count, this.receivedMessages.Count);

		}


	}

}

