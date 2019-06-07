using System;
using System.Threading.Tasks;
using Ceen;
using Ceen.Httpd;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Unittests
{
	public class TimeOfDayHandler : IHttpModule
	{
		public TimeOfDayHandler(int a, string b, bool c)
		{
		}

		#region IHttpModule implementation
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			context.Response.SetNonCacheable();
			await context.Response.WriteAllJsonAsync(JsonConvert.SerializeObject(new { time = DateTime.Now.TimeOfDay }));
			return true;
		}
		#endregion

		public bool ExtendedLogging { get; set; }
		public int MaxFileSize { get; set; }
	}

	public class TestLogger : ILogger
	{
		public Task LogRequest(IHttpContext context, Exception ex, DateTime started, TimeSpan duration)
		{
			return Task.FromResult(true);
		}

        public Task LogRequestCompletedAsync(IHttpContext context, Exception ex, DateTime started, TimeSpan duration)
        {
            return Task.FromResult(true);   
        }

        public bool TestProp { get; set; }
	}



	[TestFixture()]
	public class CliTests
	{
		[Test()]
		public void TestParseConfig()
		{
			var filepath = System.IO.Path.Combine(
				System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
				, "test1.txt");
			var c1 = Ceen.Httpd.Cli.ConfigParser.ParseTextFile(filepath);

			Assert.AreEqual(c1.CertificatePath, "xyz");
			Assert.AreEqual(c1.CertificatePassword, "pass");
			Assert.AreEqual(c1.HttpPort, 22);
			Assert.AreEqual(c1.HttpsPort, 333);
			Assert.AreEqual(c1.HttpAddress, "0.0.0.0");
			Assert.AreEqual(c1.HttpsAddress, "any");
		}

		[Test()]
		public void TestConfigSetup()
		{
			var filepath = System.IO.Path.Combine(
				System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
				, "test2.txt");
			var c1 = Ceen.Httpd.Cli.ConfigParser.ParseTextFile(filepath);
			var c2 = Ceen.Httpd.Cli.ConfigParser.ValidateConfig(c1);

			Assert.AreEqual(c2.MaxActiveRequests, 999);
			Assert.AreEqual(c2.AllowHttpMethodOverride, true);

			Assert.AreEqual(c2.Loggers.Count, 4);
		}
	}
}
