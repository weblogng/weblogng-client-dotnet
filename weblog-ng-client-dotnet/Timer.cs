using System;
using System.Diagnostics;

namespace weblog
{
	public class Timer : IDisposable
	{
		public string MetricName { get; set; }
		
		private Stopwatch Watch;
		private Logger Logger;
		
		public Timer (String metricName, Logger logger)
		{
			MetricName = metricName;
			Logger = logger;
			Watch = new Stopwatch ();
			Watch.Start ();
			
		}
		
		public bool IsRunning ()
		{
			return Watch.IsRunning;
		}
		
		public void Dispose ()
		{
			Watch.Stop();
			Logger.addToFinishedTimers(this);
		}

	}
	
		
}

