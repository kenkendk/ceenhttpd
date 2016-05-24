using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Globalization;

namespace Ceenhttpd.Logging
{
	/// <summary>
	/// Implementation of a logger for the Combined Log Format
	/// </summary>
	public class CLFLogger : ILogger, IDisposable
	{
		/// <summary>
		/// The stream to write to
		/// </summary>
		protected StreamWriter m_stream;
		/// <summary>
		/// The lock used to provide exclusive access to the stream
		/// </summary>
		protected readonly object m_lock = new object();
		/// <summary>
		/// <c>True</c>if the stream should be closed when the logger is disposed
		/// </summary>
		protected readonly bool m_closeOnDispose;
		/// <summary>
		/// <c>True</c> if the logging should be in combined format, <c>false</c> otherwise
		/// </summary>
		protected readonly bool m_useCombinedFormat;
		/// <summary>
		/// <c>True</c> if the logging should contain cookies in the combined format, <c>false</c> otherwise
		/// </summary>
		protected readonly bool m_logCookies;
		/// <summary>
		/// The log format string
		/// </summary>
		protected readonly string m_logFormatString;

		/// <summary>
		/// Cached instance of the timezone for use in the log output
		/// </summary>
		private static readonly string TIME_ZONE_SPECIFIER = new DateTime().ToString("zzz").Replace(":", "");


		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.Logging.CLFLogger"/> class.
		/// </summary>
		/// <param name="filename">The file to write log data into.</param>
		/// <param name="useCombinedFormat"><c>True</c> if the logging should be in combined format, <c>false</c> otherwise.</param>
		/// <param name="logCookies"><c>True</c> if the logging should contain cookies in the combined format, <c>false</c> otherwise.</param>
		public CLFLogger(string filename, bool useCombinedFormat = true, bool logCookies = false)
			: this(File.Open(filename, FileMode.Append, FileAccess.Write, FileShare.Read), useCombinedFormat, logCookies, true)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.Logging.CLFLogger"/> class.
		/// </summary>
		/// <param name="destination">The stream to write to.</param>
		/// <param name="useCombinedFormat"><c>True</c> if the logging should be in combined format, <c>false</c> otherwise.</param>
		/// <param name="logCookies"><c>True</c> if the logging should contain cookies in the combined format, <c>false</c> otherwise.</param>
		/// <param name="closeOnDispose"><c>True</c>if the stream should be closed when the logger is disposed.</param>
		public CLFLogger(Stream destination, bool useCombinedFormat = true, bool logCookies = false, bool closeOnDispose = false)
		{
			m_stream = new StreamWriter(destination);
			m_closeOnDispose = closeOnDispose;
			m_useCombinedFormat = useCombinedFormat;
			m_logCookies = logCookies;

			var logstr = m_useCombinedFormat ? "{0} {1} {2} {3} \"{4} {5} {6}\" {7} {8} \"{9}\" \"{10}\"" : "{0} {1} {2} {3} \"{4} {5} {6}\" {7} {8}";
			if (m_useCombinedFormat && m_logCookies)
				logstr += " \"{11}\"";

			m_logFormatString = logstr;

		}

		/// <summary>
		/// Gets the log line in the combined log format.
		/// </summary>
		/// <returns>The combined log line.</returns>
		/// <param name="request">The request.</param>
		/// <param name="response">The response.</param>
		/// <param name="ex">The exception.</param>
		/// <param name="started">Timestamp for when the request started.</param>
		/// <param name="duration">Duration of the request processing.</param>
		public string GetCombinedLogLine(HttpRequest request, HttpResponse response, Exception ex, DateTime started, TimeSpan duration)
		{
			string remoteAddr;

			if (request.RemoteEndPoint is IPEndPoint)
				remoteAddr = ((IPEndPoint)request.RemoteEndPoint).Address.ToString();
			else
				remoteAddr = request.RemoteEndPoint.ToString();

			string referer = null;
			string cookies = null;
			string useragent = null;

			if (m_useCombinedFormat)
			{
				referer = request.Headers["Referer"];
				useragent = request.Headers["User-Agent"];

				if (string.IsNullOrWhiteSpace(referer))
					referer = "-";
				if (string.IsNullOrWhiteSpace(useragent))
					useragent = "-";

				if (m_logCookies)
				{
					cookies = request.Headers["Cookie"];
					if (string.IsNullOrWhiteSpace(cookies))
						cookies = "-";
				}

			}

			var statuscode = response == null ? HttpStatusCode.InternalServerError : response.StatusCode;
			var streamlength = response == null ? -1 : response.GetResponseStream().Length;

			return string.Format(
				m_logFormatString,
				remoteAddr,
				"-",
				"-",
				string.Format("[{0} {1}]", started.ToString("dd/MMM/yyyy:HH:mm:ss", CultureInfo.InvariantCulture), TIME_ZONE_SPECIFIER),
				request.Method, request.Path + request.RawQueryString ?? string.Empty, request.HttpVersion,
				(int)statuscode,
				streamlength,
				referer,
				useragent,
				cookies
			);
		}

		/// <summary>
		/// Dispose the specified isDisposing.
		/// </summary>
		/// <param name="isDisposing">If set to <c>true</c> is disposing.</param>
		protected virtual void Dispose(bool isDisposing)
		{
			if (m_closeOnDispose && m_stream != null)
				try {m_stream.Dispose(); }

			catch { }
			finally { m_stream = null; }
		}

		#region IDisposable implementation

		/// <summary>
		/// Releases all resource used by the <see cref="Ceenhttpd.Logging.CLFLogger"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Ceenhttpd.Logging.CLFLogger"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Ceenhttpd.Logging.CLFLogger"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the <see cref="Ceenhttpd.Logging.CLFLogger"/> so
		/// the garbage collector can reclaim the memory that the <see cref="Ceenhttpd.Logging.CLFLogger"/> was occupying.</remarks>
		public void Dispose()
		{
			Dispose(true);
		}

		#endregion

		#region ILogger implementation

		/// <summary>
		/// Logs the request to the stream.
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="request">The request.</param>
		/// <param name="response">The response.</param>
		/// <param name="ex">The exception.</param>
		/// <param name="started">Timestamp for when the request started.</param>
		/// <param name="duration">Duration of the request processing.</param>
		public virtual Task LogRequest(HttpRequest request, HttpResponse response, Exception ex, DateTime started, TimeSpan duration)
		{
			return Task.Run(() => {
				if (m_stream == null)
					throw new ObjectDisposedException(this.GetType().FullName);

				var logmsg = GetCombinedLogLine(request, response, ex, started, duration);

				lock(m_lock)
					m_stream.WriteLine(logmsg);
			});
		}

		#endregion
	}
}

