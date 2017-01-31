using System;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Class that performs a logout action, and can be useds a redirect target or an API target
	/// </summary>
	public class LogoutHandler : LoginSettingsModule, IHttpModule
	{
		/// <summary>
		/// Gets or sets the url to redirect responses to after logout.
		/// Set this to an empty string if the status code is not a redirect.
		/// </summary>
		public string RedirectUrl { get; set; } = "/";
		/// <summary>
		/// Gets or sets the status code reported after logout.
		/// </summary>
		public int ResultStatusCode { get; set; } = 302;
		/// <summary>
		/// Gets or sets the status message reported after logout.
		/// </summary>
		public string ResultStatusMessage { get; set; } = "Found";

		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			var xsrf = context.Request.Headers[XSRFHeaderName];
			var cookie = context.Request.Cookies[AuthSessionCookieName];
			var longterm = context.Request.Cookies[AuthCookieName];

			if (!string.IsNullOrWhiteSpace(xsrf))
			{
				var session = await ShortTermStorage.GetSessionFromXSRFAsync(xsrf);
				if (session != null)
				{
					await ShortTermStorage.DropSessionAsync(session);
					if (!string.IsNullOrWhiteSpace(cookie))
						context.Response.AddCookie(AuthSessionCookieName, "", expires: new DateTime(1970, 1, 1), maxage: 0);

					cookie = null;
				}

				context.Response.AddCookie(XSRFCookieName, "", expires: new DateTime(1970, 1, 1), maxage: 0);
			}

			if (!string.IsNullOrWhiteSpace(cookie))
			{
				var session = await ShortTermStorage.GetSessionFromXSRFAsync(cookie);
				if (session != null)
					await ShortTermStorage.DropSessionAsync(session);
			}

			if (!string.IsNullOrWhiteSpace(longterm))
			{
				if (LongTermStorage != null)
				{
					var pbkdf2 = new LongTermCookie(longterm);
					if (pbkdf2.IsValid)
					{
						var lts = await LongTermStorage.GetLongTermLoginAsync(pbkdf2.Series);
						if (lts != null)
							await LongTermStorage.DropLongTermLoginAsync(lts);
					}
				}

				context.Response.AddCookie(AuthCookieName, "", expires: new DateTime(1970, 1, 1), maxage: 0);
			}

			context.Response.StatusCode = (HttpStatusCode)ResultStatusCode;
			context.Response.StatusMessage = ResultStatusMessage;
			if (!string.IsNullOrWhiteSpace(RedirectUrl))
				context.Response.Headers["Location"] = RedirectUrl;

			return true;
		}
	}
}
