using System.Collections.Generic;
using System.Text;
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

    public class FormUnpackHandler : IHttpModule
    {
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			context.Response.SetNonCacheable();
			await context.Response.WriteAllJsonAsync(JsonConvert.SerializeObject(new { time = DateTime.Now.TimeOfDay, key = context.Request.Form["key"] }));
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
                new Ceen.Httpd.ServerConfig() {
                    // 10MiB headers for testing
                    MaxRequestHeaderSize = 1024 * 1024 * 10
                }
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

        // https://github.com/kenkendk/ceenhttpd/issue/25
        // The issue was parsing content-type for form-urlencoded
        [Test()]
        public void TestIssue25()
        {
            using (var server = new ServerRunner(
                new Ceen.Httpd.ServerConfig() {
                    // 10MiB headers for testing
                    MaxRequestHeaderSize = 1024 * 1024 * 10
                }
                .AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
                .AddRoute(new FormUnpackHandler()))
            )
            {
                var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{server.Port}/reporttarget");
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                var data = Encoding.UTF8.GetBytes("key=1234");
                using(var rq = req.GetRequestStream())
                    rq.Write(data, 0, data.Length);

                using (var res = (HttpWebResponse)req.GetResponse())
                {
                    if (res.StatusCode != System.Net.HttpStatusCode.OK)
                        throw new Exception($"Bad status code: {res.StatusCode}");
                    
                    string result;
                    using (var sr = new System.IO.StreamReader(res.GetResponseStream()))
                        result = sr.ReadToEnd();

                    var v = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
                    if (!v.TryGetValue("key", out var k) || !string.Equals(k, "1234"))
                        throw new Exception($"Failed to auto-parse header: {res.StatusCode}");
                }

            }
        }                 
    }
}