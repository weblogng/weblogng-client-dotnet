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

