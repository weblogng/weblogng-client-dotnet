using System;
using System.Collections.Generic;
using NUnit.Framework;
using weblog;

namespace weblog
{
	class MockFinishedMetricsFlusher : BaseFinishedMetricsFlusher {

		public int FlushCount { get; set; }

		override public void Flush (Object stateInfo)
		{
			DrainFinishedTimersForFlush ();
			FlushCount++;
			Console.WriteLine ("Flushed with " + stateInfo.ToString() + " flush count: " + FlushCount);
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
		public void RecordStart_should_return_new_running_timer_for_metric_name() 
		{
			String expectedName = "some_metric";
			Logger logger = MakeLogger();
			Timer timer = logger.RecordStart(expectedName);

			Assert.IsNotNull(timer);
			Assert.IsTrue (timer.IsRunning());
			Assert.AreEqual (expectedName, timer.MetricName);
		}

		[Test()]
		public void RecordStart_should_retain_the_created_timer(){
			Logger logger = MakeLogger();
			String expectedName = "some_metric";

			Assert.IsFalse(logger.HasTimer (expectedName));

			logger.RecordStart(expectedName);

			Assert.IsTrue(logger.HasTimer (expectedName));
		}

		[Test()]
		public void RecordFinish_should_transfer_timer_from_inProgress_to_finished()
		{
			Logger logger = MakeLogger ();
			String expectedName = "some_metric";

			Timer startedTimer = logger.RecordStart (expectedName);
			Assert.IsTrue (startedTimer.IsRunning());

			Timer finishedTimer = logger.RecordFinish (expectedName);

			Assert.AreSame (startedTimer, finishedTimer);
			Assert.IsFalse (finishedTimer.IsRunning());
			Assert.Contains(finishedTimer, logger.GetFinishedTimers());
		}

		[Test()]
		public void RecordFinishAndSendMetric_should_flush_timers()
		{
			MockFinishedMetricsFlusher flusher = new MockFinishedMetricsFlusher ();
			Logger logger = new Logger (flusher);
			String expectedName = "some_metric";

			Assert.AreEqual (0, flusher.FlushCount);
			Assert.AreEqual(0, logger.FinishedMetricsFlusher.GetFinishedTimers ().Count);

			Timer startedTimer = logger.RecordStart (expectedName);
			Timer finishedTimer = logger.RecordFinishAndSendMetric (expectedName);

			Assert.AreEqual (startedTimer, finishedTimer);
			Assert.AreEqual (1, flusher.FlushCount);
			Assert.AreEqual(0, logger.FinishedMetricsFlusher.GetFinishedTimers ().Count);
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
	
}

