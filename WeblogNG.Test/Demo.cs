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

