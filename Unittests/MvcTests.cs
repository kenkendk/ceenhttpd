using NUnit.Framework;
using System;
using Ceen;
using Ceen.Mvc;
using System.Threading.Tasks;

namespace Unittests
{
	[Name("api")]
	public interface IAPI : IControllerPrefix { }

	[Name("v1")]
	public interface IApiV1 : IAPI { }

	public class ControllerItems
	{
		public const string ENTRY_DEFAULT_INDEX = "GET /api/v1/entry";
		public const string ENTRY_UPDATE = "POST /api/v1/entry/update";
		public const string ENTRY_INDEX_ID = "GET /api/v1/entry/id";
		public const string ENTRY_DETAIL_INDEX = "GET /api/v1/entry/detail";
		public const string ENTRY_DETAIL_CROSS = "GET /api/v1/entry/cross/id";
		public const string ENTRY_DETAIL_ID = "GET /api/v1/entry/detail/id";
		public const string WAIT_INDEX = "GET /api/v1/wait";
		public const string XYZ_HOME_INDEX = "XYZ /";

		[Name("entry")]
		public class ApiExampleController : Controller, IApiV1
		{

			[HttpGet]
			public IResult Index(IHttpContext context)
			{
				return Status(HttpStatusCode.OK, ENTRY_DEFAULT_INDEX);
			}

			[HttpPost]
			public IResult Update(int id)
			{
				return Status(HttpStatusCode.OK, ENTRY_UPDATE);
			}

			[HttpGet]
			[HttpDelete]
			[Route("{id}/vss-{*blurp}")]
			[Route("{id}")]
			public IResult Index(IHttpContext context, [Parameter(Source = ParameterSource.Url)]int id, string blurp = "my-blurp")
			{
				return Status(HttpStatusCode.OK, ENTRY_INDEX_ID);
			}

			[Route("detail/{id}")]
			public IResult Detail(IHttpContext context, int id)
			{
				return Status(HttpStatusCode.OK, ENTRY_DETAIL_ID);
			}

			[Route("{id}/detail")]
			public IResult Cross(IHttpContext context, int id)
			{
				return Status(HttpStatusCode.OK, ENTRY_DETAIL_CROSS);
			}

			public IResult Detail(IHttpContext context)
			{
				return Status(HttpStatusCode.OK, ENTRY_DETAIL_INDEX);
			}

		}

		[Name("wait")]
		public class WaitExample : Controller, IApiV1
		{
			[HttpGet]
			public Task<IResult> Index()
			{
				return Task.FromResult(Status(HttpStatusCode.OK, WAIT_INDEX));
			}
		}

		//[Name("home")]
		public class HomeController : Controller
		{
			public IResult Index()
			{
				return Status(HttpStatusCode.OK, XYZ_HOME_INDEX);
			}
		}
	}

	public class ConflictControllerItems1
	{
		public const string WAIT_INDEX = "GET /api/v1/wait";

		[Name("wait")]
		public class WaitExample : Controller, IApiV1
		{
			[HttpGet]
			public Task<IResult> Index()
			{
				return Task.FromResult(Status(HttpStatusCode.OK, WAIT_INDEX));
			}

			[HttpGet]
			public Task<IResult> Index(int id)
			{
				return Task.FromResult(Status(HttpStatusCode.OK, WAIT_INDEX));
			}
		}
	}

	public class ConflictControllerItems2
	{
		public const string WAIT_INDEX = "GET /api/v1/wait";

		[Name("wait")]
		public class WaitExample : Controller, IApiV1
		{
			public Task<IResult> Index()
			{
				return Task.FromResult(Status(HttpStatusCode.OK, WAIT_INDEX));
			}

			public Task<IResult> Index(int id)
			{
				return Task.FromResult(Status(HttpStatusCode.OK, WAIT_INDEX));
			}
		}
	}

	public class RequirementControllerItems
	{
		public const string WAIT_INDEX_GET = "GET /api/v1/wait";
		public const string WAIT_INDEX_DETAIL_GET = "GET /api/v1/wait/detail";
		public const string WAIT_INDEX_DETAIL_HEAD = "HEAD /api/v1/wait/detail";

		public class TestHandler : IHttpModule
		{
			public Task<bool> HandleAsync(IHttpContext context)
			{
				return Task.FromResult(false);
			}
		}

		[Name("wait")]
		public class WaitExample : Controller, IApiV1
		{
			[HttpGet]
			[RequireHandler(typeof(TestHandler))]
			public Task<IResult> Index()
			{
				return Task.FromResult(Status(HttpStatusCode.OK, WAIT_INDEX_GET));
			}

			[HttpHead]
			[RequireHandler(typeof(TestHandler))]
			[Name("detail")]
			public Task<IResult> DetailHead()
			{
				return Task.FromResult(Status(HttpStatusCode.OK, WAIT_INDEX_DETAIL_HEAD));
			}
		
			[Name("detail")]
			public Task<IResult> Detail()
			{
				return Task.FromResult(Status(HttpStatusCode.OK, WAIT_INDEX_DETAIL_GET));
			}
		}
	}

	internal class ServerRunner : IDisposable
	{
		public Ceen.Httpd.ServerConfig Config;

		public readonly System.Threading.CancellationTokenSource StopToken;
		public Task ServerTask;
		public readonly int Port;

		public ServerRunner(Ceen.Httpd.ServerConfig config, int port = 8900)
		{
			Config = config;
			StopToken = new System.Threading.CancellationTokenSource();
			ServerTask = Ceen.Httpd.HttpServer.ListenAsync(
				new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, port),
				false,
				config,
				StopToken.Token);
			Port = port;
		}

		public void Dispose()
		{
			StopToken.Cancel();
			ServerTask.Wait();
		}

		public HttpStatusCode GetStatusCode(string path, string verb = "GET")
		{
			try
			{
				var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{Port}{path}");
				req.Method = verb;
				using (var res = (System.Net.HttpWebResponse)req.GetResponse())
					return (HttpStatusCode)res.StatusCode;
			}
			catch (System.Net.WebException wex)
			{
				if (wex.Response is System.Net.HttpWebResponse)
					return (HttpStatusCode)((System.Net.HttpWebResponse)wex.Response).StatusCode;
				throw;
			}
		}

		public string GetStatusMessage(string path, string verb = "GET")
		{
			try
			{
				var req = System.Net.WebRequest.CreateHttp($"http://127.0.0.1:{Port}{path}");
				req.Method = verb;
				using (var res = (System.Net.HttpWebResponse)req.GetResponse())
					return res.StatusDescription;
			}
			catch (System.Net.WebException wex)
			{
				if (wex.Response is System.Net.HttpWebResponse)
					return ((System.Net.HttpWebResponse)wex.Response).StatusDescription;
				throw;
			}
		}

	}

	[TestFixture()]
	public class MvcTest
	{
		[Test()]
		public void TestRoutingWithConflictingVerbs()
		{
			Assert.Throws<Exception>(() =>
			{
				using (var server = new ServerRunner(
					new Ceen.Httpd.ServerConfig()
					.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
					.AddRoute(
						typeof(ConflictControllerItems1)
						.GetNestedTypes()
						.ToRoute(
							new ControllerRouterConfig()
							{ Debug = true }
						))
					))
				{ }
			});
		}

		[Test()]
		public void TestRoutingWithConflictingDefaultVerbs()
		{
			Assert.Throws<Exception>(() =>
			{
				using (var server = new ServerRunner(
					new Ceen.Httpd.ServerConfig()
					.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
					.AddRoute(
						typeof(ConflictControllerItems2)
						.GetNestedTypes()
						.ToRoute(
							new ControllerRouterConfig()
							{ Debug = true }
						))
					))
				{ }
			});
		}


		[Test()]
		public void TestRoutingWithRequirements()
		{
			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
				.AddRoute("/api/v1/wait", new RequirementControllerItems.TestHandler())
				.AddRoute(
					new[] { typeof(RequirementControllerItems.WaitExample) } 
					.ToRoute(
						new ControllerRouterConfig()
						{ Debug = true }
					))
				))
			{ 
				Assert.AreEqual(RequirementControllerItems.WAIT_INDEX_GET, server.GetStatusMessage("/api/v1/wait", "GET"));
				Assert.AreEqual(RequirementControllerItems.WAIT_INDEX_DETAIL_GET, server.GetStatusMessage("/api/v1/wait/detail", "GET"));
				Assert.AreEqual(HttpStatusCode.InternalServerError, server.GetStatusCode("/api/v1/wait/detail", "HEAD"));
			}

			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
				.AddRoute("/api/v1/wait/detail", new RequirementControllerItems.TestHandler())
				.AddRoute(
					new[] { typeof(RequirementControllerItems.WaitExample) }
					.ToRoute(
						new ControllerRouterConfig()
						{ Debug = true }
					))
				))
			{
				Assert.AreEqual(HttpStatusCode.InternalServerError, server.GetStatusCode("/api/v1/wait", "GET"));
				Assert.AreEqual(RequirementControllerItems.WAIT_INDEX_DETAIL_GET, server.GetStatusMessage("/api/v1/wait/detail", "GET"));
				Assert.AreEqual(RequirementControllerItems.WAIT_INDEX_DETAIL_HEAD, server.GetStatusMessage("/api/v1/wait/detail", "HEAD"));
			}
		}

		[Test()]
		public void TestRoutingWithDefaultHiddenController()
		{
			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
				.AddRoute(
					typeof(ControllerItems)
					.GetNestedTypes()
					.ToRoute(
						new ControllerRouterConfig(
							typeof(ControllerItems.HomeController)) 
							{ HideDefaultController = true, Debug = true }
					))
				))
			{
				Assert.AreEqual(HttpStatusCode.OK, server.GetStatusCode("/"));
				Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/home"));
				Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/xyz"));
				Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/home1"));

				Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/", "GET"));
				Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/", "XYZ"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/home", "GET"));
				Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/", "XYZ"));

				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/home1", "XYZ"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/home", "XYZ"));
				Assert.AreEqual(ControllerItems.ENTRY_DEFAULT_INDEX, server.GetStatusMessage("/api/v1/entry"));
				Assert.AreEqual(ControllerItems.ENTRY_INDEX_ID, server.GetStatusMessage("/api/v1/entry/4"));
				Assert.AreEqual(HttpStatusCode.BadRequest, server.GetStatusCode("/api/v1/entry/x"));
				Assert.AreEqual(ControllerItems.ENTRY_DETAIL_INDEX, server.GetStatusMessage("/api/v1/entry/detail"));
				Assert.AreEqual(ControllerItems.ENTRY_DETAIL_ID, server.GetStatusMessage("/api/v1/entry/detail/7"));
				Assert.AreEqual(HttpStatusCode.BadRequest, server.GetStatusCode("/api/v1/entry/detail/y"));

				Assert.AreEqual(ControllerItems.ENTRY_DETAIL_CROSS, server.GetStatusMessage("/api/v1/entry/7/detail"));

				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1/"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1/home"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1/xyz"));

				Assert.AreEqual(ControllerItems.WAIT_INDEX, server.GetStatusMessage("/api/v1/wait", "GET"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.MethodNotAllowed), server.GetStatusMessage("/api/v1/wait", "POST"));
				
                Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/api/v1/4/detail"));
            }

		}		
		[Test()]
		public void TestRoutingWithDefaultController()
		{
			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
				.AddRoute(
					typeof(ControllerItems)
					.GetNestedTypes()
					.ToRoute(
						new ControllerRouterConfig(
							typeof(ControllerItems.HomeController)) 
							{ HideDefaultController = false, Debug = true }
					))
				))
			{
				Assert.AreEqual(HttpStatusCode.OK, server.GetStatusCode("/"));
				Assert.AreEqual(HttpStatusCode.OK, server.GetStatusCode("/home"));
				Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/xyz"));
				Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/home1"));

				Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/", "GET"));
				Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/", "XYZ"));
				Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/home", "GET"));
				Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/home", "XYZ"));

				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/home1", "XYZ"));
				Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/home", "XYZ"));
				Assert.AreEqual(ControllerItems.ENTRY_DEFAULT_INDEX, server.GetStatusMessage("/api/v1/entry"));
				Assert.AreEqual(ControllerItems.ENTRY_INDEX_ID, server.GetStatusMessage("/api/v1/entry/4"));
				Assert.AreEqual(HttpStatusCode.BadRequest, server.GetStatusCode("/api/v1/entry/x"));
				Assert.AreEqual(ControllerItems.ENTRY_DETAIL_INDEX, server.GetStatusMessage("/api/v1/entry/detail"));
				Assert.AreEqual(ControllerItems.ENTRY_DETAIL_ID, server.GetStatusMessage("/api/v1/entry/detail/7"));
				Assert.AreEqual(HttpStatusCode.BadRequest, server.GetStatusCode("/api/v1/entry/detail/y"));

				Assert.AreEqual(ControllerItems.ENTRY_DETAIL_CROSS, server.GetStatusMessage("/api/v1/entry/7/detail"));

				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1/"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1/home"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1/xyz"));

				Assert.AreEqual(ControllerItems.WAIT_INDEX, server.GetStatusMessage("/api/v1/wait", "GET"));
				Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.MethodNotAllowed), server.GetStatusMessage("/api/v1/wait", "POST"));

                Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/api/v1/4/detail"));
            }
			      
		}

		/// <summary>
		/// Helper method to ensure that the different ways of routing
		/// result in the same setup
		/// </summary>
		/// <param name="server">The server runner instance</param>
		private void CommonTesting(ServerRunner server)
		{
			Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/"));
			Assert.AreEqual(HttpStatusCode.OK, server.GetStatusCode("/home"));
			Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/xyz"));
			Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/home1"));

			Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/home", "GET"));
			Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/home", "XYZ"));

			Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/home1", "XYZ"));
			Assert.AreEqual(ControllerItems.XYZ_HOME_INDEX, server.GetStatusMessage("/home", "XYZ"));
			Assert.AreEqual(ControllerItems.ENTRY_DEFAULT_INDEX, server.GetStatusMessage("/api/v1/entry"));
			Assert.AreEqual(ControllerItems.ENTRY_INDEX_ID, server.GetStatusMessage("/api/v1/entry/4"));
			Assert.AreEqual(HttpStatusCode.BadRequest, server.GetStatusCode("/api/v1/entry/x"));
			Assert.AreEqual(ControllerItems.ENTRY_DETAIL_INDEX, server.GetStatusMessage("/api/v1/entry/detail"));
			Assert.AreEqual(ControllerItems.ENTRY_DETAIL_ID, server.GetStatusMessage("/api/v1/entry/detail/7"));
			Assert.AreEqual(HttpStatusCode.BadRequest, server.GetStatusCode("/api/v1/entry/detail/y"));

			Assert.AreEqual(ControllerItems.ENTRY_DETAIL_CROSS, server.GetStatusMessage("/api/v1/entry/7/detail"));

			Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1/"));
			Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1"));
			Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1/home"));
			Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.NotFound), server.GetStatusMessage("/api/v1/xyz"));

			Assert.AreEqual(ControllerItems.WAIT_INDEX, server.GetStatusMessage("/api/v1/wait", "GET"));
			Assert.AreEqual(HttpStatusMessages.DefaultMessage(HttpStatusCode.MethodNotAllowed), server.GetStatusMessage("/api/v1/wait", "POST"));

			Assert.AreEqual(HttpStatusCode.NotFound, server.GetStatusCode("/api/v1/4/detail"));
		}

		[Test()]
		public void TestRoutingWithoutDefaultController()
		{
			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
				.AddRoute(
					typeof(ControllerItems)
					.GetNestedTypes()
					.ToRoute())
				))
			{
				CommonTesting(server);
            }
		}

		[Test()]
		public void TestRoutingWithManualRoutes()
		{
			var inst = new ControllerItems.ApiExampleController();

			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
				.AddRoute(
					new ManualRoutingController()
						// We need to supply the function argument types, as C# does not support
						// automatic inference of these
						.Wire<IHttpContext>(             "GET /api/v1/entry", inst.Index)
						.Wire<IHttpContext, int, string>("GET /api/v1/entry/{id}", inst.Index)
						.Wire<IHttpContext, int>(        "GET /api/v1/entry/{id}/detail", inst.Cross)
						.Wire<int>(                      "POST /api/v1/entry/{id}", inst.Update)
						.Wire<IHttpContext>(             "GET /api/v1/entry/detail", inst.Detail)
						.Wire<IHttpContext, int>(        "GET|POST /api/v1/entry/detail/{id}", inst.Detail)
						// If the methods have no arguments, we do not need to supply types
						.Wire(                           "* /home", new ControllerItems.HomeController().Index)
						.Wire(                           "GET /api/v1/wait", new ControllerItems.WaitExample().Index)
						.ToRoute()
				)))
			{
				CommonTesting(server);
            }
		}

		[Test()]
		public void TestRoutingWithManualRoutesWithoutInstances()
		{
			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
				.AddRoute(
					new ManualRoutingController()
						// For types with overloaded methods, we need to specify which overload
						// The WireWith() helper saves us constantly referencing the type
						.WireWith<ControllerItems.ApiExampleController>()
							.Wire("GET /api/v1/entry", nameof(ControllerItems.ApiExampleController.Index), typeof(IHttpContext))
							.Wire("GET /api/v1/entry/{id}", nameof(ControllerItems.ApiExampleController.Index), typeof(IHttpContext), typeof(int), typeof(string))
							.Wire("GET /api/v1/entry/{id}/detail", nameof(ControllerItems.ApiExampleController.Cross))
							.Wire("POST /api/v1/entry/{id}", nameof(ControllerItems.ApiExampleController.Update))
							.Wire("GET /api/v1/entry/detail", nameof(ControllerItems.ApiExampleController.Detail), typeof(IHttpContext))
							.Wire("GET|POST /api/v1/entry/detail/{id}", nameof(ControllerItems.ApiExampleController.Detail), typeof(IHttpContext), typeof(int))
						.Wire<ControllerItems.HomeController>(      "* /home", nameof(ControllerItems.HomeController.Index))
						.Wire<ControllerItems.WaitExample>(         "GET /api/v1/wait", nameof(ControllerItems.WaitExample.Index))
						.ToRoute()
				)))
			{
				CommonTesting(server);
            }
		}

		[Test()]
		public void TestRoutingWithManualRoutesWithoutInstancesPrefixed()
		{
			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
				.AddRoute(
					new ManualRoutingController()
						// Wire the full controllers, but use a custom prefix
						.WireController<ControllerItems.ApiExampleController>("/api/v1/entry")
						.WireController(new ControllerItems.HomeController(), "/home")
						.WireController(typeof(ControllerItems.WaitExample), "/api/v1/wait")
						.ToRoute()
				)))
			{
				CommonTesting(server);
            }
		}

		[Test()]
		public void TestRoutingWithManualFromConfig1()
		{
			var inst = new ControllerItems.ApiExampleController();

			var filepath = System.IO.Path.Combine(
				System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
				, "mvc.test1.txt");

			var cfg = Ceen.Httpd.Cli.ConfigParser.ParseTextFile(filepath);

			using (var server = new ServerRunner(
				Ceen.Httpd.Cli.ConfigParser.ValidateConfig(cfg)
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
			))
			{
				CommonTesting(server);
            }
		}

		[Test()]
		public void TestRoutingWithManualFromConfig2()
		{
			var filepath = System.IO.Path.Combine(
				System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
				, "mvc.test2.txt");

			var cfg = Ceen.Httpd.Cli.ConfigParser.ParseTextFile(filepath);

			using (var server = new ServerRunner(
				Ceen.Httpd.Cli.ConfigParser.ValidateConfig(cfg)
				.AddLogger(new Ceen.Httpd.Logging.StdOutErrors())
			))
			{
				CommonTesting(server);
            }
		}													
	}
}

