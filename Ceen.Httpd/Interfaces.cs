﻿using System;
using System.Threading.Tasks;

namespace Ceen.Httpd
{
	/// <summary>
	/// A delegate for handling a log event. Note that the response and exception values may be <c>null</c>.
	/// </summary>
	public delegate Task LogDelegate(IHttpContext context, Exception exception, DateTime started, TimeSpan duration);

	/// <summary>
	/// A delegate for handling a debug log event. Note that the response and exception values may be <c>null</c>.
	/// </summary>
	public delegate void DebugLogDelegate(string message, string logtaskid, object data);

	/// <summary>
	/// A delegate for handling a HTTP request
	/// </summary>
	public delegate Task<bool> HttpHandlerDelegate(IHttpContext context);

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


}
