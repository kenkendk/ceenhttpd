using System;
using System.Threading.Tasks;
using SyslogNet.Client;
using SyslogNet.Client.Serialization;
using SyslogNet.Client.Transport;
using System.IO;
using Ceen.Common;

namespace Ceen.Httpd.Cli
{
	public class SyslogLogger : Ceen.Httpd.Logging.CLFLogger
	{
		private readonly ISyslogMessageSerializer m_serializer = new SyslogLocalMessageSerializer();
		private readonly ISyslogMessageSender m_sender = new SyslogLocalSender();

		public SyslogLogger()
			: base(new MemoryStream(), true, false)
		{
			m_stream.Dispose();
			m_stream = null;
		}

		public override Task LogRequest(IHttpContext context, Exception ex, DateTime started, TimeSpan duration)
		{
			return Task.Run(() =>
				{
					m_sender.Send(
						new SyslogMessage(Facility.SystemDaemons, Severity.Informational, "ceenhttpd", GetCombinedLogLine(context, ex, started, duration)),
						m_serializer
					);
				});
		}
	}
}

