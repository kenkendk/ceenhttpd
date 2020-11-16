using System;
using System.Linq;
using System.Threading.Tasks;
using Ceen;
using Ceen.Mvc;
using NUnit.Framework;

namespace Unittests
{
    [Name("reporttarget")]
    public class ReportExample : Controller
    {
        [HttpPost]
        public async Task<IResult> Index(IHttpContext context)
        {
            var sb = new System.Text.StringBuilder();

            foreach(var file in context.Request.Files)
                sb.Append(await file.Data.ReadAllAsStringAsync());

            if (context.Request.Files.Count == 0)
                sb.Append("No content...");

            return Text(sb.ToString());
        }
    }

    [TestFixture()]
    public class MultipartTests
    {
        [Test()]
        public void TestSingleMultipart()
        {
            using (var server = new ServerRunner(
                new Ceen.Httpd.ServerConfig()
                {
                    AutoParseMultipartFormData = true,
                }
                .AddLogger((context, exception, started, duration) => Task.Run(() => Console.WriteLine("Error: {0}", exception)))
                .AddRoute(
                    new[] { typeof(ReportExample) }
                    .ToRoute(
                        new ControllerRouterConfig()
                        { Debug = true }
                    ))
                ))
            {

                var payload = " ... some text data ...";
                string result = null;
                try
                {
                    var boundary = "--test-boundary-1234";

                    var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{server.Port}/reporttarget");
                    req.Method = "POST";
                    req.ContentType = "multipart/form-data; boundary=" + boundary;

                    using (var p = req.GetRequestStream())
                    {
                        var data = System.Text.Encoding.UTF8.GetBytes(
                            string.Join("\r\n",
                                $"--{boundary}",
                                "Content-Disposition: form-data; name=\"file\"; filename=\"test.txt\"",
                                "Content-Type: text/plain",
                                "",
                                payload,
                                $"--{boundary}--",
                                ""
                            )
                        );
                        p.Write(data, 0, data.Length);
                    }

                    using (var res = (System.Net.HttpWebResponse)req.GetResponse())
                    using (var sr = new System.IO.StreamReader(res.GetResponseStream()))
                        result = sr.ReadToEnd();
                }
                catch (System.Net.WebException wex)
                {
                    if (wex.Response is System.Net.HttpWebResponse)
                        result = ((System.Net.HttpWebResponse)wex.Response).StatusDescription;
                    else
                        throw;
                }

                Assert.AreEqual(payload, result);
            }
        }

        [Test()]
        public void TestDualMultipart()
        {
            using (var server = new ServerRunner(
                new Ceen.Httpd.ServerConfig()
                {
                    AutoParseMultipartFormData = true,
                }
                .AddLogger((context, exception, started, duration) => Task.Run(() => Console.WriteLine("Error: {0}", exception)))
                .AddRoute(
                    new[] { typeof(ReportExample) }
                    .ToRoute(
                        new ControllerRouterConfig()
                        { Debug = true }
                    ))
                ))
            {

                var payload1 = " ... some text data ...";
                var payload2 = " ... some other text data ...";
                string result = null;
                try
                {
                    var boundary = "--test-boundary-1234";

                    var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{server.Port}/reporttarget");
                    req.Method = "POST";
                    req.ContentType = "multipart/form-data; boundary=" + boundary;

                    using (var p = req.GetRequestStream())
                    {
                        var data = System.Text.Encoding.UTF8.GetBytes(
                            string.Join("\r\n",
                                $"--{boundary}",
                                "Content-Disposition: form-data; name=\"file1\"; filename=\"test1.txt\"",
                                "Content-Type: text/plain",
                                "",
                                payload1,
                                $"--{boundary}",
                                "Content-Disposition: form-data; name=\"file2\"; filename=\"test2.txt\"",
                                "Content-Type: text/plain",
                                "",
                                payload2,
                                $"--{boundary}--",
                                ""
                            )
                        );
                        p.Write(data, 0, data.Length);
                    }

                    using (var res = (System.Net.HttpWebResponse)req.GetResponse())
                    using (var sr = new System.IO.StreamReader(res.GetResponseStream()))
                        result = sr.ReadToEnd();
                }
                catch (System.Net.WebException wex)
                {
                    if (wex.Response is System.Net.HttpWebResponse)
                        result = ((System.Net.HttpWebResponse)wex.Response).StatusDescription;
                    else
                        throw;
                }

                Assert.AreEqual(payload1 + payload2, result);
            }
        }

        [Test]
        public void TestBadTrailingMultipart()
        {
            using (var server = new ServerRunner(
                new Ceen.Httpd.ServerConfig()
                {
                    AutoParseMultipartFormData = true,
                }
                .AddLogger((context, exception, started, duration) => Task.Run(() => Console.WriteLine("Error: {0}", exception)))
                .AddRoute(
                    new[] { typeof(ReportExample) }
                    .ToRoute(
                        new ControllerRouterConfig()
                        { Debug = true }
                    ))
                ))
            {

                var payload = " ... some text data ...";
                string result = null;
                try
                {
                    var boundary = "--test-boundary-1234";

                    var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{server.Port}/reporttarget");
                    req.Method = "POST";
                    req.ContentType = "multipart/form-data; boundary=" + boundary;

                    using (var p = req.GetRequestStream())
                    {
                        var data = System.Text.Encoding.UTF8.GetBytes(
                            string.Join("\r\n",
                                $"--{boundary}",
                                "Content-Disposition: form-data; name=\"file\"; filename=\"test.txt\"",
                                "Content-Type: text/plain",
                                "",
                                payload,
                                $"--{boundary}--",
                                "",
                                "abc" // This should not be here...
                            )
                        );
                        p.Write(data, 0, data.Length);
                    }

                    using (var res = (System.Net.HttpWebResponse)req.GetResponse())
                    using (var sr = new System.IO.StreamReader(res.GetResponseStream()))
                        result = sr.ReadToEnd();
                }
                catch (System.Net.WebException wex)
                {
                    if (wex.Response is System.Net.HttpWebResponse)
                        result = ((System.Net.HttpWebResponse)wex.Response).StatusDescription;
                    else
                        throw;
                }

                Assert.AreEqual("Bad Request", result);
            }
        }
    }
}