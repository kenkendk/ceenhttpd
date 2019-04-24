using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
		/// The delegate used to forward exceptions to the loggers 
		/// </summary>
		internal Func<LogLevel, string, Exception, Task> LogHandlerDelegate { get; set;}

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

        /// <summary>
        /// Logs a message
        /// </summary>
        /// <param name="level">The level to log</param>
        /// <param name="message">The message to log</param>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public Task LogMessageAsync(LogLevel level, string message, Exception ex) => LogHandlerDelegate?.Invoke(level, message, ex) ?? Task.FromResult(true);
    }
}
