using System;
using weblog;

namespace weblogclienttester
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Logger logger = new weblog.Logger("ec2-174-129-123-237.compute-1.amazonaws.com:9000", "93c5a127-e2a4-42cc-9cc6-cf17fdac8a7f");
		
			Console.WriteLine ("Press Enter key to stop...");
			Console.ReadLine();
		}
	}
}
