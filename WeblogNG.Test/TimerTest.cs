using NUnit.Framework;
using System;

namespace WeblogNG.Test
{
	[TestFixture ()]
	public class TimerTest
	{

		Logger logger;

		Logger MakeLogger(){
			return new Logger (new MockFinishedMetricsFlusher());
		}


		[SetUp ()]
		public void setUp(){
			logger = MakeLogger ();
		}


		[Test ()]
		public void should_be_configured_via_constructor_params ()
		{
			String expectedName = "my name is metric";

			Timer timer = new Timer (expectedName, logger);

			Assert.AreEqual (expectedName, timer.MetricName);
			Assert.AreSame (logger, timer.Logger);
		}
	}
}

