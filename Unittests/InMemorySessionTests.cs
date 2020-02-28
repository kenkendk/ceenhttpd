using System.IO;
using System.Net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ceen;
using Ceen.Extras.InMemorySession;
using NUnit.Framework;

namespace Unittests
{
    /// <summary>
    /// Simple handler to test the create and expire methods of sessions
    /// </summary>
    public class CustomSessionHandler : InMemorySessionHandler
    {
        public int _createCount = 0;
        public int _expireCount = 0;
        public int _liveSessions = 0;

        protected override Task OnCreateAsync(IHttpRequest request, string sessionID, Dictionary<string, object> values)
        {
            // Create handler that pre-populates the session
            values["init"] = "1234";
            System.Threading.Interlocked.Increment(ref _createCount);
            System.Threading.Interlocked.Increment(ref _liveSessions);
            Console.WriteLine("{0} - Created new session: {1}", DateTime.Now.TimeOfDay, sessionID);
            return Task.FromResult(true);
        }

        protected override Task OnExpireAsync(string id, Dictionary<string, object> values)
        {
            // Example of expiration handler
            System.Threading.Interlocked.Increment(ref _expireCount);
            System.Threading.Interlocked.Decrement(ref _liveSessions);
            Console.WriteLine("{0} - Destroyed session: {1}", DateTime.Now.TimeOfDay, id);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Simple handler to interface with the in-memory session
    /// </summary>
    public class SessionQueryHandler : IHttpModule
    {
        /// <summary>
        /// Helper method to grab the session for the current request
        /// </summary>
        private Dictionary<string, object> Session 
        { 
            get => Context.Current.CurrentSession(); 
        }

        public Task<bool> HandleAsync(IHttpContext context)
        {
            //Can also get local variable, if prefered
            //var session = context.CurrentSession();

            var query = context.Request.QueryString;

            // Update session on POST/PUT
            if (context.Request.Method == "POST" || context.Request.Method == "PUT")
                Session[query["key"]] = query["value"];

            // Grab and report current value of "key"
            Session.TryGetValue(query["key"], out var item);
            context.Response.StatusCode = Ceen.HttpStatusCode.OK;
            context.Response.WriteAllAsync(item?.ToString() ?? "null", "text/plain");

            return Task.FromResult(true);
        }
    }

    [TestFixture]
    public class InMemorySessionTests
    {
        [Test]
        public void TestInMemorySessions()
        {
            var sessionhandler = 
                new CustomSessionHandler() {
                    ExpirationSeconds = TimeSpan.FromSeconds(10)
                };

            using (var server = new ServerRunner(
                new Ceen.Httpd.ServerConfig()
                .AddLogger((context, exception, started, duration) => Task.Run(() => Console.WriteLine("Error: {0}", exception)))
                .AddRoute(sessionhandler)
                .AddRoute("/store", new SessionQueryHandler())
            ))
            {
                Console.WriteLine("{0} - Starting...", DateTime.Now.TimeOfDay);

                Assert.AreEqual(0, sessionhandler._createCount);
                Assert.AreEqual(0, sessionhandler._liveSessions);
                Assert.AreEqual(0, sessionhandler._expireCount);

                var jar1 = new CookieContainer();
                var jar2 = new CookieContainer();
                Assert.AreEqual("b", GetResponse(server, "/store?key=a&value=b", jar1, "POST"));
                Assert.AreEqual(1, sessionhandler._createCount);
                Assert.AreEqual(1, sessionhandler._liveSessions);
                Assert.AreEqual(0, sessionhandler._expireCount);

                Assert.AreEqual("d", GetResponse(server, "/store?key=c&value=d", jar1, "PUT"));
                Assert.AreEqual("b", GetResponse(server, "/store?key=a", jar1, "GET"));
                Assert.AreEqual("1234", GetResponse(server, "/store?key=init", jar1, "GET"));
                Assert.AreEqual("5678", GetResponse(server, "/store?key=init&value=5678", jar1, "PUT"));
                Assert.AreEqual("5678", GetResponse(server, "/store?key=init", jar1, "GET"));

                // Test with other cookie jar
                Assert.AreEqual("null", GetResponse(server, "/store?key=a", jar2, "GET"));
                Assert.AreEqual("1234", GetResponse(server, "/store?key=init", jar2, "GET"));
                Assert.AreEqual(2, sessionhandler._createCount);
                Assert.AreEqual(2, sessionhandler._liveSessions);
                Assert.AreEqual(0, sessionhandler._expireCount);

                Console.WriteLine("{0} - Waiting 8 seconds", DateTime.Now.TimeOfDay);
                Task.Delay(TimeSpan.FromSeconds(7)).Wait();
                Console.WriteLine("{0} - Refreshing jar1", DateTime.Now.TimeOfDay);
                Assert.AreEqual("d", GetResponse(server, "/store?key=c", jar1, "GET"));

                Assert.AreEqual(2, sessionhandler._createCount);
                Assert.AreEqual(2, sessionhandler._liveSessions);
                Assert.AreEqual(0, sessionhandler._expireCount);
                
                Task.Delay(TimeSpan.FromSeconds(8)).Wait();
                Console.WriteLine("{0} - jar2 should be expired", DateTime.Now.TimeOfDay);
                Assert.AreEqual(2, sessionhandler._createCount);
                Assert.AreEqual(1, sessionhandler._liveSessions);
                Assert.AreEqual(1, sessionhandler._expireCount);

                // Test with other cookie jar, this will create a new session
                Assert.AreEqual("null", GetResponse(server, "/store?key=a", jar2, "GET"));
                Assert.AreEqual(3, sessionhandler._createCount);
                Assert.AreEqual(2, sessionhandler._liveSessions);
                Assert.AreEqual(1, sessionhandler._expireCount);

                Console.WriteLine("{0} - Waiting 8 seconds, jar1 should expire", DateTime.Now.TimeOfDay);

                Task.Delay(TimeSpan.FromSeconds(8)).Wait();
                Assert.AreEqual("null", GetResponse(server, "/store?key=a", jar1, "GET"));
                Assert.AreEqual(4, sessionhandler._createCount);
                Assert.AreEqual(2, sessionhandler._liveSessions);
                Assert.AreEqual(2, sessionhandler._expireCount);

                // Test with other cookie jar
                Assert.AreEqual("null", GetResponse(server, "/store?key=a", jar2, "GET"));
                Assert.AreEqual(4, sessionhandler._createCount);
                Assert.AreEqual(2, sessionhandler._liveSessions);
                Assert.AreEqual(2, sessionhandler._expireCount);
            }
        }

        private string GetResponse(ServerRunner server, string path, CookieContainer cookieJar, string verb = "GET")
        {
            try
            {
                var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{server.Port}{path}");
                req.Method = verb;
                req.CookieContainer = cookieJar;
                using (var res = (System.Net.HttpWebResponse)req.GetResponse())
                {
                    using(var rs = res.GetResponseStream())
                    using(var tr = new StreamReader(rs))
                        return tr.ReadToEnd();
                }
            }
            catch (System.Net.WebException wex)
            {
                throw;
            }
        }                
    }
}