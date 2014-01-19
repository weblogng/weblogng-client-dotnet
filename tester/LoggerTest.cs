using System;
using NUnit.Framework;
using weblog;

namespace weblogclienttester
{
	[TestFixture()]
	public class LoggerTest
	{
		
		Logger logger;
		
		[Test()]
		public void should_throw_exception_if_empty_uri ()
		{
			try {
				Logger logger = new Logger("", "");
				Assert.Fail();
			}
			catch(System.ArgumentException ex) {
			
			}
		}
		
		[Test()]
		public void it_should_sanitize_invalid_names() 
		{
			string[] chars = {@".", @"!", @"," , @";", @":", @"?", @"/", @"\", @"@", @"#", @"$", @"%", @"^", @"&", @"*", @"(", @")"};
			   
			foreach(String forbiddenChar in chars) {
				String actualMetricName = Logger.sanitizeMetricName(String.Format ("metric-name_1{0}2", forbiddenChar));
				Assert.AreEqual( @"metric-name_1_2", actualMetricName);
			}
		}
		
		
		[Test()]
		public void createTimer_should_return_new_running_timer() 
		{
			Logger logger = new Logger("http://some_host", "fake_api_key");
			Timer timer = logger.CreateTimer("some_metric");
			Assert.IsNotNull(timer);
			Assert.IsTrue (timer.IsRunning());
		}
		
		[Test()] 
		public void timer_should_have_metric_name() {
			Logger logger = new Logger("http://some_host", "fake_api_key");
			
			Timer timer = logger.CreateTimer("operation_to_measure_timing");
			
			Assert.AreEqual("operation_to_measure_timing", timer.MetricName);
		}
		
		
		[Test()]
		public void timer_should_not_run_after_dispose() 
		{
			Logger logger = new Logger("http://some_host", "fake_api_key");
			Timer timer = new Timer("metric_name", logger);
			Assert.IsTrue(timer.IsRunning());
			
			timer.Dispose();
			
			Assert.IsFalse(timer.IsRunning());
		}
		
		
		[Test()]
		public void logger_should_accumulate_finished_timers() 
		{
			Logger logger = new Logger("http://some_host", "fake_api_key");
			
			Timer timer = logger.CreateTimer("operation_to_measure_timing");
			
			timer.Dispose();
			
			Assert.Contains(timer, logger.getFinishedTimers());
		}
		
	}
}


/*
	using(logger.CreateTimer("operation_a_execution_time")){ //new Timer(this);
                               System.sleep (100); //ms
                                                      }
*/                       