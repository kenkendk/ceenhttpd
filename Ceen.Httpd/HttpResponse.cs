using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace Ceen.Httpd
{
	/// <summary>
	/// Interface for providing a HTTP response
	/// </summary>
	internal class HttpResponse : IHttpResponse
	{
		/// <summary>
		/// Dictionary wrapper to keep track of what headers to emit
		/// </summary>
		private class HttpResponseHeaders : IDictionary<string, string>
		{
			/// <summary>
			/// The wrapped parent
			/// </summary>
			private readonly HttpResponse m_parent;

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Httpd.HttpResponse.HttpResponseHeaders"/> class.
			/// </summary>
			/// <param name="parent">The parent to wrap.</param>
			public HttpResponseHeaders(HttpResponse parent)
			{
				m_parent = parent;
			}

			#region IDictionary implementation
			public bool ContainsKey(string key)
			{
				return m_parent.m_headers.ContainsKey(key);
			}
			public void Add(string key, string value)
			{
				m_parent.AddHeader(key, value);
			}
			public bool Remove(string key)
			{
				var hasit = ContainsKey(key);
				m_parent.AddHeader(key, null);
				return hasit;
			}
			public bool TryGetValue(string key, out string value)
			{
				return m_parent.m_headers.TryGetValue(key, out value);
			}
			public string this[string index]
			{
				get
				{
					string s;
					if (!TryGetValue(index, out s))
						s = null;

					return s;
				}
				set
				{
					m_parent.AddHeader(index, value);
				}
			}
			public ICollection<string> Keys
			{
				get
				{
					return m_parent.m_headers.Keys;
				}
			}
			public ICollection<string> Values
			{
				get
				{
					return m_parent.m_headers.Values;
				}
			}
			#endregion
			#region ICollection implementation
			public void Add(KeyValuePair<string, string> item)
			{
				m_parent.AddHeader(item.Key, item.Value);
			}
			public void Clear()
			{
				if (m_parent.HasSentHeaders)
					m_parent.AddHeader("dummy", "dummy"); // Trigger exeception
				m_parent.m_headers.Clear();
			}
			public bool Contains(KeyValuePair<string, string> item)
			{
				string value;
				return TryGetValue(item.Key, out value) && value == item.Value;
			}
			public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
			{
				throw new NotImplementedException();
			}
			public bool Remove(KeyValuePair<string, string> item)
			{
				if (Contains(item) || m_parent.HasSentHeaders)
				{
					Remove(item.Key);
					return true;
				}

				return false;
			}
			public int Count
			{
				get
				{
					return m_parent.m_headers.Count;
				}
			}
			public bool IsReadOnly
			{
				get
				{
					return m_parent.HasSentHeaders;
				}
			}
			#endregion
			#region IEnumerable implementation
			public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
			{
				return m_parent.m_headers.GetEnumerator();
			}
			#endregion
			#region IEnumerable implementation
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
			#endregion
		}

		/// <summary>
		/// Gets or sets the HTTP version to report.
		/// </summary>
		/// <value>The http version.</value>
		public string HttpVersion { get; set; }
		/// <summary>
		/// Gets or sets the status code to report.
		/// </summary>
		/// <value>The status code.</value>
		public HttpStatusCode StatusCode { get; set; }
		/// <summary>
		/// Gets or sets the status message to report.
		/// If this is <c>null</c>, the default message for
		/// the HTTP status code is used
		/// </summary>
		/// <value>The status message.</value>
		public string StatusMessage { get; set; }
		/// <summary>
		/// Gets a value indicating whether the sent headers are sent to the client.
		/// Once the headers are sent, the header collection can no longer be modified
		/// </summary>
		/// <value><c>true</c> if this instance has sent headers; otherwise, <c>false</c>.</value>
		public bool HasSentHeaders { get { return m_hasSentHeaders; } }
		/// <summary>
		/// Dictionary with headers that are sent as part of the response.
		/// Cannot be modified after the headers have been sent.
		/// </summary>
		/// <value>The headers.</value>
		public IDictionary<string, string> Headers { get { return m_headerwrapper; } }

		/// <summary>
		/// Gets a list of cookies that are set with the response.
		/// Cannot be modified after the headers have been sent.
		/// </summary>
		/// <value>The cookies.</value>
		public IList<IResponseCookie> Cookies { get; private set; }

		/// <summary>
		/// The underlying output stream
		/// </summary>
		private Stream m_stream;
		/// <summary>
		/// The intercepting output stream exposed from this instance
		/// </summary>
		private ResponseOutputStream m_outstream;
		/// <summary>
		/// The internal storage for the response headers
		/// </summary>
		private Dictionary<string, string> m_headers;
		/// <summary>
		/// The value indicating if the headers have been sent
		/// </summary>
		private bool m_hasSentHeaders = false;
		/// <summary>
		/// The wrapper class for controlling headers
		/// </summary>
		private HttpResponseHeaders m_headerwrapper;
		/// <summary>
		/// The server configuration
		/// </summary>
		private readonly ServerConfig m_serverconfig;

		/// <summary>
		/// The CRLF line termination string
		/// </summary>
		private const string CRLF = "\r\n";

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceen.Httpd.HttpResponse"/> class.
		/// </summary>
		/// <param name="stream">The underlying stream.</param>
		/// <param name="config">The server configuration.</param>
		public HttpResponse(Stream stream, ServerConfig config)
		{
			m_stream = stream;
			m_serverconfig = config;
			m_headerwrapper = new HttpResponseHeaders(this);
			Cookies = new List<IResponseCookie>();
			Reset();
		}

		/// <summary>
		/// Resets this instance so it can be re-used
		/// </summary>
		private void Reset()
		{
			this.HttpVersion = "HTTP/1.1";
			this.StatusCode = HttpStatusCode.OK;

			m_headers = new Dictionary<string, string>();
			m_outstream = new ResponseOutputStream(m_stream, this);
			m_hasSentHeaders = false;

			AddDefaultHeaders();
		}

		/// <summary>
		/// Adds the default headers
		/// </summary>
		private void AddDefaultHeaders()
		{
			if (m_serverconfig.AddDefaultResponseHeaders != null)
				m_serverconfig.AddDefaultResponseHeaders(this);
		}

		/// <summary>
		/// Adds a header to the output, use null to delete a header.
		/// This method throws an exception if the headers are already sent
		/// </summary>
		/// <param name="key">The header name.</param>
		/// <param name="value">The header value.</param>
		public void AddHeader(string key, string value)
		{
			if (m_hasSentHeaders)
				throw new InvalidOperationException("Cannot set headers after they are sent");

			if (value == null)
				m_headers.Remove(key);
			else
				m_headers[key] = value;
		}

		/// <summary>
		/// Gets or sets the Content-Type header
		/// </summary>
		/// <value>The type of the content.</value>
		public string ContentType
		{
			get
			{
				string v;
				m_headers.TryGetValue("Content-Type", out v);

				return v;
			}
			set
			{
				AddHeader("Content-Type", value);
			}
		}

		/// <summary>
		/// Gets or sets the Content-Length header
		/// </summary>
		/// <value>The length of the content.</value>
		public long ContentLength
		{
			get
			{
				string v;
				m_headers.TryGetValue("Content-Length", out v);

				long vv;
				if (!long.TryParse(v, out vv))
					return -1;
				
				return vv;
			}
			set
			{
				AddHeader("Content-Length", value == -1 ? null : value.ToString());
			}
		}

		/// <summary>
		/// Gets or sets the Keep-Alive header
		/// </summary>
		/// <value><c>true</c> if keep alive; otherwise, <c>false</c>.</value>
		public bool KeepAlive
		{
			get
			{
				string v;
				m_headers.TryGetValue("Connection", out v);

				return string.Equals("keep-alive", v, StringComparison.OrdinalIgnoreCase);
			}
			set
			{
				AddHeader("Connection", value ? "keep-alive" : "close");
			}
		}

		/// <summary>
		/// Helper property to check if the internal stream has written the number of bytes
		/// sent with Content-Length. Used to determine if keep-alive is possible
		/// </summary>
		/// <value><c>true</c> if this instance has written correct length; otherwise, <c>false</c>.</value>
		internal bool HasWrittenCorrectLength
		{
			get
			{
				if (ContentLength < 0)
					return false;

				return m_outstream.Length == ContentLength;
			}
		}

		/// <summary>
		/// Flush all headers async.
		/// This method can be called multiple times if desired.
		/// </summary>
		/// <returns>The headers async.</returns>
		public async Task FlushHeadersAsync()
		{
			if (!m_hasSentHeaders)
			{
				if (string.IsNullOrWhiteSpace(this.StatusMessage))
					this.StatusMessage = HttpStatusMessages.DefaultMessage(this.StatusCode);

				var line = Encoding.ASCII.GetBytes(string.Format("{0} {1} {2}{3}", this.HttpVersion, (int)this.StatusCode, this.StatusMessage, CRLF));
				await m_stream.WriteAsync(line, 0, line.Length);

				foreach (var e in m_headers)
				{
					line = Encoding.ASCII.GetBytes(string.Format("{0}: {1}{2}", e.Key, e.Value, CRLF));
					await m_stream.WriteAsync(line, 0, line.Length);
				}

				foreach (var cookie in Cookies)
				{
					Func<string, string> cookievalue = x => string.IsNullOrWhiteSpace(x) ? "" : string.Format("=\"{0}\"", x);
					line = Encoding.ASCII.GetBytes(string.Format("Set-Cookie: {0}{1}{2}", cookie.Name, string.Join("; ", new string[] { cookievalue(cookie.Value) }.Union(cookie.Settings.Select(x => string.Format("{0}{1}", x.Key, cookievalue(x.Value)))).Where(x => !string.IsNullOrWhiteSpace(x))), CRLF));
					await m_stream.WriteAsync(line, 0, line.Length);
				}

				if (Cookies is List<IResponseCookie>)
					Cookies = ((List<IResponseCookie>)Cookies).AsReadOnly();

				line = Encoding.ASCII.GetBytes(CRLF);
				await m_stream.WriteAsync(line, 0, line.Length);
			
				m_hasSentHeaders = true;
			}
		}

		/// <summary>
		/// Flushes all headers and sets the length to the amount of data currently buffered in the output
		/// </summary>
		/// <returns>The and set length async.</returns>
		internal Task FlushAndSetLengthAsync()
		{
			return m_outstream.SetLengthAndFlushAsync(true);
		}

		/// <summary>
		/// Flush the contents after an error occurred.
		/// </summary>
		/// <returns>The as error async.</returns>
		internal async Task FlushAsErrorAsync()
		{
			if (!m_hasSentHeaders)
				await FlushHeadersAsync();
			m_outstream.Clear();
		}

		/// <summary>
		/// Copies the stream to the output. Note that the stream is copied from the current position to the end, and the stream must report the length.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The stream to copy.</param>
		/// <param name="contenttype">An optional content type to set. Throws an exception if the headers are already sent.</param>
		public Task WriteAllAsync(Stream data, string contenttype = null)
		{
			if (contenttype != null)
				ContentType = contenttype;
			if (!HasSentHeaders)
				ContentLength = data.Length - data.Position;
			return data.CopyToAsync(m_outstream);
		}

		/// <summary>
		/// Writes the byte array to the output.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The data to write.</param>
		/// <param name="contenttype">An optional content type to set. Throws an exception if the headers are already sent.</param>
		public Task WriteAllAsync(byte[] data, string contenttype = null)
		{
			if (contenttype != null)
				ContentType = contenttype;
			if (!HasSentHeaders)
				ContentLength = data.Length;
			return m_outstream.WriteAsync(data, 0, data.Length);
		}

		/// <summary>
		/// Writes the string to the output using UTF-8 encoding.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The data to write.</param>
		/// <param name="encoding">The encoding to apply.</param>
		/// <param name="contenttype">An optional content type to set. Throws an exception if the headers are already sent.</param>
		public Task WriteAllAsync(string data, string contenttype = null)
		{
			return WriteAllAsync(System.Text.Encoding.UTF8.GetBytes(data), contenttype);
		}

		/// <summary>
		/// Writes the string to the output.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The data to write.</param>
		/// <param name="encoding">The encoding to apply.</param>
		/// <param name="contenttype">An optional content type to set. Throws an exception if the headers are already sent.</param>
		public Task WriteAllAsync(string data, Encoding encoding, string contenttype = null)
		{
			return WriteAllAsync(encoding.GetBytes(data), contenttype);
		}

		/// <summary>
		/// Writes the json string to the output with UTF-8 encoding.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The JSON data to write.</param>
		public Task WriteAllJsonAsync(string data)
		{
			if (!HasSentHeaders)
				ContentType = "application/json; charset=utf-8";
			return WriteAllAsync(System.Text.Encoding.UTF8.GetBytes(data));
		}

		/// <summary>
		/// Performs a 302 redirect
		/// </summary>
		/// <param name="newurl">The target url.</param>
		public void Redirect(string newurl)
		{
			Headers["Location"] = newurl;
			StatusCode = HttpStatusCode.Found;
			StatusMessage = HttpStatusMessages.DefaultMessage(StatusCode);
		}

		/// <summary>
		/// Sets headers that instruct the client and proxies to avoid caching the response
		/// </summary>
		public void SetNonCacheable()
		{
			Headers["Vary"] = "X-Origin,Origin,Accept-Encoding";
			Headers["Date"] = Headers["Expires"] = DateTime.Now.ToString("R", CultureInfo.InvariantCulture);
			Headers["Cache-Control"] = "private, max-age=0";
		}

		/// <summary>
		/// Gets the response stream.
		/// To avoid buffering the contents, make sure the
		/// Content-Length header is set before writing to the stream
		/// </summary>
		/// <returns>The response stream.</returns>
		public Stream GetResponseStream()
		{
			return m_outstream;
		}

		/// <summary>
		/// Adds a cookie to the output
		/// </summary>
		/// <returns>The new cookie.</returns>
		/// <param name="name">The name of the cookie.</param>
		/// <param name="value">The cookie value.</param>
		/// <param name="path">The optional path limiter.</param>
		/// <param name="domain">The optional domain limiter.</param>
		/// <param name="expires">The optional expiration date.</param>
		/// <param name="maxage">The optional maximum age.</param>
		/// <param name="secure">A flag for making the cookie available over SSL only.</param>
		/// <param name="httponly">A flag indicating if the cookie should be hidden from the scripting environment.</param>
		public IResponseCookie AddCookie(string name, string value, string path = null, string domain = null, DateTime? expires = null, long maxage = -1, bool secure = false, bool httponly = false)
		{
			return new ResponseCookie(name, value) 
			{
				Path = path,
				Domain = domain,
				Expires = expires,
				MaxAge = maxage,
				Secure = secure,
				HttpOnly = httponly
			};
		}

	}
}

