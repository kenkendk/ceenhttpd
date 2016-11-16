using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.IO;
using System.Linq;

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
		public string Path { get; private set; }
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
		/// Gets the http version string.
		/// </summary>
		/// <value>The http version.</value>
		public string HttpVersion { get; private set; }
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
		public HttpRequest(System.Net.EndPoint remoteEndpoint, string logtaskid, string logrequestid, X509Certificate clientCert)
		{
			RemoteEndPoint = remoteEndpoint;
			ClientCertificate = clientCert;
			LogTaskID = logtaskid;
			LogRequestID = logrequestid;
			Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);
			QueryString = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);
			Form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);
			Cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);
		}

		/// <summary>
		/// Handles a line from the input stream.
		/// </summary>
		/// <param name="self">The request.</param>
		/// <param name="line">The line being read.</param>
		private void HandleLine(string line)
		{
			// Check and parse the HTTP request line
			if (this.HttpVersion == null)
			{
				var components = line.Split(new char[] {' '}, 4);
				if (components.Length != 3)
					throw new HttpException(HttpStatusCode.BadRequest);

				if (components[2] != "HTTP/1.1")
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
				this.Path = path;
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
					foreach(var k in SplitHeaderLine((components[1] ?? string.Empty).Trim()))
						Cookies[Uri.UnescapeDataString(k.Key)] = Uri.UnescapeDataString(k.Value);

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
		/// Splits a header line into its key-value components
		/// </summary>
		/// <returns>The components.</returns>
		/// <param name="line">The line to split.</param>
		public virtual IEnumerable<KeyValuePair<string, string>> SplitHeaderLine(string line)
		{
			return (line ?? "").Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x =>
				{
					var c = x.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
					var value = (c.Skip(1).FirstOrDefault() ?? "").Trim();
					if (value.StartsWith("\"") && value.EndsWith("\""))
						value = value.Substring(1, value.Length - 2);
					return new KeyValuePair<string, string>(c.First().Trim(), value);
				});
		}


		/// <summary>
		/// Gets a named component from a header line
		/// </summary>
		/// <returns>The header component or null.</returns>
		/// <param name="line">The header line.</param>
		/// <param name="key">The component to find.</param>
		public virtual string GetHeaderComponent(string line, string key)
		{
			return
				SplitHeaderLine(line)
				.Where(x => string.Equals(x.Key,key, StringComparison.OrdinalIgnoreCase))
				.Select(x => x.Value)
				.FirstOrDefault();		
		}

		/// <summary>
		/// Gets an encoding from a charset string
		/// </summary>
		/// <returns>The encoding for the charset.</returns>
		/// <param name="charset">The charset string.</param>
		public virtual System.Text.Encoding GetEncodingForCharset(string charset)
		{
			if (string.Equals("utf-8", charset, StringComparison.OrdinalIgnoreCase))
				return System.Text.Encoding.UTF8;
			else if (string.Equals("ascii", charset, StringComparison.OrdinalIgnoreCase))
				return System.Text.Encoding.ASCII;
			else
				return System.Text.Encoding.GetEncoding(charset);
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
			else if ((this.ContentType ?? "").StartsWith("multipart/form-data") && this.ContentLength > 0 && this.ContentLength < config.MaxUrlEncodedFormSize && config.AutoParseMultipartFormData)
			{
				await ParseMultiPart(
					async (headers, stream) =>
					{
						var dispositionItems = SplitHeaderLine(headers["Content-Disposition"]);
						if (!string.Equals(dispositionItems.FirstOrDefault().Key, "form-data", StringComparison.OrdinalIgnoreCase))
							throw new HttpException(HttpStatusCode.BadRequest);

						var name = GetHeaderComponent(headers["Content-Disposition"], "name");
						if (string.IsNullOrWhiteSpace("name"))
							throw new HttpException(HttpStatusCode.BadRequest);
						
						var filename = GetHeaderComponent(headers["Content-Disposition"], "filename");
						var charset = GetHeaderComponent(headers["Content-Type"], "charset") ?? "ascii";

						if (string.IsNullOrWhiteSpace(filename))
						{
							using (var sr = new StreamReader(stream, GetEncodingForCharset(charset)))
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
	}
}

