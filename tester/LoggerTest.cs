using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace weblog
{
	class MockFinishedMetricsFlusher : BaseFinishedMetricsFlusher {

		override public void Flush (Object stateInfo){
			Console.WriteLine ("Flushed with " + stateInfo.ToString());
		}

	}

	[TestFixture()]
	public class LoggerTest
	{

		Logger MakeLogger(){
			return new Logger (new MockFinishedMetricsFlusher());
		}


		[Test()]
		public void should_be_configured_via_constructor_params ()
		{
			FinishedMetricsFlusher expectedFlusher = new MockFinishedMetricsFlusher ();

			Logger logger = new Logger (expectedFlusher);

			Assert.AreSame (expectedFlusher, logger.FinishedMetricsFlusher);
		}

		[Test()]
		public void should_throw_exception_if_flusher_not_provided ()
		{
			try {
				new Logger (null);
				Assert.Fail("expected an exception due null flusher");
			}
			catch(System.ArgumentException ex) {
				Assert.IsNotNull (ex);
			}
		}
			

		[Test()]
		public void it_should_sanitize_invalid_names() 
		{
			string[] chars = {@".", @"!", @"," , @";", @":", @"?", @"/", @"\", @"@", @"#", @"$", @"%", @"^", @"&", @"*", @"(", @")"};
			   
			foreach(String forbiddenChar in chars) {
				String actualMetricName = LoggerAPIConnectionWS.sanitizeMetricName(String.Format ("metric-name_1{0}2", forbiddenChar));
				Assert.AreEqual( @"metric-name_1_2", actualMetricName);
			}
		}
		
		
		[Test()]
		public void createTimer_should_return_new_running_timer() 
		{
			Logger logger = MakeLogger();
			Timer timer = logger.CreateTimer("some_metric");
			Assert.IsNotNull(timer);
			Assert.IsTrue (timer.IsRunning());
		}
		
		[Test()] 
		public void createTimer_should_return_timer_with_provided_metric_name()
		{
			Logger logger = MakeLogger();
			
			Timer timer = logger.CreateTimer("operation_to_measure_timing");
			
			Assert.AreEqual("operation_to_measure_timing", timer.MetricName);
		}
		
		
		[Test()]
		public void timer_should_not_run_after_dispose() 
		{
			Logger logger = MakeLogger();
			Timer timer = new Timer("metric_name", logger);
			Assert.IsTrue(timer.IsRunning());
			
			timer.Dispose();
			
			Assert.IsFalse(timer.IsRunning());
		}
		
		
		[Test()]
		public void logger_should_accumulate_finished_timers() 
		{
			Logger logger = MakeLogger();
			
			Timer timer = logger.CreateTimer("operation_to_measure_timing");
			
			timer.Dispose();
			
			Assert.Contains(timer, logger.GetFinishedTimers());
		}
		
		
		[Test()]
		public void using_should_time_block_of_code() 
		{
			Logger logger = MakeLogger();
			using(logger.CreateTimer("operation_a_execution_time"))
			{ 
                               
				System.Threading.Thread.Sleep (100); //ms
                               
            }
			
			Assert.AreEqual(1, logger.GetFinishedTimers().Count);
			
			Timer t = logger.GetFinishedTimers().First.Value;
			
			Assert.GreaterOrEqual(t.TimeElapsedMilliseconds, 75L);
		}

	}

	[TestFixture()]
	public class AsyncFinishedMetricsFlusherTest {

		class MockLoggerAPIConnection : LoggerAPIConnection {
			public void sendMetrics(ICollection<Timer> timers){
				Console.WriteLine ("Sending metrics with " + timers.ToString());
			}
		}

		private AsyncFinishedMetricsFlusher MakeFlusher (){
			return new AsyncFinishedMetricsFlusher (new MockLoggerAPIConnection ());
		}

		[Test()]
		public void should_be_configured_via_constructor_params(){
			LoggerAPIConnection apiConnection = new MockLoggerAPIConnection ();
			AsyncFinishedMetricsFlusher flusher = new AsyncFinishedMetricsFlusher (apiConnection);

			Assert.AreSame (apiConnection, flusher.LoggerAPIConnection);
		}


		[Test()]
		public void draining_finished_timers_should_result_in_new_empty_collection(){

			AsyncFinishedMetricsFlusher flusher = MakeFlusher();

			ICollection<Timer> origFinishedTimers = flusher.GetFinishedTimers ();

			Assert.AreEqual (0, origFinishedTimers.Count);

			Timer timer = new Timer ("an_operation", new Logger(flusher));
			flusher.AddToFinishedTimers (timer);

			Assert.AreSame (origFinishedTimers, flusher.GetFinishedTimers ());
			Assert.Contains (timer, flusher.GetFinishedTimers ());
			Assert.AreEqual (1, origFinishedTimers.Count);

			ICollection<Timer> drainedTimers = flusher.DrainFinishedTimersForFlush ();
			Assert.AreSame (origFinishedTimers, drainedTimers);

			Assert.AreNotSame (drainedTimers, flusher.GetFinishedTimers());
			Assert.AreEqual (0, flusher.GetFinishedTimers().Count);
		}

		[Test()]
		public void flush_should_drain_finished_timers(){
			AsyncFinishedMetricsFlusher flusher = MakeFlusher();

			ICollection<Timer> origFinishedTimers = flusher.GetFinishedTimers ();

			Assert.AreEqual (0, origFinishedTimers.Count);

			Timer timer = new Timer ("an_operation", new Logger(flusher));
			flusher.AddToFinishedTimers (timer);

			Assert.Contains (timer, flusher.GetFinishedTimers ());

			flusher.Flush (new object());

			Assert.AreEqual (0, flusher.GetFinishedTimers().Count);

		}

	}

	[TestFixture()]
	public class LoggerAPIConnectionWSTest {

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

