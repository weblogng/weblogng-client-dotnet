using System;
using System.Security.Permissions;

namespace WeblogNG.Test
{
	class InteractiveTester
	{
		private static Random random = new Random();

		[SecurityPermission(SecurityAction.Demand, Flags=SecurityPermissionFlag.ControlAppDomain)]
		public static void Main (string[] args)
		{
			AppDomain currentDomain = AppDomain.CurrentDomain;
			currentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionUtilities.UnhandledExceptionHandler);

			Logger logger = Logger.CreateAsyncLogger ("93c5a127-e2a4-42cc-9cc6-cf17fdac8a7f");

			using(logger.CreateTimer("csharp-tester-startup")){
				System.Threading.Thread.Sleep (200); //ms
			}

			Console.WriteLine ("Using logger {0}", logger.ToString ());


			String command = "";
			do
			{
				command = promptForCommand ().Trim ().ToLower ();
				if("auto".Equals(command)){
					while(true){
						using(logger.CreateTimer("csharp-tester-auto")){
							System.Threading.Thread.Sleep (1000 + random.Next(-250, 250));
						}
						Console.WriteLine("completed csharp-tester-auto loop {0}", DateTime.Now);
					}
				}
			} while("exit".Equals (command));

		}

		public static String promptForCommand(){
			Console.WriteLine ("Type one of the following options and press enter:");
			Console.WriteLine ("  auto");
			Console.WriteLine ("  exit");
			Console.WriteLine ("Press Enter key to stop...");
			return Console.ReadLine ();
		}

	}
}
