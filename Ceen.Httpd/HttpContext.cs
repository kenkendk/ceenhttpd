using System;
using System.Collections.Generic;

namespace Ceen.Httpd
{
	/// <summary>
	/// A http context
	/// </summary>
	internal class HttpContext : IHttpContext
	{
		/// <summary>
		/// Gets the HTTP request
		/// </summary>
		public IHttpRequest Request { get; private set; }
		/// <summary>
		/// Gets the HTTP response.
		/// </summary>
		public IHttpResponse Response { get; private set; }

		/// <summary>
		/// Gets the storage creator
		/// </summary>
		public IStorageCreator Storage { get; private set; }

		/// <summary>
		/// Gets or sets the session storage.
		/// Note that this can be null if there is no session module loaded.
		/// </summary>
		public IDictionary<string, string> Session { get; set; }

		/// <summary>
		/// Additional data that can be used in a logging module to tag the request or response
		/// </summary>
		public IDictionary<string, string> LogData { get; } = new Dictionary<string, string>();

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.HttpContext"/> class.
		/// </summary>
		/// <param name="request">The HTTP request.</param>
		/// <param name="response">The HTTP response.</param>
		/// <param name="storage">The storage instance</param>
		public HttpContext(HttpRequest request, HttpResponse response, IStorageCreator storage)
		{
			this.Request = request;
			this.Response = response;
			this.Storage = storage;
		}
	}
}
