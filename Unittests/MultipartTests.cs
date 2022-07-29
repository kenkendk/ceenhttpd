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

            foreach(var formdata in context.Request.Form.OrderBy(x => x.Key))
                sb.Append(formdata.Value);
            
            if (context.Request.Files.Count == 0 && context.Request.Form.Count == 0)
                sb.Append("No content...");

            return Text(sb.ToString());
        }
    }

    [TestFixture()]
    public class MultipartTests : IDisposable
    {
        private readonly ServerRunner m_server;

        public MultipartTests()
        {
            m_server = new ServerRunner(
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
                );
        }
        public void Dispose() 
        {
            m_server.Dispose();
        }

        [Test()]
        public void TestSingleMultipart()
        {
            TestSingleMultipart(" ... some text data ...", "test.txt");
        }

        [Test()]
        public void TestSingleMultipartFormdata()
        {
            TestSingleMultipart(" ... some text data ...", null);
        }

        [Test()]
        public void TestLargeSingleMultipart()
        {
            TestSingleMultipart(string.Join("\r\n", Enumerable.Range(0, 200).Select(x => " ... some text data ...")), "test.txt");
        }

        [Test()]
        public void TestAroundBufferLimits()
        {
            // Test that all variations of buffer edges are hit
            for(var i = 900; i <= 1032; i++) 
                TestSingleMultipart(new string(' ', i), null);
        }

        private void TestSingleMultipart(string payload, string filename)
        {
            string result = null;
            try
            {
                var boundary = "--test-boundary-1234";

                var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{m_server.Port}/reporttarget");
                req.Method = "POST";
                req.ContentType = "multipart/form-data; boundary=" + boundary;

                using (var p = req.GetRequestStream())
                {
                    var data = System.Text.Encoding.UTF8.GetBytes(
                        string.Join("\r\n",
                            $"--{boundary}",
                            string.IsNullOrWhiteSpace(filename)
                            ? $"Content-Disposition: form-data; name=\"file\""
                            : $"Content-Disposition: form-data; name=\"file\"; filename=\"{filename}\"",
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

        [Test()]
        public void TestDualMultipart()
        {
            var payload1 = " ... some text data ...";
            var payload2 = " ... some other text data ...";
            string result = null;
            try
            {
                var boundary = "--test-boundary-1234";

                var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{m_server.Port}/reporttarget");
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

        [Test]
        public void TestBadTrailingMultipart()
        {
            var payload = " ... some text data ...";
            string result = null;
            try
            {
                var boundary = "--test-boundary-1234";

                var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{m_server.Port}/reporttarget");
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