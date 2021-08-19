using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// A module for setting login related options in a common place
	/// </summary>
	public class LoginSettingsModule : DerivedSettings<LoginSettingsModule>, IModule
	{
		/// <summary>
		/// Initializes the <see cref="T:Ceen.Httpd.Handler.Login.LoginSettings"/> class and sets the default values
		/// </summary>
		static LoginSettingsModule()
		{
			_basesettings[nameof(XSRFErrorStatusCode)] = 403;
			_basesettings[nameof(XSRFErrorStatusMessage)] = "XSRF token invalid or missing";
			_basesettings[nameof(XSRFErrorRedirectUrl)] = null;

			_basesettings[nameof(HijackErrorStatusCode)] = 403;
			_basesettings[nameof(HijackErrorStatusMessage)] = "Login token hijacking detected, someone else has used your identity";
			_basesettings[nameof(HijackErrorRedirectUrl)] = null;

			_basesettings[nameof(LoginErrorStatusCode)] = 403;
			_basesettings[nameof(LoginErrorStatusMessage)] = "Not logged in";
			_basesettings[nameof(LoginErrorRedirectUrl)] = null;

			_basesettings[nameof(LongTermDurationSeconds)] = (int)TimeSpan.FromDays(60).TotalSeconds;

			_basesettings[nameof(LoginSuccessStatusCode)] = 200;
			_basesettings[nameof(LoginSuccessStatusMessage)] = "OK";
			_basesettings[nameof(LoginSuccessRedirectUrl)] = null;

			_basesettings[nameof(ShortTermExpirationSeconds)] = (int)TimeSpan.FromMinutes(15).TotalSeconds;
			_basesettings[nameof(ShortTermRefreshThreshold)] = (int)TimeSpan.FromMinutes(14).TotalSeconds;

			_basesettings[nameof(UseLongTermCookieStorage)] = true;
			_basesettings[nameof(UseXSRFTokens)] = true;
			_basesettings[nameof(XSRFHeaderName)] = "X-XSRF-Token";
			_basesettings[nameof(XSRFCookieName)] = "xsrf-token";

			_basesettings[nameof(AuthSessionCookieName)] = "ceen-auth-session-token";
			_basesettings[nameof(AuthCookieName)] = "ceen-auth-token";

			_basesettings[nameof(CookiePath)] = "/";

			_basesettings[nameof(AllowBasicAuth)] = true;

			_basesettings[nameof(AuthSessionCookieSameSite)] = "Strict";
			_basesettings[nameof(XSRFCookieSameSite)] = "Strict";

			_basesettings[nameof(ShortTermStorage)] = null;
			_basesettings[nameof(LongTermStorage)] = null;
			_basesettings[nameof(Authentication)] = null;			
		}

		/// <summary>
		/// Gets or sets the status code reported if the XSRF token is invalid or missing.
		/// </summary>
		public int XSRFErrorStatusCode { get { return GetValue<int>(); } set { SetValue(value); } }
		/// <summary>
		/// Gets or sets the status message reported if the XSRF token is invalid or missing.
		/// </summary>
		public string XSRFErrorStatusMessage { get { return GetValue<string>(); } set { SetValue(value); } }
		/// <summary>
		/// Gets or sets the status code reported if the XSRF token is invalid or missing.
		/// </summary>
		public string XSRFErrorRedirectUrl { get { return GetValue<string>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the status code reported if the XSRF token is invalid or missing.
		/// </summary>
		public int HijackErrorStatusCode { get { return GetValue<int>(); } set { SetValue(value); } }
		/// <summary>
		/// Gets or sets the status message reported if the XSRF token is invalid or missing.
		/// </summary>
		public string HijackErrorStatusMessage { get { return GetValue<string>(); } set { SetValue(value); } }
		/// <summary>
		/// Gets or sets the status code reported if the XSRF token is invalid or missing.
		/// </summary>
		public string HijackErrorRedirectUrl { get { return GetValue<string>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the status code reported if the login is invalid and a redirect is not performed.
		/// </summary>
		public int LoginErrorStatusCode { get { return GetValue<int>(); } set { SetValue(value); } }
		/// <summary>
		/// Gets or sets the status message reported if login is invalid and a redirect is not performed.
		/// </summary>
		public string LoginErrorStatusMessage { get { return GetValue<string>(); } set { SetValue(value); } }
		/// <summary>
		/// Gets or sets the url to redirect responses to if not authenticated.
		/// If this is set to a non-empty string, responses are redirected,
		/// instead of reporting the error code
		/// </summary>
		public string LoginErrorRedirectUrl { get { return GetValue<string>(); } set { SetValue(value); } }


		/// <summary>
		/// Gets or sets the number of seconds a long-term token is valid.
		/// </summary>
		public int LongTermDurationSeconds { get { return GetValue<int>(); } set { SetValue(value); UpdateExpiration(); } }

		/// <summary>
		/// Gets or sets the url to redirect responses to if not authenticated.
		/// If this is set to a non-empty string, responses are redirected,
		/// instead of reporting the error code
		/// </summary>
		public string LoginSuccessRedirectUrl { get { return GetValue<string>(); } set { SetValue(value); } }
		/// <summary>
		/// Gets or sets the status code reported if the login succeeds and a redirect is not performed.
		/// </summary>
		public int LoginSuccessStatusCode { get { return GetValue<int>(); } set { SetValue(value); } }
		/// <summary>
		/// Gets or sets the status message reported if login succeeds and a redirect is not performed.
		/// </summary>
		public string LoginSuccessStatusMessage { get { return GetValue<string>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the number of seconds a token is valid.
		/// </summary>
		public int ShortTermExpirationSeconds { get { return GetValue<int>(); } set { SetValue(value); UpdateExpiration(); } }

		/// <summary>
		/// Gets or sets the number of seconds a token can have left without being renewed.
		/// </summary>
		public int ShortTermRefreshThreshold { get { return GetValue<int>(); } set { SetValue(value); UpdateExpiration(); } }

		/// <summary>
		/// Gets or sets a value indicating if credentials passed as Basic HTTP auth are accepted
		/// </summary>
		/// <value><c>true</c> if allow basic auth; otherwise, <c>false</c>.</value>
		public bool AllowBasicAuth { get { return GetValue<bool>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets a value indicating whether a valid logged in user is required.
		/// </summary>
		public bool UseXSRFTokens { get { return GetValue<bool>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets a value indicating if XSRF tokens are required.
		/// </summary>
		public bool UseLongTermCookieStorage { get { return GetValue<bool>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the name of the XSRF header to look for.
		/// </summary>
		public string XSRFHeaderName { get { return GetValue<string>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the name of the authentication cookie to look for.
		/// </summary>
		public string AuthSessionCookieName { get { return GetValue<string>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the name of the authentication cookie to look for.
		/// </summary>
		public string AuthCookieName { get { return GetValue<string>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the name of the authentication cookie to look for.
		/// </summary>
		public string XSRFCookieName { get { return GetValue<string>(); } set { SetValue(value); } }

        /// <summary>
        /// Gets or sets the value for the cookie &quot;samesite&quot; attribute.
        /// The default is &quot;Strict&quot; meaning that the cookie will not be shared with other sites.
        /// </summary>
        public string AuthSessionCookieSameSite { get { return GetValue<string>(); } set { SetValue(value); } }

        /// <summary>
        /// Gets or sets the value for the cookie &quot;samesite&quot; attribute.
        /// The default is &quot;Strict&quot; meaning that the cookie will not be shared with other sites.
        /// </summary>
        public string XSRFCookieSameSite { get { return GetValue<string>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the path component for the cookies.
		/// </summary>
		public string CookiePath { get { return GetValue<string>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the storage implementation for short term items.
		/// </summary>
		public Login.IShortTermStorageModule ShortTermStorage { get { return GetValue<Login.IShortTermStorageModule>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the storage implementation for short term items.
		/// </summary>
		public Login.ILongTermStorageModule LongTermStorage { get { return GetValue<Login.ILongTermStorageModule>(); } set { SetValue(value); } }

		/// <summary>
		/// Gets or sets the authentication implementation.
		/// </summary>
		public Login.ILoginEntryModule Authentication { get { return GetValue<Login.ILoginEntryModule>(); } set { SetValue(value); } }

		/// <summary>
		/// The expiration handler
		/// </summary>
		protected static readonly PeriodicTask _expireHandler = new PeriodicTask(ExpireItems, TimeSpan.FromSeconds(10));

		/// <summary>
		/// The list of active subclasses
		/// </summary>
		protected static readonly List<LoginSettingsModule> _active = new List<LoginSettingsModule>();

		/// <summary>
		/// A lock to guard access to the _active list
		/// </summary>
		protected static AsyncLock _active_lock = new AsyncLock();

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Handler.Login.LoginSettingsModule"/> class.
		/// </summary>
		public LoginSettingsModule()
		{
			Task.Run(async () => {
				using (await _active_lock.LockAsync())
					_active.Add(this);
			});
		}

		/// <summary>
		/// Sets the login success on the response.
		/// </summary>
		/// <returns><c>true</c>, if login success was set, <c>false</c> otherwise.</returns>
		/// <param name="context">The http context.</param>
		protected virtual bool SetLoginSuccess(IHttpContext context)
		{
			context.Response.StatusCode = (HttpStatusCode)LoginSuccessStatusCode;
			context.Response.StatusMessage = LoginSuccessStatusMessage;
			if (!string.IsNullOrWhiteSpace(LoginSuccessRedirectUrl))
				context.Response.Headers["Location"] = LoginSuccessRedirectUrl;
			
			return true;
		}

		/// <summary>
		/// Sets the login error on the response.
		/// </summary>
		/// <returns><c>true</c>, if login error was set, <c>false</c> otherwise.</returns>
		/// <param name="context">The http context.</param>
		protected virtual bool SetLoginError(IHttpContext context)
		{
			context.Response.StatusCode = (HttpStatusCode)LoginErrorStatusCode;
			context.Response.StatusMessage = LoginErrorStatusMessage;
			if (!string.IsNullOrWhiteSpace(LoginErrorRedirectUrl))
				context.Response.Headers["Location"] = LoginErrorRedirectUrl;
			
			return true;
		}

		/// <summary>
		/// Sets the XSRF error on the response.
		/// </summary>
		/// <returns><c>true</c>, if XSRF error was set, <c>false</c> otherwise.</returns>
		/// <param name="context">The http context.</param>
		protected virtual bool SetXSRFError(IHttpContext context)
		{
			context.Response.StatusCode = (HttpStatusCode)XSRFErrorStatusCode;
			context.Response.StatusMessage = XSRFErrorStatusMessage;
			if (!string.IsNullOrWhiteSpace(XSRFErrorRedirectUrl))
				context.Response.Headers["Location"] = XSRFErrorRedirectUrl;
			return true;
		}

		/// <summary>
		/// Sets the hijack error on the response.
		/// </summary>
		/// <returns><c>true</c>, if Hijack error was set, <c>false</c> otherwise.</returns>
		/// <param name="context">The http context.</param>
		protected virtual bool SetHijackError(IHttpContext context)
		{
			context.Response.StatusCode = (HttpStatusCode)HijackErrorStatusCode;
			context.Response.StatusMessage = HijackErrorStatusMessage;
			if (!string.IsNullOrWhiteSpace(HijackErrorRedirectUrl))
				context.Response.Headers["Location"] = HijackErrorRedirectUrl;

			return true;
		}

		/// <summary>
		/// Emits the current session tokens with an updated expiration time
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="context">The http context.</param>
		/// <param name="session">The current session record.</param>
		protected virtual async Task RefreshSessionTokensAsync(IHttpContext context, SessionRecord session)
		{
			if (session == null)
				throw new ArgumentNullException(nameof(session));

			// Renew if the token is starting to get old
			if ((session.Expires - DateTime.Now).TotalSeconds < ShortTermRefreshThreshold)
			{
				// If the connection is using SSL, require SSL for the cookie
				var usingssl = context.Request.SslProtocol != System.Security.Authentication.SslProtocols.None;

				session.Expires = DateTime.Now.AddSeconds(ShortTermExpirationSeconds);
				await ShortTermStorage.UpdateSessionExpirationAsync(session);

				if (!string.IsNullOrWhiteSpace(session.XSRFToken))
					context.Response.AddCookie(XSRFCookieName, session.XSRFToken, expires: session.Expires, httponly: false, path: CookiePath, secure: usingssl, samesite: XSRFCookieSameSite);
				
				if (!string.IsNullOrWhiteSpace(session.Cookie))
					context.Response.AddCookie(AuthSessionCookieName, session.Cookie, expires: session.Expires, httponly: true, path: CookiePath, secure: usingssl, samesite: AuthSessionCookieSameSite);
			}
		}

		/// <summary>
		/// Performs all steps required to do a login
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="context">The http context.</param>
		/// <param name="userid">The user ID.</param>
		/// <param name="series">The long-term series</param>
		/// <param name="withlongterm">A value indicating if a long-term session should be created</param>
		protected virtual async Task PerformLoginAsync(IHttpContext context, string userid, string series, bool withlongterm)
		{
			var session = new SessionRecord();

			// Re-use the XSRF if possible
			if (UseXSRFTokens)
			{
				var xsrf = context.Request.Headers[XSRFHeaderName];
				if (!string.IsNullOrWhiteSpace(xsrf))
				{
					var prev = await ShortTermStorage.GetSessionFromXSRFAsync(xsrf);
					if (prev != null)
					{
                        // Remove the previous entry to avoid conflicts
                        await ShortTermStorage.DropSessionAsync(prev);
						
						// Re-use the XSRF token
						session.XSRFToken = prev.XSRFToken;
                    }
				}
			}

			session.UserID = userid;
			session.Expires = DateTime.Now.AddSeconds(ShortTermExpirationSeconds);

			// If the connection is using SSL, require SSL for the cookie
			var usingssl = context.Request.SslProtocol != System.Security.Authentication.SslProtocols.None;

			if (UseXSRFTokens)
			{
				session.XSRFToken = session.XSRFToken ?? PRNG.GetRandomString(32);
				context.Response.AddCookie(XSRFCookieName, session.XSRFToken, expires: session.Expires, httponly: false, path: CookiePath, secure: usingssl, samesite: XSRFCookieSameSite);
			}

			if (UseLongTermCookieStorage && LongTermStorage != null && (!string.IsNullOrWhiteSpace(series) || withlongterm))
			{
				var cookie = new LongTermCookie();
				if (!string.IsNullOrWhiteSpace(series))
					cookie.Series = series;
				
				var st = new LongTermToken()
				{
					UserID = userid,
					Expires = DateTime.Now.AddSeconds(LongTermDurationSeconds),
					Series = cookie.Series,
					Token = PBKDF2.CreatePBKDF2(cookie.Token)
				};

				await LongTermStorage.AddOrUpdateLongTermLoginAsync(st);
				context.Response.AddCookie(AuthCookieName, cookie.ToString(), expires: st.Expires, httponly: true, path: CookiePath, secure: usingssl, samesite: AuthSessionCookieSameSite);
			}

			session.Cookie = PRNG.GetRandomString(32);
			context.Response.AddCookie(AuthSessionCookieName, session.Cookie, expires: session.Expires, httponly: true, path: CookiePath, secure: usingssl, samesite: AuthSessionCookieSameSite);

			if (ShortTermStorage == null)
				Console.WriteLine("Missing short term storage module, make sure you load Ceen.Security.Login.DatabaseStorageModule or manually set a storage module");
			await ShortTermStorage.AddSessionAsync(session);

			SetLoginSuccess(context);

			context.Request.UserID = userid;
		}

		/// <summary>
		/// Checks if the request carries a valid long-term cookie, and replaces it with a new valid cookie.
		/// If a hijack is performed, the response headers will be flushed.
		/// Callers should check the flush status after calling this method.
		/// </summary>
		/// <returns><c>True</c> if the long-term cookie is valid, <c>false</c> otherwise.</returns>
		/// <param name="context">The http context.</param>
		protected virtual async Task<bool> PerformLongTermLogin(IHttpContext context)
		{
			if (!UseLongTermCookieStorage || LongTermStorage == null)
				return false;

			var longterm = context.Request.Cookies[AuthCookieName];
			if (string.IsNullOrWhiteSpace(longterm))
				return false;

			var ltc = new LongTermCookie(longterm);
			if (!ltc.IsValid)
				return false;

			var lts = await LongTermStorage.GetLongTermLoginAsync(ltc.Series);
			if (Utility.IsNullOrExpired(lts))
				return false;

			if (!PBKDF2.ComparePassword(ltc.Token, lts.Token))
			{
				await LongTermStorage.DropAllLongTermLoginsAsync(lts.UserID, lts.Series);

				SetHijackError(context);
				await context.Response.FlushHeadersAsync();

				return false;
			}

			await PerformLoginAsync(context, lts.UserID, lts.Series, true);

			return true;
		}

		/// <summary>
		/// Updates the expiration interval for the periodic expiration handler.
		/// </summary>
		public void UpdateExpiration()
		{
			_expireHandler.Interval = TimeSpan.FromSeconds(Math.Min(ShortTermExpirationSeconds, LongTermDurationSeconds));
		}

		/// <summary>
		/// Calls the expiration handler, and starts it ahead of the cycle
		/// </summary>
		public void RequestExpiration()
		{
			_expireHandler.RunNow();
		}

		/// <summary>
		/// Removes items from the tables that are expired
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="requested">If set to <c>true</c>, the expiration was requested, if set to <c>false</c> the expiration was triggered by the timer.</param>
		protected static async Task ExpireItems(bool requested)
		{
			List<IShortTermStorageModule> shorttermtargets;
			List<ILongTermStorageModule> longtermtargets;

			using (await _active_lock.LockAsync())
			{
				shorttermtargets = _active.Select(x => x.ShortTermStorage).Where(x => x != null).Distinct().ToList();
				longtermtargets = _active.Select(x => x.LongTermStorage).Where(x => x != null).Distinct().ToList();
			}
			await Task.WhenAll(
				shorttermtargets.Select(x => x.ExpireOldItemsAsync())
				.Union(
					longtermtargets.Select(x => x.ExpireOldItemsAsync())
				)
			);
		}

		/// <summary>
		/// Attempts to use the HTTP basic auth header to perform a login
		/// </summary>
		/// <returns><c>True</c> if the login succeded, <c>false</c> otherwise.</returns>
		/// <param name="context">The http context.</param>
		protected virtual async Task<bool> LoginWithBasicAuth(IHttpContext context)
		{
			if (AllowBasicAuth && Authentication != null)
			{
				var authstring = context.Request.Headers["Authorization"];
				if (authstring != null && authstring.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(authstring.Substring("Basic ".Length)));
						if (parts != null)
						{
							var ix = parts.IndexOf(':');
							if (ix > 0)
							{
								var username = parts.Substring(0, ix);
								var password = parts.Substring(ix + 1);

								var user =
									(await Authentication.GetLoginEntriesAsync(username))
									.FirstOrDefault(x => x != null && PBKDF2.ComparePassword(password, x.Token));

								if (user != null)
								{
									await PerformLoginAsync(context, user.UserID, null, false);
									return true;
								}
							}
						}
					}
					catch
					{
					}
				}
			}

			return false;
		}

	}
}
