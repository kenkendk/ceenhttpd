using System;
using System.Threading.Tasks;

namespace Ceen.Httpd.Logging
{
	/// <summary>
	/// Outputs Common Log Format to STDOUT
	/// </summary>
	public class CLFStdOut : CLFLogger
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Logging.CFLStdOut"/> class.
		/// </summary>
		public CLFStdOut()
			: base(Console.OpenStandardOutput())
		{
		}
	}

	/// <summary>
	/// Outputs Common Log Format to STDERR
	/// </summary>
	public class CLFStdErr : CLFLogger
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Logging.CFLStdErr"/> class.
		/// </summary>
		public CLFStdErr()
			: base(Console.OpenStandardError())
		{
		}
	}

	/// <summary>
	/// Logger that outputs exception messages to stdout
	/// </summary>
	public class StdErrErrors : FunctionLogger
	{
		/// <summary>
		/// A static cached instance of the StdErr stream
		/// </summary>
		private static readonly System.IO.StreamWriter _stderr;

		/// <summary>
		/// Static initializer
		/// </summary>
		static StdErrErrors()
		{
			_stderr = new System.IO.StreamWriter(Console.OpenStandardError(), System.Text.Encoding.UTF8, 1024, true);
			_stderr.AutoFlush = true;
		}
		
		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Logging.StdErrErrors"/> class.
		/// </summary>
		public StdErrErrors()
			: base(HandleMsg)
		{
		}

		/// <summary>
		/// Handles the log message.
		/// </summary>
		/// <returns><c>true</c></returns>
		/// <param name="context">The http context.</param>
		/// <param name="exception">The exception.</param>
		/// <param name="started">The time the request started.</param>
		/// <param name="duration">The request duration.</param>
		static Task HandleMsg(IHttpContext context, Exception exception, DateTime started, TimeSpan duration)
		{
			if (exception != null)
				_stderr.WriteLine(exception);
			return Task.FromResult(true);
		}
	}

	/// <summary>
	/// Logger that outputs exception messages to stdout
	/// </summary>
	public class StdOutErrors : FunctionLogger
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Logging.StdErrErrors"/> class.
		/// </summary>
		public StdOutErrors()
			: base(HandleMsg)
		{
		}

		/// <summary>
		/// Handles the log message.
		/// </summary>
		/// <returns><c>true</c></returns>
		/// <param name="context">The http context.</param>
		/// <param name="exception">The exception.</param>
		/// <param name="started">The time the request started.</param>
		/// <param name="duration">The request duration.</param>
		static Task HandleMsg(IHttpContext context, Exception exception, DateTime started, TimeSpan duration)
		{
			if (exception != null)
				Console.WriteLine(exception);
			return Task.FromResult(true);
		}
	}
}
