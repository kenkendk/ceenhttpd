using System;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// This module checks for valid logins, and verifies the XSRF token too
	/// </summary>
	public class LoginRequiredHandler : LoginSettingsModule, IHttpModule
	{
		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="T:Ceen.Httpd.Handler.Login.LoginRequiredHandler"/> will check the credentials, even if the UserID is already established.
		/// </summary>
		public bool ForceCheck { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="T:Ceen.Httpd.Handler.Login.LoginRequiredHandler"/> will check
		/// the XSRF token. If the LoginSettingsModule.UseXSRFToken is false, this setting has no effect.
		/// </summary>
		public bool CheckXSRFToken { get; set; } = true;

		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			if (!ForceCheck && !string.IsNullOrWhiteSpace(context.Request.UserID))
				return false;
			
			var xsrf = context.Request.Headers[XSRFHeaderName];
			var cookie = context.Request.Cookies[AuthSessionCookieName];

			if (UseXSRFTokens && CheckXSRFToken && string.IsNullOrWhiteSpace(xsrf))
				return SetXSRFError(context);

			if (UseXSRFTokens && CheckXSRFToken)
			{
				var session = await ShortTermStorage.GetSessionFromXSRFAsync(xsrf);
				if (session == null || session.Expires > DateTime.Now)
					return SetXSRFError(context);

				if (string.IsNullOrWhiteSpace(cookie) || session.Cookie != cookie)
				{
					if (await LoginWithBasicAuth(context))
						return false;

					if (await PerformLongTermLogin(context))
						return false;

					// Check for a Hijack response
					if (context.Response.HasSentHeaders)
						return true;

					return SetLoginError(context);
				}

				await RefreshSessionTokensAsync(context, session);
			}
			else
			{
				SessionRecord session = null;

				if (!string.IsNullOrWhiteSpace(cookie))
					session = await ShortTermStorage.GetSessionFromCookieAsync(cookie);

				if (session == null || DateTime.Now > session.Expires)
				{
					if (await LoginWithBasicAuth(context))
						return false;
					
					if (await PerformLongTermLogin(context))
						return false;

					// Check for a Hijack response
					if (context.Response.HasSentHeaders)
						return true;
					
					return SetLoginError(context);
				}

				await RefreshSessionTokensAsync(context, session);
			}

			return false;
		}
	}
}
