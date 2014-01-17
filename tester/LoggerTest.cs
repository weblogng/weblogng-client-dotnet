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
	}
}

