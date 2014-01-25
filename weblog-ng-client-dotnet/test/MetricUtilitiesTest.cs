using NUnit.Framework;
using System;
using weblog;

namespace weblog.test
{
	[TestFixture ()]
	public class MetricUtilitiesTest
	{

		[Test()]
		public void it_should_sanitize_invalid_names() 
		{
			string[] chars = {@".", @"!", @"," , @";", @":", @"?", @"/", @"\", @"@", 
				@"#", @"$", @"%", @"^", @"&", @"*", @"(", @")"};

			foreach(String forbiddenChar in chars) {
				String actualMetricName = MetricUtilities.sanitizeMetricName
				                          (String.Format ("metric-name_1{0}2", forbiddenChar));
				Assert.AreEqual(@"metric-name_1_2", actualMetricName);
			}
		}

	}
}

