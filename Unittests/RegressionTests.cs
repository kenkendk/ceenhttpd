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
using Ceen.Mvc;

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

    public class QueryStringReader : Controller
    {
        [HttpGet]
        [HttpPost]
        public IResult Index(string key)
        {
            return Json(new { 
                time = DateTime.Now.TimeOfDay,
                parsekey = key,
                urlkey = Context.Request.QueryString["key"],
                urlk_e_y = Context.Request.QueryString["k e y"],
                formkey = Context.Request.Form["key"],
                formk_e_y = Context.Request.Form["k e y"]
            });
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

        // https://github.com/kenkendk/ceenhttpd/issues/25
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

        // https://github.com/kenkendk/ceenhttpd/issues/26
        // The issue was handling + as space in urls
        [Test()]
        public void TestIssue26()
        {
            using (var server = new ServerRunner(
                new Ceen.Httpd.ServerConfig() {
                    // 10MiB headers for testing
                    MaxRequestHeaderSize = 1024 * 1024 * 10
                }
                .AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
                .AddRoute(
                    new Type[] { typeof(QueryStringReader) }
                    .ToRoute(
                        new ControllerRouterConfig()
                        { Debug = true }
                    )
                )
            ))
            {
                var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{server.Port}/{nameof(QueryStringReader)}?key=1+2+3&k+e+y=1+1+1");
                req.Method = "GET";

                using (var res = (HttpWebResponse)req.GetResponse())
                {
                    if (res.StatusCode != System.Net.HttpStatusCode.OK)
                        throw new Exception($"Bad status code: {res.StatusCode}");
                    
                    string result;
                    using (var sr = new System.IO.StreamReader(res.GetResponseStream()))
                        result = sr.ReadToEnd();

                    var v = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
                    if (!v.TryGetValue("parsekey", out var k) || !string.Equals(k, "1 2 3"))
                        throw new Exception($"Failed to parse key as input: {res.StatusCode}");
                    if (!v.TryGetValue("urlkey", out k) || !string.Equals(k, "1 2 3"))
                        throw new Exception($"Failed to parse key in url: {res.StatusCode}");
                    if (!v.TryGetValue("urlk_e_y", out k) || !string.Equals(k, "1 1 1"))
                        throw new Exception($"Failed to parse k+e+y in url: {res.StatusCode}");
                }

                req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{server.Port}/{nameof(QueryStringReader)}");
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                var data = Encoding.UTF8.GetBytes("key=1+2+3&k+e+y=1+1+1");
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
                    if (!v.TryGetValue("parsekey", out var k) || !string.Equals(k, "1 2 3"))
                        throw new Exception($"Failed to parse key as form input: {res.StatusCode}");
                    if (!v.TryGetValue("formkey", out k) || !string.Equals(k, "1 2 3"))
                        throw new Exception($"Failed to parse key in form: {res.StatusCode}");
                    if (!v.TryGetValue("formk_e_y", out k) || !string.Equals(k, "1 1 1"))
                        throw new Exception($"Failed to parse k+e+y in form: {res.StatusCode}");
                }

            }
        }                                       
    }
}