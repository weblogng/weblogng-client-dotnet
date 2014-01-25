using NUnit.Framework;
using System;
using weblog;

namespace weblog.test
{

	[TestFixture()]
	public class LoggerAPIConnectionWSTest
	{

		[Test()]
		public void should_be_configured_via_constructor_params(){

			String expectedKey = "key";
			String expectedHost = "somehost:42";
			String expectedUrl = "ws://" + expectedHost + "/log/ws";
			LoggerAPIConnectionWS apiConn = new LoggerAPIConnectionWS (expectedHost, expectedKey);

			Assert.AreEqual (expectedKey, apiConn.ApiKey);
			Assert.AreEqual (expectedUrl, apiConn.ApiUrl);
		}
	}

}

