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
	public class Utilities
	{
		public static Logger Logger = Logger.CreateAsyncLogger ("93c5a127-e2a4-42cc-9cc6-cf17fdac8a7f");
	}

	public class Application
	{
		public void StartUp()
		{
			using (Utilities.Logger.CreateTimer ("Application-StartUp")) {
				//perform the acutal start up operations, e.g.:
				//1. read configuration
				//2. initialize connection pools
				//3. pre-heat caches
				//4. etc

				System.Threading.Thread.Sleep (1000);
			}
		}

		public static void Main (string[] args){
			Application app = new Application ();
			app.StartUp ();

			System.Threading.Thread.Sleep (15000);
		}
	}
}

```