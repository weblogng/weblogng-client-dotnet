using System;
using System.Diagnostics;

namespace WeblogNG
{
	public class Timer : IDisposable
	{
		public string MetricName { get; set; }
		
		private Stopwatch Watch;
		private Logger logger;
		
		public Timer (String metricName, Logger logger)
		{
			MetricName = metricName;
			this.logger = logger;
			Watch = new Stopwatch ();
			Watch.Start ();
			
		}
		
		public bool IsRunning ()
		{
			return Watch.IsRunning;
		}

		public void Stop()
		{
			Watch.Stop ();
		}

		public void Dispose ()
		{
			Watch.Stop();
			Logger.AddToFinishedTimers(this);
		}

		public long TimeElapsedMilliseconds {
			get { return Watch.ElapsedMilliseconds; }
		}

		public Logger Logger {
			get { return this.logger; }
		}

	}
	
		
}

