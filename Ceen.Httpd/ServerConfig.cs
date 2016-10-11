using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Globalization;

namespace Ceen.Httpd
{
	/// <summary>
	/// Configuration of a server instance
	/// </summary>
	public class ServerConfig
	{
		/// <summary>
		/// The socket backlog.
		/// </summary>
		public int SocketBacklog = 5;
		/// <summary>
		/// The maximum size of the request line.
		/// </summary>
		public int MaxRequestLineSize = 8 * 1024;
		/// <summary>
		/// The maximum size of the request header.
		/// </summary>
		public int MaxRequestHeaderSize = 64 * 1024;
		/// <summary>
		/// The maximum number of active requests.
		/// </summary>
		public int MaxActiveRequests = 500000;

		/// <summary>
		/// The maximum size of a POST request with url encoded data.
		/// This is also the maximum size allowed for automatically
		/// decoding multipart form data.
		/// </summary>
		public int MaxUrlEncodedFormSize = 5 * 1024 * 1025;

		/// <summary>
		/// Allow automatic parsing of multipart form data
		/// </summary>
		public bool AutoParseMultipartFormData = true;

		/// <summary>
		/// The maximum size of a POST request
		/// </summary>
		public long MaxPostSize = 100 * 1024 * 1024;

		/// <summary>
		/// The request idle timeout in seconds.
		/// </summary>
		public int RequestIdleTimeoutSeconds = 5;
		/// <summary>
		/// The request header read timeout in seconds.
		/// </summary>
		public int RequestHeaderReadTimeoutSeconds = 10;
		/// <summary>
		/// The maximum number of requests to server with a single connection.
		/// </summary>
		public int KeepAliveMaxRequests = 30;
		/// <summary>
		/// The keep-alive timeout in seconds
		/// </summary>
		public int KeepAliveTimeoutSeconds = 10;
		/// <summary>
		/// The router instance to use for handling requests
		/// </summary>
		public IRouter Router;
		/// <summary>
		/// The logger instance to use
		/// </summary>
		public IList<ILogger> Loggers;
		/// <summary>
		/// A callback method for injecting headers into the responses
		/// </summary>
		public Action<IHttpResponse> AddDefaultResponseHeaders = DefaultHeaders;

		/// <summary>
		/// The server certificate if used for serving SSL requests
		/// </summary>
		public X509Certificate SSLCertificate;
		/// <summary>
		/// True if a client SSL certificate should be requested
		/// </summary>
		public bool SSLRequireClientCert = false;
		/// <summary>
		/// List the allowed SSL versions
		/// </summary>
		public SslProtocols SSLEnabledProtocols = SslProtocols.Tls12;
		/// <summary>
		/// Value indicating if SSL certificates are checked against a revocation list
		/// </summary>
		public bool SSLCheckCertificateRevocation = true;

		/// <summary>
		/// A callback handler for debugging the internal server state
		/// </summary>
		public DebugLogDelegate DebugLogHandler;

		/// <summary>
		/// Adds default headers to the output.
		/// </summary>
		/// <param name="response">The response to update.</param>
		private static void DefaultHeaders(IHttpResponse response)
		{
			response.AddHeader("Date", DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture));
			response.AddHeader("Server", "ceenhttpd/0.1");
		}

		/// <summary>
		/// Adds a logger instance to the server
		/// </summary>
		/// <returns>The server configuration.</returns>
		/// <param name="logger">The logger module to add.</param>
		public ServerConfig AddLogger(LogDelegate logger)
		{
			return AddLogger(new Logging.FunctionLogger(logger));
		}

		/// <summary>
		/// Adds a logger instance to the server
		/// </summary>
		/// <returns>The server configuration.</returns>
		/// <param name="logger">The logger module to add.</param>
		public ServerConfig AddLogger(ILogger logger)
		{
			if (logger == null)
				throw new ArgumentNullException(nameof(logger));
			if (Loggers == null)
				Loggers = new List<ILogger>();

			Loggers.Add(logger);
			return this;
		}

		/// <summary>
		/// Adds a route to this configuration
		/// </summary>
		/// <returns>The server configuration.</returns>
		/// <param name="handler">The handler function that will execute the operation.</param>
		public ServerConfig AddRoute(HttpHandlerDelegate handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));
			return AddRoute(null, new Handler.FunctionHandler(handler));
		}

		/// <summary>
		/// Adds a route to this configuration
		/// </summary>
		/// <returns>The server configuration.</returns>
		/// <param name="handler">The handler function that will execute the operation.</param>
		public ServerConfig AddRoute(IHttpModule handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));
			return AddRoute(null, handler);
		}

		/// <summary>
		/// Adds a route to this configuration
		/// </summary>
		/// <returns>The server configuration.</returns>
		/// <param name="route">The expression used to pre-filter requests before invoking the handler.</param>
		/// <param name="handler">The handler function that will execute the operation.</param>
		public ServerConfig AddRoute(string route, HttpHandlerDelegate handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));
			return AddRoute(route, new Handler.FunctionHandler(handler));
		}

		/// <summary>
		/// Adds a route to this configuration
		/// </summary>
		/// <returns>The server configuration.</returns>
		/// <param name="route">The expression used to pre-filter requests before invoking the handler.</param>
		/// <param name="handler">The handler module that will execute the operation.</param>
		public ServerConfig AddRoute(string route, IHttpModule handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			Router rt;
			if (this.Router == null)
				this.Router = rt = new Router();
			else if (this.Router is Router)
				rt = this.Router as Router;
			else
				throw new Exception($"Cannot use the AddRoute method unless the {nameof(Router)} is an instance of {typeof(Router).FullName}");

			rt.Add(route, handler);
			return this;		
		}
	}
}

