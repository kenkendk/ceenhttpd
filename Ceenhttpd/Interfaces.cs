using System;
using System.Threading.Tasks;

namespace Ceenhttpd
{
	/// <summary>
	/// Basic interface for a request handler
	/// </summary>
	public interface IHttpModule
	{
		Task<bool> HandleAsync(HttpRequest request, HttpResponse response);
	}

	/// <summary>
	/// Interface for implementing a routing provider
	/// </summary>
	public interface IRouter
	{
		Task<bool> Process(HttpRequest request, HttpResponse response);
	}

	/// <summary>
	/// A delegate for handling a log event. Note that the response and exception values may be <c>null</c>.
	/// </summary>
	public delegate Task LogDelegate(HttpRequest request, HttpResponse response, Exception exception, DateTime started, TimeSpan duration);

	/// <summary>
	/// A delegate for handling a debug log event. Note that the response and exception values may be <c>null</c>.
	/// </summary>
	public delegate void DebugLogDelegate(string message, string logtaskid, object data);

	/// <summary>
	/// A delegate for handling a HTTP request
	/// </summary>
	public delegate Task<bool> HttpHandlerDelegate(HttpRequest request, HttpResponse response);

	/// <summary>
	/// Interface for implementing a logging provider
	/// </summary>
	public interface ILogger
	{
		Task LogRequest(HttpRequest request, HttpResponse response, Exception ex, DateTime started, TimeSpan duration);
	}

	/// <summary>
	/// Interface for logging requests before they are processed
	/// </summary>
	public interface IStartLogger : ILogger
	{
		Task LogRequestStarted(HttpRequest request);
	}

	/// <summary>
	/// Marker interface for REST enabled handlers
	/// </summary>
	public interface IRestHandler
	{
	}

	/// <summary>
	/// Interface for a REST handler module that accepts GET requests
	/// </summary>
	public interface IRestGetHandler : IRestHandler
	{
		Task<bool> HandleGetAsync(HttpRequest request, HttpResponse response);
	}

	/// <summary>
	/// Interface for a REST handler module that accepts PUT requests
	/// </summary>
	public interface IRestPutHandler : IRestHandler
	{
		Task<bool> HandlePutAsync(HttpRequest request, HttpResponse response);
	}

	/// <summary>
	/// Interface for a REST handler module that accepts POST requests
	/// </summary>
	public interface IRestPostHandler : IRestHandler
	{
		Task<bool> HandlePostAsync(HttpRequest request, HttpResponse response);
	}

	/// <summary>
	/// Interface for a REST handler module that accepts PATCH requests
	/// </summary>
	public interface IRestPatchHandler : IRestHandler
	{
		Task<bool> HandlePatchAsync(HttpRequest request, HttpResponse response);
	}

	/// <summary>
	/// Interface for a REST handler module that accepts DELETE requests
	/// </summary>
	public interface IRestDeleteHandler : IRestHandler
	{
		Task<bool> HandleDeleteAsync(HttpRequest request, HttpResponse response);
	}
}

