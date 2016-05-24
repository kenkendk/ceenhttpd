using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ceenhttpd;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Ceenhttpd.Logging;

namespace Ceenhttpdcli
{
	class MainClass
	{
		private class TestHandler : IHttpModule
		{
			#region IHttpModule implementation
			public async Task<bool> HandleAsync(HttpRequest request, HttpResponse response)
			{
				response.ContentType = "application/json";

				using (var streamwriter = new StreamWriter(response.GetResponseStream()))
					new Newtonsoft.Json.JsonSerializer().Serialize(streamwriter, new { Xyz = 1 });

				return true;
			}
			#endregion
		}

		public static int Main(string[] args)
		{
			// Create a certificate with OpenSSL:
			// > openssl req -x509 -sha256 -nodes -days 365 -newkey rsa:2048 -keyout privkey.key -out certificate.crt

			// Convert it to pcks12
			// > openssl pkcs12 -export -in certificate.crt -inkey privkey.key -out certificate.pfx

			X509Certificate cert = null;

			if (File.Exists("certificate.pfx"))
				cert = new X509Certificate2("certificate.pfx", "");

			if (args == null || args.Length == 0)
				args = new string[] { Path.GetFullPath(".") };

			if (args.Length != 1)
			{
				Console.WriteLine("Usage: Ceenhttpd-cli [path-to-webroot]");
				return 1;
			}

			if (!Directory.Exists(args[0]))
			{
				Console.WriteLine("Directory not found: {0}", args[0]);
				return 1;
			}

			var server = new HttpServer(new ServerConfig() {
				Router = new Router(
					new Tuple<string, IHttpModule>[] {
						new Tuple<string, IHttpModule>(
							"/data",
							new TestHandler()
						),
						new Tuple<string, IHttpModule>(
							"/",
							new FileHandler(args[0])
						)

					}
				),

				Logger = new LogSplitter(new ILogger[] {
					new CLFLogger(Console.OpenStandardOutput()),
					new SyslogLogger(),
					new FunctionLogger((req,resp,ex,start,duration) => {
						if (ex != null)
							Console.WriteLine(ex);
						return Task.FromResult(true);
					})
				}),

				SSLCertificate = cert
			});

			var tcs = new System.Threading.CancellationTokenSource();
			var task = 
				Task.WhenAll(
					server.ListenAsync(new IPEndPoint(IPAddress.Loopback, 8900), tcs.Token),
					cert == null 
						? Task.FromResult(true) // No certificate, do not host SSL
						: server.ListenSSLAsync(new IPEndPoint(IPAddress.Loopback, 8901), stoptoken: tcs.Token) //Host using the given cert
				);

			Console.WriteLine("Server is running...");

			task.Wait();
			tcs.Cancel();

			Console.WriteLine("Server has stopped...");
			task.Wait();

			return 0;
		}
	}
}
