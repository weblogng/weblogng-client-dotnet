using System;
using weblog;

namespace weblogclienttester
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Logger logger = Logger.CreateAsyncLogger ("93c5a127-e2a4-42cc-9cc6-cf17fdac8a7f");

			Console.WriteLine ("Using logger {0}", logger.ToString ());
			Console.WriteLine ("Press Enter key to stop...");
			Console.ReadLine();
		}
	}
}
