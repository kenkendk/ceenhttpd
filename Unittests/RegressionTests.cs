using System.Reflection.Metadata;
using System;
using System.Threading.Tasks;
using Ceen;
using Ceen.Httpd;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Net;

namespace Unittests
{
    public class RegressionDummyHandler : IHttpModule
    {
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			context.Response.SetNonCacheable();
			await context.Response.WriteAllJsonAsync(JsonConvert.SerializeObject(new { time = DateTime.Now.TimeOfDay }));
            return context.SetResponseOK();
		}
    }

	[TestFixture()]
    public class RegressionTests
    {
        // https://github.com/kenkendk/ceenhttpd/pull/23
        // The issue was overwriting of the read buffer
        // for large request headers
        [Test()]
        public void TestIssue23()
        {
            using (var server = new ServerRunner(
                new Ceen.Httpd.ServerConfig() { }
                .AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
                .AddRoute(new RegressionDummyHandler()))
            )
            {
                var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{server.Port}/reporttarget");
                req.Method = "GET";

                // Add a bunch of nonsense headers
                // to artificially inflate the header size
                for(var i = 0; i < 1000; i++)
                    req.Headers.Add($"X-{i}", new string((char)('A' + (i % ('Z' - 'A'))), 100));

                using (var res = (HttpWebResponse)req.GetResponse())
                {
                    if (res.StatusCode != System.Net.HttpStatusCode.OK)
                        throw new Exception($"Bad status code: {res.StatusCode}");
                    
                    string result;
                    using (var sr = new System.IO.StreamReader(res.GetResponseStream()))
                        result = sr.ReadToEnd();
                }

            }
        }        
    }
}