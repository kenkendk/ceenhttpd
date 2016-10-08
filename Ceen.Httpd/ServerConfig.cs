using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Globalization;
using Ceen.Common;

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
		public ILogger Logger;
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
	}
}

