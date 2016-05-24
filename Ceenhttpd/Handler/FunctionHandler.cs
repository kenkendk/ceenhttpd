using System;
using System.Threading.Tasks;

namespace Ceenhttpd.Handler
{
	public class FunctionHandler : IHttpModule
	{
		private readonly HttpHandlerDelegate m_handler;
		public FunctionHandler(HttpHandlerDelegate handler)
		{
			m_handler = handler;			
		}

		#region IHttpModule implementation

		public Task<bool> HandleAsync(HttpRequest request, HttpResponse response)
		{
			return m_handler(request, response);
		}

		#endregion

		public static implicit operator FunctionHandler(HttpHandlerDelegate handler)
		{
			return new FunctionHandler(handler);
		}
	}
}

