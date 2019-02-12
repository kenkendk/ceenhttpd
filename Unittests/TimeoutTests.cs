using System;
using System.Threading.Tasks;
using Ceen;
using Ceen.Mvc;
using NUnit.Framework;

namespace Unittests
{
    [Name("delaytarget")]
    public class DelayExample : Controller
    {
        public async Task<IResult> Index(IHttpRequest req, int waitseconds, bool resettimer)
        {
            var rounds = waitseconds * 10;

            for (var i = 0; i < rounds; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                // Play nice and respect the processing timeout
                req.ThrowIfTimeout();

                // Reset the timeout, if instructed to
                if (resettimer && i % 5 == 0)
                    req.ResetProcessingTimeout();
            }

            return Status(HttpStatusCode.OK);
        }
    }


    [TestFixture]
    public class TimeoutTests
    {

        [Test]
        public void TestResetProcessingTimeout()
        {
            using (var server = new ServerRunner(
                new Ceen.Httpd.ServerConfig()
                {
                    MaxProcessingTimeSeconds = 1
                }
                .AddLogger((context, exception, started, duration) => Task.Run(() => Console.WriteLine("Error: {0}", exception)))
                .AddRoute(
                    new[] { typeof(DelayExample) }
                    .ToRoute(
                        new ControllerRouterConfig()
                        { Debug = true }
                    ))
                ))
            {
                Assert.AreEqual(HttpStatusCode.OK, server.GetStatusCode("/delaytarget?waitseconds=2&resettimer=true"));
                Assert.AreEqual(HttpStatusCode.RequestTimeout, server.GetStatusCode("/delaytarget?waitseconds=2&resettimer=false"));
            }
        }

    }
}
