using System;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// This module requires an XSRF token to be present on all requests.
	/// This module does not require the user to be logged in.
	/// If you require logins, use the LoginRequiredHandler instead.
	/// </summary>
	public class XSRFTokenRequiredHandler : LoginSettingsModule, IHttpModule
	{
		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			var xsrf = context.Request.Headers[XSRFHeaderName];

			if (string.IsNullOrWhiteSpace(xsrf))
				return SetXSRFError(context);

			var session = await ShortTermStorage.GetSessionFromXSRFAsync(xsrf);
			if (session == null || session.Expires > DateTime.Now)
				return SetXSRFError(context);

			session.Expires = DateTime.Now.AddSeconds(ShortTermExpirationSeconds);
			await ShortTermStorage.UpdateSessionExpirationAsync(session);

			return false;
		}
	}
}
