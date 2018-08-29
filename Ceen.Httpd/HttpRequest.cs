using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.IO;
using System.Linq;
using System.Security.Authentication;

namespace Ceen.Httpd
{
	/// <summary>
	/// Representation of the values in a HTTP request
	/// </summary>
	internal class HttpRequest : IHttpRequest
	{
		/// <summary>
		/// Gets the HTTP Request line as sent by the client
		/// </summary>
		public string RawHttpRequestLine { get; private set; }
		/// <summary>
		/// The HTTP method or Verb
		/// </summary>
		/// <value>The method.</value>
		public string Method { get; private set; }
		/// <summary>
		/// The path of the query, not including the query string
		/// </summary>
		/// <value>The path.</value>
		public string Path { get; internal set; }
		/// <summary>
		/// The original path of the request, before internal path rewriting
		/// </summary>
		/// <value>The path.</value>
		public string OriginalPath { get; internal set; }
		/// <summary>
		/// The query string
		/// </summary>
		/// <value>The query string, including the leading question mark.</value>
		public string RawQueryString { get; private set; }
		/// <summary>
		/// Gets a parsed representation of the query string.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The parsed query string.</value>
		public IDictionary<string, string> QueryString { get; private set; }
		/// <summary>
		/// Gets the headers found in the request.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The headers.</value>
		public IDictionary<string, string> Headers { get; private set; }
		/// <summary>
		/// Gets the form data, if any.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The form values.</value>
		public IDictionary<string, string> Form { get; private set; }
		/// <summary>
		/// Gets the cookies supplied, if any.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The cookie values.</value>
		public IDictionary<string, string> Cookies { get; private set; }
		/// <summary>
		/// Gets the posted files, if any.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The files.</value>
		public IList<IMultipartItem> Files { get; private set; }
		/// <summary>
		/// Gets the headers found in the request.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The headers.</value>
		public IDictionary<string, object> RequestState { get; private set; }
		/// <summary>
		/// Gets the http version string.
		/// </summary>
		/// <value>The http version.</value>
		public string HttpVersion { get; private set; }
		/// <summary>
		/// Gets or sets a user identifier attached to the request.
		/// This can be set by handlers processing the request to simplify dealing with logged in users.
		/// Handlers should only set this is the user is authenticated.
		/// </summary>
		public string UserID { get; set; }
		/// <summary>
		/// Gets a value indicating what connection security is used.
		/// </summary>
		public SslProtocols SslProtocol { get; private set; }
		/// <summary>
		/// Gets the remote endpoint
		/// </summary>
		public System.Net.EndPoint RemoteEndPoint { get; private set; }
		/// <summary>
		/// Gets the client SSL certificate, if any
		/// </summary>
		public X509Certificate ClientCertificate { get; private set; }
		/// <summary>
		/// The taskid used for logging and tracing the request
		/// </summary>
		public string LogTaskID { get; private set; }
		/// <summary>
		/// The taskid used for logging and tracing the request
		/// </summary>
		public string LogRequestID { get; private set; }

		/// <summary>
		/// The stream representing the body of the request
		/// </summary>
		public Stream Body { get; private set; }

        /// <summary>
        /// The cancellation token source
        /// </summary>
        private CancellationTokenSource m_cancelRequest;

		/// <summary>
		/// Gets the request cancellation token that is triggered if the request times out
		/// </summary>
        public CancellationToken TimeoutCancellationToken { get { return m_cancelRequest.Token; } }

        /// <summary>
        /// The method to call to obtain the remote client connected stated
        /// </summary>
        private Func<bool> m_connectedMethod;

		/// <summary>
		/// Gets a value indicating whether this <see cref="T:Ceen.IHttpRequest"/> is connected.
		/// </summary>
		/// <value><c>true</c> if is connected; otherwise, <c>false</c>.</value>
        public bool IsConnected 
        { 
            get 
            {
                if (m_connectedMethod != null && !m_connectedMethod())
                    return false;

                return true;
            }
        }

        /// <summary>
		/// Gets the time the request processing started
		/// </summary>
        public DateTime RequestProcessingStarted { get; private set; }

		/// <summary>
		/// Gets the HTTP Content-Type header value
		/// </summary>
		/// <value>The type of the content.</value>
		public string ContentType
		{
			get
			{
				return Headers["Content-Type"];
			}
		}

		/// <summary>
		/// Gets the HTTP Content-Length header value
		/// </summary>
		/// <value>The length of the content.</value>
		public int ContentLength
		{
			get
			{
				int contentlength;
				if (!int.TryParse(Headers["Content-Length"], out contentlength))
					contentlength = -1;

				return contentlength;					
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceen.Httpd.HttpRequest"/> class.
		/// </summary>
		/// <param name="remoteEndpoint">The remote endpoint.</param>
		/// <param name="logtaskid">The logging ID for the task</param>
		/// <param name="clientCert">The client SSL certificate.</param>
		/// <param name="sslProtocol">The SSL protocol used</param>
        /// <param name="logrequestid">The ID of the request for logging purposes</param>
        /// <param name="connected">The method providing the remote client connected state</param>
        public HttpRequest(System.Net.EndPoint remoteEndpoint, string logtaskid, string logrequestid, X509Certificate clientCert, SslProtocols sslProtocol, Func<bool> connected)
		{
            m_cancelRequest = new CancellationTokenSource();
            m_connectedMethod = connected;
			RemoteEndPoint = remoteEndpoint;
			ClientCertificate = clientCert;
			SslProtocol = sslProtocol;
			LogTaskID = logtaskid;
			LogRequestID = logrequestid;
			Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);
			QueryString = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);
			Form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);
			Cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);
            Files = new List<IMultipartItem>();
			RequestState = new Dictionary<string, object>();
		}

		/// <summary>
		/// Handles a line from the input stream.
		/// </summary>
		/// <param name="line">The line being read.</param>
		private void HandleLine(string line)
		{
			// Check and parse the HTTP request line
			if (this.HttpVersion == null)
			{
				var components = line.Split(new char[] {' '}, 4);
				if (components.Length != 3)
					throw new HttpException(HttpStatusCode.BadRequest);

				if (components[2] != "HTTP/1.1" && components[2] != "HTTP/1.0")
					throw new HttpException(HttpStatusCode.HTTPVersionNotSupported);

				if (string.IsNullOrWhiteSpace(components[0]) || string.IsNullOrWhiteSpace(components[1]))
					throw new HttpException(HttpStatusCode.BadRequest);

				string qs = null;
				var path = components[1];
				var qix = path.IndexOf("?", StringComparison.Ordinal);
				if (qix >= 0)
				{
					qs = path.Substring(qix);
					path = path.Substring(0, qix);
				}

				this.RawHttpRequestLine = line;
				this.Method = components[0];
				this.OriginalPath = this.Path = path;
				this.RawQueryString = qs;
				this.HttpVersion = components[2];

				ParseQueryString(qs, this.QueryString);
			}
			else
			{
				var components = line.Split(new char[] {':'}, 2);
				if (components.Length != 2 || string.IsNullOrWhiteSpace(components[0]))
					throw new HttpException(HttpStatusCode.BadRequest);

				// Setup cookie collection automatically
				if (string.Equals(components[0].Trim(), "cookie", StringComparison.OrdinalIgnoreCase))
					foreach(var k in RequestUtility.SplitHeaderLine((components[1] ?? string.Empty).Trim()))
						Cookies[k.Key] = Uri.UnescapeDataString(k.Value);

				var key = components[0].Trim();
				var value = (components[1] ?? string.Empty).Trim();

				this.Headers[key] = value;
			}
		}

		/// <summary>
		/// Parses the query string.
		/// </summary>
		/// <param name="qs">The query string.</param>
		/// <param name="target">The dictionary target.</param>
		private static void ParseQueryString(string qs, IDictionary<string, string> target)
		{
			var fr = qs ?? string.Empty;
			if (fr.StartsWith("?", StringComparison.Ordinal))
				fr = fr.Substring(1);

			foreach (var frag in fr.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
			{
				var parts = frag.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
				target[Uri.UnescapeDataString(parts[0])] = parts.Length == 1 ? null : Uri.UnescapeDataString(parts[1]);
			}
		}

		private async Task ParseMultiPart(Func<IDictionary<string, string>, Stream, Task> itemparser, BufferedStreamReader reader, ServerConfig config, TimeSpan idletime, Task timeouttask, Task stoptask)
		{
			if ((this.ContentType ?? "").StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
			{
				if (this.ContentLength > config.MaxPostSize)
					throw new HttpException(HttpStatusCode.PayloadTooLarge);

				var trail = new byte[2];
				var parts = this.ContentType.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				var bndpart = parts.Where(x => x.Trim().StartsWith("boundary", StringComparison.OrdinalIgnoreCase)).FirstOrDefault() ?? string.Empty;
				var boundary = bndpart.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
				if (string.IsNullOrWhiteSpace(boundary))
					throw new HttpException(HttpStatusCode.BadRequest);

				var itemboundary = System.Text.Encoding.ASCII.GetBytes("--" + boundary);
				var tmp = await reader.RepeatReadAsync(itemboundary.Length, idletime, timeouttask, stoptask);
				if (!Enumerable.SequenceEqual(itemboundary, tmp))
					throw new HttpException(HttpStatusCode.BadRequest);

				await reader.RepeatReadAsync(trail, 0, 2, idletime, timeouttask, stoptask);

				if (trail[0] != '\r' || trail[1] != '\n')
					throw new HttpException(HttpStatusCode.BadRequest);

				do
				{
					var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);

					await reader.ReadHeaders(
						config.MaxRequestLineSize, 
						config.MaxRequestHeaderSize, 
						idletime,
						line =>
						{
							var components = line.Split(new char[] { ':' }, 2);
							if (components.Length != 2 || string.IsNullOrWhiteSpace(components[0]))
								throw new HttpException(HttpStatusCode.BadRequest);

							headers[components[0].Trim()] = (components[1] ?? string.Empty).Trim();
						},
						timeouttask,
						stoptask
					);

					await itemparser(headers, reader.GetDelimitedSubStream(itemboundary, idletime, timeouttask, stoptask));
					await reader.RepeatReadAsync(trail, 0, 2, idletime, timeouttask, stoptask);
				}
				while (trail[0] == '\r' && trail[1] == '\n');


				if (trail[0] != '-' || trail[1] != '-')
					throw new HttpException(HttpStatusCode.BadRequest);


				if (trail[0] != '\r' || trail[1] != '\n')
					throw new HttpException(HttpStatusCode.BadRequest);
			}
		}

		/// <summary>
		/// Parses url encoded form data
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="reader">The stream to read from.</param>
		/// <param name="config">The server configuration.</param>
		/// <param name="idletime">The maximum idle time.</param>
		/// <param name="timeouttask">A task that signals request timeout.</param>
		/// <param name="stoptask">A task that signals server stop.</param>
		internal async Task ParseFormData(BufferedStreamReader reader, ServerConfig config, TimeSpan idletime, Task timeouttask, Task stoptask)
		{
			var cs = new CancellationTokenSource();

			if (string.Equals("application/x-www-form-urlencoded", this.ContentType))
			{
				if (this.ContentLength != 0)
				{
					if (this.ContentLength > config.MaxUrlEncodedFormSize)
						throw new HttpException(HttpStatusCode.PayloadTooLarge);
					
					ParseQueryString(
						System.Text.Encoding.ASCII.GetString(
							this.ContentLength > 0
							? await reader.RepeatReadAsync(this.ContentLength, idletime, timeouttask, stoptask) 
							: await reader.ReadUntilCrlfAsync(config.MaxRequestLineSize, config.MaxUrlEncodedFormSize, idletime, timeouttask, stoptask)
						), this.Form);
				}

				this.Body = new LimitedBodyStream(reader, 0, idletime, timeouttask, stoptask);
			}
            else if (RequestUtility.IsMultipartRequest(this.ContentType) && this.ContentLength > 0 && this.ContentLength < config.MaxUrlEncodedFormSize && config.AutoParseMultipartFormData)
			{
				await ParseMultiPart(
					async (headers, stream) =>
					{
						var dispositionItems = RequestUtility.SplitHeaderLine(headers["Content-Disposition"]);
						if (!string.Equals(dispositionItems.FirstOrDefault().Key, "form-data", StringComparison.OrdinalIgnoreCase))
							throw new HttpException(HttpStatusCode.BadRequest);

						var name = RequestUtility.GetHeaderComponent(headers["Content-Disposition"], "name");
						if (string.IsNullOrWhiteSpace("name"))
							throw new HttpException(HttpStatusCode.BadRequest);
						
						var filename = RequestUtility.GetHeaderComponent(headers["Content-Disposition"], "filename");
						var charset = RequestUtility.GetHeaderComponent(headers["Content-Type"], "charset") ?? "ascii";

						if (string.IsNullOrWhiteSpace(filename))
						{
							using (var sr = new StreamReader(stream, RequestUtility.GetEncodingForCharset(charset)))
							{
								var rtask = sr.ReadToEndAsync();
								var rt = await Task.WhenAny(Task.Delay(idletime), timeouttask, stoptask, rtask);
								if (rt != rtask)
								{
									cs.Cancel();
									if (rt == stoptask)
										throw new TaskCanceledException();
									else
										throw new HttpException(HttpStatusCode.RequestTimeout);
								}

								this.Form[name] = await rtask;
							}
						}
						else
						{
							var me = new MultipartItem(headers) {
								Name = name,
								Filename = filename,
								Data = new MemoryStream()
							};

							var rtask = stream.CopyToAsync(me.Data, 8 * 1024, cs.Token);

							var rt = await Task.WhenAny(Task.Delay(idletime), timeouttask, stoptask, rtask);
							if (rt != rtask)
							{
								cs.Cancel();
								if (rt == stoptask)
									throw new TaskCanceledException();
								else
									throw new HttpException(HttpStatusCode.RequestTimeout);
							}

							await rtask;
							me.Data.Position = 0;

							this.Files.Add(me);
						}
					},
					reader,
					config,
					idletime,
					timeouttask,
					stoptask
				);

				this.Body = new LimitedBodyStream(reader, 0, idletime, timeouttask, stoptask);
			}
			else
			{
                this.Body = new LimitedBodyStream(reader, this.ContentLength, idletime, timeouttask, stoptask);
			}
		}

		/// <summary>
		/// Parses a HTTP header by reading the input stream
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="reader">The stream to read from.</param>
		/// <param name="config">The server configuration.</param>
		/// <param name="idletime">The maximum idle time.</param>
		/// <param name="timeouttask">A task that signals request timeout.</param>
		/// <param name="stoptask">A task that signals server stop.</param>
		internal async Task Parse(BufferedStreamReader reader, ServerConfig config, TimeSpan idletime, Task timeouttask, Task stoptask)
		{
			await reader.ReadHeaders(
				config.MaxRequestLineSize, 
				config.MaxRequestHeaderSize, 
				idletime,
				line => HandleLine(line),
				timeouttask,
				stoptask
			);

			if (this.ContentLength > config.MaxPostSize)
				throw new HttpException(HttpStatusCode.PayloadTooLarge);

			if (config.AllowHttpMethodOverride)
			{
				string newmethod;
				this.Headers.TryGetValue("X-HTTP-Method-Override", out newmethod);
				if (!string.IsNullOrWhiteSpace(newmethod))
					this.Method = newmethod;					
			}

			if (!string.IsNullOrWhiteSpace(config.AllowedSourceIPHeaderValue))
			{
				string realip;
				this.Headers.TryGetValue(config.AllowedSourceIPHeaderValue, out realip);
				if (!string.IsNullOrWhiteSpace(realip))
					this.RemoteEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(realip), ((System.Net.IPEndPoint)this.RemoteEndPoint).Port);
			}

			await ParseFormData(reader, config, idletime, timeouttask, stoptask);
		}

		/// <summary>
		/// The list of handled request modules
		/// </summary>
		private List<IHttpModule> m_handlerStack = new List<IHttpModule>();

		/// <summary>
		/// Gets the handlers that have processed this request
		/// </summary>
		public IEnumerable<IHttpModule> HandlerStack { get { return m_handlerStack; } }

		/// <summary>
		/// Clears the handler stack.
		/// </summary>
		internal void ClearHandlerStack()
		{
			m_handlerStack.Clear();
		}

		/// <summary>
		/// Registers a handler on the request stack
		/// </summary>
		public void PushHandlerOnStack(IHttpModule handler)
		{
			m_handlerStack.Add(handler);
		}

		/// <summary>
		/// Enforces that the handler stack obeys the requirements
		/// </summary>
		/// <param name="attributes">The list of attributes to check.</param>
		public void RequireHandler(IEnumerable<RequireHandlerAttribute> attributes)
		{
			if (attributes == null)
				return;

			foreach (var attr in attributes)
			{
				var any =
					attr.AllowDerived
						? m_handlerStack.Any(x => attr.RequiredType.IsAssignableFrom(x.GetType()))
						: m_handlerStack.Any(x => attr.RequiredType == x.GetType());

				if (!any)
					throw new RequirementFailedException($"Did not find any handlers of type {attr.RequiredType.FullName} while processing path {this.Path}. The handler stack contains: {string.Join(", ", m_handlerStack.Select(x => x.GetType().FullName))}");
			}
		}

		/// <summary>
		/// Enforces that the given type has processed the request
		/// </summary>
		/// <param name="handler">The type to check for.</param>
		/// <param name="allowderived">A flag indicating if the type match must be exact, or if derived types are accepted</param>
		public void RequireHandler(Type handler, bool allowderived = true)
		{
			RequireHandler(new[] { new RequireHandlerAttribute(handler) { AllowDerived = allowderived } });
		}

        /// <summary>
        /// Sets the processing timeout value.
        /// </summary>
        /// <param name="maxtime">The maximum processing time.</param>
        internal void SetProcessingTimeout(TimeSpan maxtime)
		{
            RequestProcessingStarted = DateTime.Now;

			if (maxtime.Ticks > 0)
				m_cancelRequest.CancelAfter(maxtime);
		}
	}
}

