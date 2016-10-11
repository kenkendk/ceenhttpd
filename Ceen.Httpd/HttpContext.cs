using System;

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
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.HttpContext"/> class.
		/// </summary>
		/// <param name="request">The HTTP request.</param>
		/// <param name="response">The HTTP response.</param>
		public HttpContext(HttpRequest request, HttpResponse response)
		{
			this.Request = request;
			this.Response = response;
		}
	}
}
