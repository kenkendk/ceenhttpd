using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Handler module that is the target of a login form, or login API call
	/// </summary>
	public class LoginHandler : LoginSettingsModule, IHttpModule
	{
		/// <summary>
		/// Gets or sets a value indicating whether a XSRF token is required.
		/// </summary>
		public bool RequireXSRFToken { get; set; } = true;

		/// <summary>
		/// Gets or sets the name of the form item that contains the username.
		/// </summary>
		public string UsernameFormElement { get; set; } = "username";
		/// <summary>
		/// Gets or sets the name of the form item that contains the password.
		/// </summary>
		public string PasswordFormElement { get; set; } = "password";
		/// <summary>
		/// Gets or sets the name of the form item that contains the password.
		/// </summary>
		public string RememberMeFormElement { get; set; } = "remember";

		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			if (context.Request.Method != "POST")
				return context.SetResponseMethodNotAllowed();

			var xsrf = context.Request.Headers[XSRFHeaderName];

			if (RequireXSRFToken && string.IsNullOrWhiteSpace(xsrf))
				return SetXSRFError(context);

			var username = ExtractUsername(context);
			var password = ExtractPassword(context);
			var rememberme = ExtractRememberMe(context);

			if (string.IsNullOrWhiteSpace(username))
				return SetLoginError(context);
			
			if (string.IsNullOrWhiteSpace(password))
				return SetLoginError(context);

			if (RequireXSRFToken)
			{
				var session = await ShortTermStorage.GetSessionFromXSRFAsync(xsrf);
				if (Utility.IsNullOrExpired(session))
					return SetXSRFError(context);
			}

			if (UseLongTermCookieStorage && LongTermStorage != null)
			{
				if (await PerformLongTermLogin(context))
					return true;

				// Check for a Hijack response
				if (context.Response.HasSentHeaders)
					return true;
			}

			var user = 
				(await Authentication.GetLoginEntriesAsync(username))
					.FirstOrDefault(x => x != null && PBKDF2.ComparePassword(password, x.Token));

			if (user == null)
			{
				if (await LoginWithBasicAuth(context))
					return true;
				
				return SetLoginError(context);
			}

			await PerformLoginAsync(context, user.UserID, null, rememberme);

			return true;
		}

		/// <summary>
		/// Extracts the username from the request.
		/// </summary>
		/// <returns>The username.</returns>
		/// <param name="context">The http context.</param>
		protected virtual string ExtractUsername(IHttpContext context)
		{
			return context.Request.Form[UsernameFormElement];
		}

		/// <summary>
		/// Extracts the password from the request.
		/// </summary>
		/// <returns>The password.</returns>
		/// <param name="context">The http context.</param>
		protected virtual string ExtractPassword(IHttpContext context)
		{
			return context.Request.Form[PasswordFormElement];
		}

		/// <summary>
		/// Extracts the password from the request.
		/// </summary>
		/// <returns>The password.</returns>
		/// <param name="context">The http context.</param>
		protected virtual bool ExtractRememberMe(IHttpContext context)
		{
			if (!context.Request.Form.ContainsKey(RememberMeFormElement))
				return false;

			return ! new[] { "0", "false", "no", "off" }.Contains(context.Request.Form[RememberMeFormElement], StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Creates a user entry for storing in a storage module
		/// </summary>
		/// <returns>The user to create.</returns>
		/// <param name="userid">The user ID.</param>
		/// <param name="username">The username.</param>
		/// <param name="password">The password.</param>
		public static LoginEntry CreateUser(string userid, string username, string password)
		{
			return new LoginEntry()
			{
				UserID = userid,
				Username = username,
				Token = PBKDF2.CreatePBKDF2(password)
			};
		}
	}
}
