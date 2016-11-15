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

	internal class ServerRunner : IDisposable
	{
		public Ceen.Httpd.ServerConfig Config;

		public System.Threading.CancellationTokenSource StopToken;
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
	public class Test
	{
		[Test()]
		public void TestRoutingWithConflictingVerbs()
		{
			Assert.Throws<Exception>(() =>
			{
				using (var server = new ServerRunner(
					new Ceen.Httpd.ServerConfig()
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
		public void TestRoutingWithDefaultHiddenController()
		{
			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
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
			}

		}		
		[Test()]
		public void TestRoutingWithDefaultController()
		{
			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
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
			}
			      
		}

		[Test()]
		public void TestRoutingWithoutDefaultController()
		{
			using (var server = new ServerRunner(
				new Ceen.Httpd.ServerConfig()
				.AddRoute(
					typeof(ControllerItems)
					.GetNestedTypes()
					.ToRoute())
				))
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
			}

		}
	}
}

