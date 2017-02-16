using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// This module generates and XSRF token and sets it as a cookie.
	/// This module does not require the user to be logged in.
	/// If you require logins, use the LoginHandler instead.
	/// </summary>
	public class XSRFTokenGeneratorHandler : LoginSettingsModule, IHttpModule
	{
		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			var xsrf = context.Request.Headers[XSRFHeaderName] ?? context.Request.Cookies[XSRFCookieName];
			var cookie = context.Request.Cookies[AuthSessionCookieName];

			SessionRecord session = null;

			if (!string.IsNullOrWhiteSpace(xsrf))
			{
				session = await ShortTermStorage.GetSessionFromXSRFAsync(xsrf);
				if (Utility.IsNullOrExpired(session))
					session = null;
				else
					await RefreshSessionTokensAsync(context, session);
			}

			if (session == null && !string.IsNullOrWhiteSpace(cookie))
			{
				session = await ShortTermStorage.GetSessionFromCookieAsync(cookie);
				if (Utility.IsNullOrExpired(session))
					session = null;
				else
					await RefreshSessionTokensAsync(context, session);
			}

			// Check that we have not already set the XSRF cookie
			if (context.Response.Cookies.FirstOrDefault(x => x.Name == XSRFCookieName) == null)
			{
				if (session == null)
				{
					session = new SessionRecord()
					{
						XSRFToken = PRNG.GetRandomString(32),
						Expires = DateTime.Now.AddSeconds(ShortTermExpirationSeconds)
					};
					await ShortTermStorage.AddSessionAsync(session);
				}

				// If the connection is using SSL, require SSL for the cookie
				var usingssl = context.Request.SslProtocol != System.Security.Authentication.SslProtocols.None;

				context.Response.AddCookie(XSRFCookieName, session.XSRFToken, expires: session.Expires, httponly: false, secure: usingssl);
			}

			return false;
		}
	}
}
