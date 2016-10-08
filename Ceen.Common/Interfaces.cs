using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.Security.Cryptography.X509Certificates;

namespace Ceen.Common
{
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
	/// Interface for a http request.
	/// </summary>
	public interface IHttpRequest
	{
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
		IList<ResponseCookie> Cookies { get; }


		/// <summary>
		/// Adds a header to the output, use null to delete a header.
		/// This method throws an exception if the headers are already sent
		/// </summary>
		/// <param name="key">The header name.</param>
		/// <param name="value">The header value.</param>
		void AddHeader(string key, string value);

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
		IHttpRequest Request { get; }
		IHttpResponse Response { get; }
	}

	/// <summary>
	/// Basic interface for a request handler
	/// </summary>
	public interface IHttpModule
	{
		Task<bool> HandleAsync(IHttpContext context);
	}

	/// <summary>
	/// Interface for implementing a routing provider
	/// </summary>
	public interface IRouter
	{
		Task<bool> Process(IHttpContext context);
	}

	/// <summary>
	/// A delegate for handling a HTTP request
	/// </summary>
	public delegate Task<bool> HttpHandlerDelegate(IHttpContext context);

	/// <summary>
	/// Interface for implementing a logging provider
	/// </summary>
	public interface ILogger
	{
		Task LogRequest(IHttpContext context, Exception ex, DateTime started, TimeSpan duration);
	}

	/// <summary>
	/// Interface for logging requests before they are processed
	/// </summary>
	public interface IStartLogger : ILogger
	{
		Task LogRequestStarted(IHttpRequest request);
	}
}

