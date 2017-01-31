using System;
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
			var token = new SessionRecord()
			{
				XSRFToken = PRNG.GetRandomString(32),
				Expires = DateTime.Now.AddSeconds(ShortTermExpirationSeconds)
			};

			await ShortTermStorage.AddSessionAsync(token);

			// If the connection is using SSL, require SSL for the cookie
			var usingssl = context.Request.SslProtocol != System.Security.Authentication.SslProtocols.None;
			context.Response.AddCookie(XSRFCookieName, token.XSRFToken, expires: token.Expires, httponly: false, secure: usingssl);

			return false;
		}
	}
}
