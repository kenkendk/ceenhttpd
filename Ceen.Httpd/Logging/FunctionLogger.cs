using System;
using System.Threading.Tasks;

namespace Ceen.Httpd.Logging
{
	/// <summary>
	/// Helper class for providing a logging method by a function delegate
	/// </summary>
	public class FunctionLogger : ILogger
	{
		/// <summary>
		/// The logging function
		/// </summary>
		private readonly LogDelegate m_func;
		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.Logging.FunctionLogger"/> class.
		/// </summary>
		/// <param name="func">The logging function.</param>
		public FunctionLogger(LogDelegate func)
		{
			m_func = func;
		}

		#region ILogger implementation

		/// <summary>
		/// Logs the request by calling the function.
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="request">The request.</param>
		/// <param name="response">The response.</param>
		/// <param name="ex">The exception.</param>
		/// <param name="started">Timestamp for when the request started.</param>
		/// <param name="duration">Duration of the request processing.</param>
		public Task LogRequest(IHttpContext context, Exception ex, DateTime started, TimeSpan duration)
		{
			return m_func(context, ex, started, duration);
		}

		#endregion
	}
}

