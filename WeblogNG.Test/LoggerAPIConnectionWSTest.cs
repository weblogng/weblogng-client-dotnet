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
using SuperWebSocket;
using WebSocket4Net;


namespace WeblogNG.Test
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
			RegisterUnhandledExceptionHandler ();

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

		//fails due to what appears to be an uncaught error in SuperSocket/WebSocket4Net
		/// <summary>
		/// Registers an unhandled exception handler that prints the exception.  This is necessary because SuperSocket/WebSocket4Net
		/// generates unhandled exceptions during the failure-handling tests.
		/// </summary>
		/// <example>
		///System.Net.Sockets.Socket.SetSocketOption (optionLevel=System.Net.Sockets.SocketOptionLevel.Socket, optionName=System.Net.Sockets.SocketOptionName.KeepAlive, optionValue=true) in /private/tmp/source/bockbuild-xamarin/profiles/mono-mac-xamarin/build-root/mono-3.2.5/mcs/class/System/System.Net.Sockets/Socket.cs:2091
		///SuperSocket.ClientEngine.TcpClientSession.ProcessConnect (socket={System.Net.Sockets.Socket}, state=(null), e={System.Net.Sockets.SocketAsyncEventArgs}) in 
		///SuperSocket.ClientEngine.ConnectAsyncExtension.SocketConnectCompleted (sender={System.Net.Sockets.Socket}, e={System.Net.Sockets.SocketAsyncEventArgs}) in 
		///System.Net.Sockets.SocketAsyncEventArgs.OnCompleted (e={System.Net.Sockets.SocketAsyncEventArgs}) in /private/tmp/source/bockbuild-xamarin/profiles/mono-mac-xamarin/build-root/mono-3.2.5/mcs/class/System/System.Net.Sockets/SocketAsyncEventArgs.cs:177
		///System.Net.Sockets.SocketAsyncEventArgs.ConnectCallback () in /private/tmp/source/bockbuild-xamarin/profiles/mono-mac-xamarin/build-root/mono-3.2.5/mcs/class/System/System.Net.Sockets/SocketAsyncEventArgs.cs:262
		///System.Net.Sockets.SocketAsyncEventArgs.DispatcherCB (ares={System.Net.Sockets.Socket.SocketAsyncResult}) in /private/tmp/source/bockbuild-xamarin/profiles/mono-mac-xamarin/build-root/mono-3.2.5/mcs/class/System/System.Net.Sockets/SocketAsyncEventArgs.cs:234
		/// </example>

		private void RegisterUnhandledExceptionHandler()
		{
			AppDomain currentDomain = AppDomain.CurrentDomain;
			currentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionUtilities.UnhandledExceptionHandler);
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

		[Test()]
		public void should_throw_CannotSendMetricsException_when_SendMetrics_fails_after_initial_connect()
		{
			LoggerAPIConnectionWS apiConn = MakeAPIConnToTestServer ();

			LinkedList<Timer> timersBeforeFail = MakeTestTimers ("before-failure", 3);
			apiConn.sendMetrics (timersBeforeFail);

			WaitForMetricsToBeReceived ();

			Assert.AreEqual (timersBeforeFail.Count, this.receivedMessages.Count);

			//Simulate a long-duration connectivity problem
			StopServer ();
			Thread.Sleep (100);

			LinkedList<Timer> timersDuringFail = MakeTestTimers ("during-failure", 7);

			try
			{
				Console.WriteLine("about to sendMetrics");
				apiConn.sendMetrics(timersDuringFail);

				Assert.Fail("expected sendMetrics to throw CannotSendMetricsException"); 
			} catch (CannotSendMetricsException csm_e)
			{
				Assert.AreEqual(string.Format("Could not send metrics to {0}", apiConn.ApiUrl), csm_e.Message);
				Assert.IsInstanceOfType(typeof(OpenConnectionTimeoutException), csm_e.InnerException);
			}

		}

		[Test()]
		public void should_throw_CannotSendMetricsException_when_metrics_cannot_be_sent_on_initial_connect()
		{
			LoggerAPIConnectionWS apiConn = MakeAPIConnToTestServer ();

			StopServer ();

			LinkedList<Timer> timersDuringFail = MakeTestTimers ("during-failure", 7);

			try
			{
				Console.WriteLine("about to sendMetrics");
				apiConn.sendMetrics(timersDuringFail);
				Assert.Fail("expected sendMetrics to throw CannotSendMetricsException"); 
			} catch (CannotSendMetricsException csm_e)
			{
				Assert.AreEqual(string.Format("Could not send metrics to {0}", apiConn.ApiUrl), csm_e.Message);
				Assert.IsInstanceOfType(typeof(OpenConnectionTimeoutException), csm_e.InnerException);
			}
		}


		[Test()]
		public void on_error_api_conn_should_discard_websocket_and_reconnect_for_subsequent_metrics()
		{
			LoggerAPIConnectionWS apiConn = MakeAPIConnToTestServer ();

			LinkedList<Timer> timersBeforeError = MakeTestTimers ("before-error", 3);
			apiConn.sendMetrics (timersBeforeError);
			WebSocket socketBeforeFailure = apiConn.WebSocket;

			WaitForMetricsToBeReceived ();

			apiConn.websocket_Error (new object (), new SuperSocket.ClientEngine.ErrorEventArgs (new Exception ("an error occurred")));

			Assert.IsNull (apiConn.WebSocket);

			LinkedList<Timer> timersAfterError = MakeTestTimers ("after-error", 10);
			apiConn.sendMetrics (timersAfterError);

			Assert.AreNotSame (socketBeforeFailure, apiConn.WebSocket);

			WaitForMetricsToBeReceived ();

			Assert.AreEqual (timersBeforeError.Count + timersAfterError.Count, this.receivedMessages.Count);

		}

		[Test()]
		public void on_close_api_conn_should_discard_websocket_and_reconnect_for_subsequent_metrics()
		{
			LoggerAPIConnectionWS apiConn = MakeAPIConnToTestServer ();

			LinkedList<Timer> timersBeforeClose = MakeTestTimers ("before-close", 3);
			apiConn.sendMetrics (timersBeforeClose);
			WebSocket socketBeforeFailure = apiConn.WebSocket;

			WaitForMetricsToBeReceived ();

			apiConn.websocket_Closed (new object (), new EventArgs());

			Assert.IsNull (apiConn.WebSocket);

			LinkedList<Timer> timersAfterClose = MakeTestTimers ("after-close", 2);
			apiConn.sendMetrics (timersAfterClose);

			Assert.AreNotSame (socketBeforeFailure, apiConn.WebSocket);

			WaitForMetricsToBeReceived ();

			Assert.AreEqual (timersBeforeClose.Count + timersAfterClose.Count, this.receivedMessages.Count);
		}
	}
}

