using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.Security.Cryptography.X509Certificates;

namespace Ceen
{
	/// <summary>
	/// Interface for a multipart-item, from a multi-part form request
	/// </summary>
	public interface IMultipartItem
	{
		/// <summary>
		/// The headers associated with the item
		/// </summary>
		/// <value>The headers.</value>
		IDictionary<string, string> Headers { get; }

		/// <summary>
		/// Gets or sets the form name.
		/// </summary>
		/// <value>The name.</value>
		string Name { get; }
		/// <summary>
		/// Gets or sets the filename.
		/// </summary>
		/// <value>The filename.</value>
		string Filename { get; }
		/// <summary>
		/// Gets the Content-Type header value.
		/// </summary>
		/// <value>The type of the content.</value>
		string ContentType { get; }
		/// <summary>
		/// The data for this entry
		/// </summary>
		/// <value>The data.</value>
		Stream Data { get; }
	}

	/// <summary>
	/// An interface describing a response cookie
	/// </summary>
	public interface IResponseCookie
	{
		/// <summary>
		/// List of settings attached to the cookie
		/// </summary>
		/// <value>The settings.</value>
		IDictionary<string, string> Settings { get; }

		/// <summary>
		/// The name of the cookie
		/// </summary>
		string Name { get; set; }

		/// <summary>
		/// The value of the cookie
		/// </summary>
		string Value { get; set; }

		/// <summary>
		/// Gets or sets the cookie path
		/// </summary>
		string Path { get; set; }

		/// <summary>
		/// Gets or sets the cookie domain
		/// </summary>
		string Domain { get; set; }

		/// <summary>
		/// Gets or sets the cookie expiration date
		/// </summary>
		DateTime? Expires { get; set; }

		/// <summary>
		/// Gets or sets the cookie max age.
		/// Zero or negative values means un-set
		/// </summary>
		long MaxAge { get; set; }

		/// <summary>
		/// Gets or sets the cookie secure flag
		/// </summary>
		bool Secure { get; set; }

		/// <summary>
		/// Gets or sets the cookie HttpOnly flag
		/// </summary>
		bool HttpOnly { get; set; }
	}

	/// <summary>
	/// Interface for a http request.
	/// </summary>
	public interface IHttpRequest
	{
		/// <summary>
		/// Gets the HTTP Request line as sent by the client
		/// </summary>
		string RawHttpRequestLine { get; }
		/// <summary>
		/// The HTTP method or Verb
		/// </summary>
		/// <value>The method.</value>
		string Method { get; }
		/// <summary>
		/// The path of the query, not including the query string
		/// </summary>
		/// <value>The path.</value>
		string Path { get; }
		/// <summary>
		/// Gets the original path before internal rewrite.
		/// </summary>
		/// <value>The original path.</value>
		string OriginalPath { get; }
		/// <summary>
		/// The query string
		/// </summary>
		/// <value>The query string, including the leading question mark.</value>
		string RawQueryString { get; }
		/// <summary>
		/// Gets a parsed representation of the query string.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The parsed query string.</value>
		IDictionary<string, string> QueryString { get; }
		/// <summary>
		/// Gets the headers found in the request.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The headers.</value>
		IDictionary<string, string> Headers { get; }
		/// <summary>
		/// Gets the form data, if any.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The form values.</value>
		IDictionary<string, string> Form { get; }
		/// <summary>
		/// Gets the cookies supplied, if any.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The cookie values.</value>
		IDictionary<string, string> Cookies { get; }
		/// <summary>
		/// Gets the posted files, if any.
		/// Duplicate values are not represented, instead only the latest is stored
		/// </summary>
		/// <value>The files.</value>
		IList<IMultipartItem> Files { get; }
		/// <summary>
		/// Gets the http version string.
		/// </summary>
		/// <value>The http version.</value>
		string HttpVersion { get; }
		/// <summary>
		/// Gets or sets a user identifier attached to the request.
		/// This can be set by handlers processing the request to simplify dealing with logged in users.
		/// Handlers should only set this is the user is authenticated.
		/// </summary>
		string UserID { get; set; }
		/// <summary>
		/// Gets a value indicating what connection security is used.
		/// </summary>
		SslProtocols SslProtocol { get; }
		/// <summary>
		/// Gets the remote endpoint
		/// </summary>
		EndPoint RemoteEndPoint { get; }
		/// <summary>
		/// Gets the client SSL certificate, if any
		/// </summary>
		X509Certificate ClientCertificate { get; }
		/// <summary>
		/// The taskid used for logging and tracing the request
		/// </summary>
		string LogTaskID { get; }

		/// <summary>
		/// The stream representing the body of the request
		/// </summary>
		Stream Body { get; }

		/// <summary>
		/// Gets the HTTP Content-Type header value
		/// </summary>
		/// <value>The type of the content.</value>
		string ContentType { get; }

		/// <summary>
		/// Gets the HTTP Content-Length header value
		/// </summary>
		/// <value>The length of the content.</value>
		int ContentLength { get; }

		/// <summary>
		/// Gets a dictionary with items attached to the current request.
		/// </summary>
		IDictionary<string, object> RequestState { get; }

		/// <summary>
		/// Gets the handlers that have processed this request
		/// </summary>
		IEnumerable<IHttpModule> HandlerStack { get; }

		/// <summary>
		/// Registers a handler on the request stack
		/// </summary>
		void PushHandlerOnStack(IHttpModule handler);

		/// <summary>
		/// Enforces that the handler stack obeys the requirements
		/// </summary>
		/// <param name="attributes">The list of attributes to check.</param>
		void RequireHandler(IEnumerable<RequireHandlerAttribute> attributes);

		/// <summary>
		/// Enforces that the given type has processed the request
		/// </summary>
		/// <param name="handler">The type to check for.</param>
		/// <param name="allowderived">A flag indicating if the type match must be exact, or if derived types are accepted</param>
		void RequireHandler(Type handler, bool allowderived = true);
	}

	/// <summary>
	/// Interface for a http response.
	/// </summary>
	public interface IHttpResponse
	{
		/// <summary>
		/// Gets or sets the HTTP version to report.
		/// </summary>
		/// <value>The http version.</value>
		string HttpVersion { get; set; }
		/// <summary>
		/// Gets or sets the status code to report.
		/// </summary>
		/// <value>The status code.</value>
		HttpStatusCode StatusCode { get; set; }
		/// <summary>
		/// Gets or sets the status message to report.
		/// If this is <c>null</c>, the default message for
		/// the HTTP status code is used
		/// </summary>
		/// <value>The status message.</value>
		string StatusMessage { get; set; }
		/// <summary>
		/// Gets a value indicating whether the sent headers are sent to the client.
		/// Once the headers are sent, the header collection can no longer be modified
		/// </summary>
		/// <value><c>true</c> if this instance has sent headers; otherwise, <c>false</c>.</value>
		bool HasSentHeaders { get; }
		/// <summary>
		/// Dictionary with headers that are sent as part of the response.
		/// Cannot be modified after the headers have been sent.
		/// </summary>
		/// <value>The headers.</value>
		IDictionary<string, string> Headers { get; }

		/// <summary>
		/// Gets a list of cookies that are set with the response.
		/// Cannot be modified after the headers have been sent.
		/// </summary>
		/// <value>The cookies.</value>
		IList<IResponseCookie> Cookies { get; }

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
		IResponseCookie AddCookie(string name, string value, string path = null, string domain = null, DateTime? expires = null, long maxage = -1, bool secure = false, bool httponly = false);

		/// <summary>
		/// Adds a header to the output, use null to delete a header.
		/// This method throws an exception if the headers are already sent
		/// </summary>
		/// <param name="key">The header name.</param>
		/// <param name="value">The header value.</param>
		void AddHeader(string key, string value);

		/// <summary>
		/// Performs an internal redirect
		/// </summary>
		/// <param name="path">The new path to use.</param>
		void InternalRedirect(string path);

		/// <summary>
		/// Gets a value indicating if an internal redirect has been requested
		/// </summary>
		bool IsRedirectingInternally { get; }

		/// <summary>
		/// Gets or sets the Content-Type header
		/// </summary>
		/// <value>The type of the content.</value>
		string ContentType { get; set; }

		/// <summary>
		/// Gets or sets the Content-Length header
		/// </summary>
		/// <value>The length of the content.</value>
		long ContentLength { get; set; }

		/// <summary>
		/// Gets or sets the Keep-Alive header
		/// </summary>
		/// <value><c>true</c> if keep alive; otherwise, <c>false</c>.</value>
		bool KeepAlive { get; set; }

		/// <summary>
		/// Flush all headers async.
		/// This method can be called multiple times if desired.
		/// </summary>
		/// <returns>The headers async.</returns>
		Task FlushHeadersAsync();

		/// <summary>
		/// Copies the stream to the output. Note that the stream is copied from the current position to the end, and the stream must report the length.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The stream to copy.</param>
		/// <param name="contenttype">An optional content type to set. Throws an exception if the headers are already sent.</param>
		Task WriteAllAsync(Stream data, string contenttype = null);

		/// <summary>
		/// Writes the byte array to the output.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The data to write.</param>
		/// <param name="contenttype">An optional content type to set. Throws an exception if the headers are already sent.</param>
		Task WriteAllAsync(byte[] data, string contenttype = null);

		/// <summary>
		/// Writes the string to the output using UTF-8 encoding.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The data to write.</param>
		/// <param name="encoding">The encoding to apply.</param>
		/// <param name="contenttype">An optional content type to set. Throws an exception if the headers are already sent.</param>
		Task WriteAllAsync(string data, string contenttype = null);

		/// <summary>
		/// Writes the string to the output.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The data to write.</param>
		/// <param name="encoding">The encoding to apply.</param>
		/// <param name="contenttype">An optional content type to set. Throws an exception if the headers are already sent.</param>
		Task WriteAllAsync(string data, Encoding encoding, string contenttype = null);

		/// <summary>
		/// Writes the json string to the output with UTF-8 encoding.
		/// </summary>
		/// <returns>The awaitable task</returns>
		/// <param name="data">The JSON data to write.</param>
		Task WriteAllJsonAsync(string data);

		/// <summary>
		/// Performs a 302 redirect
		/// </summary>
		/// <param name="newurl">The target url.</param>
		void Redirect(string newurl);

		/// <summary>
		/// Sets headers that instruct the client and proxies to avoid caching the response
		/// </summary>
		void SetNonCacheable();

		/// <summary>
		/// Gets the response stream.
		/// To avoid buffering the contents, make sure the
		/// Content-Length header is set before writing to the stream
		/// </summary>
		/// <returns>The response stream.</returns>
		Stream GetResponseStream();
	}

	/// <summary>
	/// Interface for a http handler
	/// </summary>
	public interface IHttpContext
	{
		/// <summary>
		/// Gets the request.
		/// </summary>
		IHttpRequest Request { get; }

		/// <summary>
		/// Gets the response
		/// </summary>
		IHttpResponse Response { get; }

		/// <summary>
		/// Gets the storage creator
		/// </summary>
		IStorageCreator Storage { get; }
	
		/// <summary>
		/// Gets or sets the session storage.
		/// Note that this can be null if there is no session module loaded.
		/// </summary>
		IDictionary<string, string> Session { get; set; }

		/// <summary>
		/// Additional data that can be used in a logging module to tag the request or response
		/// </summary>
		IDictionary<string, string> LogData { get; }
	}

	/// <summary>
	/// Interface for implementing a routing provider
	/// </summary>
	public interface IRouter
	{
		/// <summary>
		/// Process the request for the specified context.
		/// </summary>
		/// <param name="context">The context to use.</param>
		/// <returns>A value indicating if the request is now processed</returns>
		Task<bool> Process(IHttpContext context);
	}

	/// <summary>
	/// Basic interface for a request handler
	/// </summary>
	public interface IHttpModule
	{
		/// <summary>
		/// Process the request for the specified context.
		/// </summary>
		/// <param name="context">The context to use.</param>
		/// <returns>A value indicating if the request is now processed</returns>
		Task<bool> HandleAsync(IHttpContext context);
	}

	/// <summary>
	/// Interface for implementing a storage creator
	/// </summary>
	public interface IStorageCreator
	{
		/// <summary>
		/// Gets or creates a storage module with the given name
		/// </summary>
		/// <returns>The storage module or null.</returns>
		/// <param name="name">The name of the module to get.</param>
		/// <param name="key">The session key of the module, or null.</param>
		/// <param name="ttl">The module time-to-live, zero or less means no expiration.</param>
		/// <param name="autocreate">Automatically create storage if not found</param>
		Task<IStorageEntry> GetStorageAsync(string name, string key, int ttl, bool autocreate);
	}

	/// <summary>
	/// Interface for storing data
	/// </summary>
	public interface IStorageEntry : IDictionary<string, string>
	{
		/// <summary>
		/// Gets the name of the storage element
		/// </summary>
		string Name { get; }
		/// <summary>
		/// Gets or sets the time the dictionary expires
		/// </summary>
		DateTime Expires { get; set; }
	}

	/// <summary>
	/// Interface for implementing a logging provider
	/// </summary>
	public interface ILogger
	{
		/// <summary>
		/// Logs a request.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="context">The execution context.</param>
		/// <param name="ex">The exception being logged, may be null.</param>
		/// <param name="started">The time the request started.</param>
		/// <param name="duration">The request duration.</param>
		Task LogRequest(IHttpContext context, Exception ex, DateTime started, TimeSpan duration);
	}

	/// <summary>
	/// Interface for logging requests before they are processed
	/// </summary>
	public interface IStartLogger : ILogger
	{
		/// <summary>
		/// Logs the start of a request.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="request">The request being started.</param>
		Task LogRequestStarted(IHttpRequest request);
	}

	/// <summary>
	/// Marker interface for a generic module
	/// </summary>
	public interface IModule
	{
	}
}
