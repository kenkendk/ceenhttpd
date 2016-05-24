using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ceenhttpd.Logging
{
	/// <summary>
	/// A simple log splitter that sends log information to multiple destinations
	/// </summary>
	public class LogSplitter : ILogger
	{
		/// <summary>
		/// The list of log targets
		/// </summary>
		private readonly ILogger[] m_targets;

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.Logging.LogSplitter"/> class.
		/// </summary>
		/// <param name="targets">The log targets.</param>
		public LogSplitter(IEnumerable<ILogger> targets)
			: this(targets, null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.Logging.LogSplitter"/> class.
		/// </summary>
		/// <param name="funcs">The log targets.</param>
		public LogSplitter(IEnumerable<LogDelegate> funcs)
			: this(null, funcs)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.Logging.LogSplitter"/> class.
		/// </summary>
		/// <param name="targets">The logger instance targets.</param>
		/// <param name="funcs">The logger function targets.</param>
		public LogSplitter(IEnumerable<ILogger> targets, IEnumerable<LogDelegate> funcs)
		{
			var list = new ILogger[0].AsEnumerable();
			if (targets != null)
				list = list.Union(targets);
			if (funcs != null)
				list = list.Union(funcs.Select(x => new FunctionLogger(x)));

			m_targets = list.ToArray();
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
		public Task LogRequest(HttpRequest request, HttpResponse response, Exception ex, DateTime started, TimeSpan duration)
		{
			return Task.WhenAll(m_targets.Select(x => x.LogRequest(request, response, ex, started, duration)));
		}
		#endregion
	}
}

