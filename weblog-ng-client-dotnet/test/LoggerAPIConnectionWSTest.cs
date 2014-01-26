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
		private WebSocketServer webSocketServer;
		private AutoResetEvent socketOpenedEvent = new AutoResetEvent(false);
		private AutoResetEvent socketClosedEvent = new AutoResetEvent(false);

		[TestFixtureSetUp]
		public virtual void Setup()
		{
			webSocketServer = new WebSocketServer(new BasicSubProtocol("Basic", new List<Assembly> { this.GetType().Assembly }));
			webSocketServer.NewDataReceived += new SessionHandler<WebSocketSession, byte[]>(m_WebSocketServer_NewDataReceived);
			webSocketServer.Setup(new ServerConfig
				{
					Port = 4242,
					Ip = "Any",
					MaxConnectionNumber = 10,
					Mode = SocketMode.Tcp,
					Name = "WeblogNG Integration Testing Server",
					LogAllSocketException = true,
				}, logFactory: new ConsoleLogFactory());
		}

		void m_WebSocketServer_NewDataReceived(WebSocketSession session, byte[] e)
		{
			session.Send(new ArraySegment<byte>(e, 0, e.Length));
		}

		[SetUp]
		public void StartServer()
		{
			webSocketServer.Start();
		}

		[TearDown]
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
		public void should_create_and_open_a_properly_configured_websocket(){
			String expectedKey = "key";
			String expectedHost = "localhost:4242";

			LoggerAPIConnectionWS apiConn = new LoggerAPIConnectionWS (expectedHost, expectedKey);

			Assert.IsNull (apiConn.WebSocket);

			WebSocket client = LoggerAPIConnectionWS.CreateWebSocket (apiConn);

			client.Opened += new EventHandler(webSocketClient_Opened);
			client.Closed += new EventHandler(webSocketClient_Closed);

			if (!socketOpenedEvent.WaitOne(1000))
				Assert.Fail("Failed to Opened session ontime");

			Assert.AreEqual (WebSocketState.Open, client.State);

			client.Close();

			if (!socketClosedEvent.WaitOne(1000))
				Assert.Fail("Failed to close session ontime");

			Assert.AreEqual(WebSocketState.Closed, client.State);
		}
	}

}

