using System;
using System.Threading.Tasks;
using SyslogNet.Client;
using SyslogNet.Client.Serialization;
using SyslogNet.Client.Transport;
using System.IO;

namespace Ceenhttpdcli
{
	public class SyslogLogger : Ceenhttpd.Logging.CLFLogger
	{
		private readonly ISyslogMessageSerializer m_serializer = new SyslogLocalMessageSerializer();
		private readonly ISyslogMessageSender m_sender = new SyslogLocalSender();

		public SyslogLogger()
			: base(new MemoryStream(), true, false)
		{
			m_stream.Dispose();
			m_stream = null;
		}

		public override Task LogRequest(Ceenhttpd.HttpRequest request, Ceenhttpd.HttpResponse response, Exception ex, DateTime started, TimeSpan duration)
		{
			return Task.Run(() =>
				{
					m_sender.Send(
						new SyslogMessage(Facility.SystemDaemons, Severity.Informational, "ceenhttpd", GetCombinedLogLine(request, response, ex, started, duration)),
						m_serializer
					);
				});
		}
	}
}

