# weblog-ng-client-dotnet #
Welcome to the WeblogNG client library for .NET.

This repository contains a solution with two sub-projects:

* the WeblogNG client library

* Tests and demonstration code for the WeblogNG client library

The current release of the library is version 0.1.0.

## Features ##
The WeblogNG client library provides a simple way to instrument .NET application code in order
to understand how long important operations take. The most convenient and reliable way to 
instrument code is to create a WeblogNG.Timer in a using block.  Timer is implemented such that:

* when created, the Timer's internal Stopwatch will start automatically

* when disposed-of, the Timer's internal Stopwatch will:
    - stop automatically
    - be handed back to the WeblogNG logging infrastructure for flushing to the remote WeblogNG api

For example, to instrument a 'StartUp' method you could do the following:

```csharp
using System;

namespace WeblogNG.Demo
{

	public class Application
	{
		public static void Main (string[] args)
		{
			//generate and use your own api key via the 'User Account' page at:
			//http://weblog-ng-ui.herokuapp.com/app/#/account
			Logger.CreateSharedLogger ("93c5a127-e2a4-42cc-9cc6-cf17fdac8a7f");

			Application app = new Application ();
			app.StartUp ();

			System.Threading.Thread.Sleep (15000);
		}

		public void StartUp()
		{
			using (Logger.SharedLogger.CreateTimer ("Application-StartUp"))
			{
				//perform the acutal start up operations, e.g.:
				//1. read configuration
				//2. initialize connection pools
				//3. pre-heat caches
				//4. etc

				System.Threading.Thread.Sleep (1000);
			}
		}
	}
}

```

The WeblogNG Logger takes a number of steps to impose a minimal impact on the instrumented application:

* when a Timer finishes and is handed back to the Logger, it is queued for flushing to the api in a non-blocking way

* periodically, a timing data is flushed to the WeblogNG api servers for aggregation
	- the current flush period is 10 seconds
	- a separate thread is used for flushing tasks, not the application's calling threads
	- the task factory used to flush timers is limited to a single thread LimitedConcurrencyLevelTaskScheduler in order
	to bound thread resource usage in the using application

* the WeblogNG library takes care-of:
    - all concurrency matters when Timers are created and finished in a `using` statement
	- (re-)connecting to the WeblogNG api, as necessary, to flush timing data

Note: Currently, if the WeblogNG api is unreachable when a flush task executes, the timing data will be discarded.  This
behavior may change in the future (or become configurable), but the intent is to prevent any WeblogNG api availability
issues from causing a memory 'leak' in applications using the library.
