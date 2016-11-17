using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ceen.Httpd.Handler
{
	/// <summary>
	/// Module for handling XSRF authentication
	/// </summary>
	public class VerifyXSRFHandler : IHttpModule
	{
		/// <summary>
		/// Gets or sets the name of the header to look for.
		/// </summary>
		public string HeaderName { get; set; } = "X-XSRF-Token";
		/// <summary>
		/// Gets or sets the status code reported if the XSRF token is missing or invalid.
		/// </summary>
		public int ErrorStatusCode { get; set; } = 403;
		/// <summary>
		/// Gets or sets the status message reported if the XSRF token is missing or invalid.
		/// </summary>
		public string ErrorStatusMessage { get; set; } = "Expired or missing XSRF token";

		/// <summary>
		/// The storage element
		/// </summary>
		protected IStorageEntry m_storage;

		/// <summary>
		/// The lock guarding the storage
		/// </summary>
		protected AsyncLock m_lock;

		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public virtual async Task<bool> HandleAsync(IHttpContext context)
		{
			if (m_storage == null)
				m_storage = await context.Storage.GetStorageAsync(SetXSRFCookieHandler.STORAGE_MODULE_NAME, string.Empty, -1, true);

			if (m_lock == null)
				m_lock = SetXSRFCookieHandler.GetLockForStorage(m_storage);

			var token = context.Request.Headers[HeaderName];
			if (string.IsNullOrWhiteSpace(token))
				return ReportExpired(context);

			var exp = m_storage[token];
			if (string.IsNullOrWhiteSpace(exp))
				return ReportExpired(context);

			if (SetXSRFCookieHandler.IsExpired(exp))
			{
				using (await m_lock.LockAsync())
					m_storage.Remove(token);
				return ReportExpired(context);
			}

			// Refresh expiration
			var newduration = SetXSRFCookieHandler.RenewToken(exp);
			if (string.IsNullOrWhiteSpace(newduration))
				return ReportExpired(context);
			
			using (await m_lock.LockAsync())
				m_storage[token] = newduration;
			
			return false;
		}

		/// <summary>
		/// Reports the XSRF token as expired.
		/// </summary>
		/// <returns>An value indicating if the request is blocked.</returns>
		/// <param name="context">The http context.</param>
		protected bool ReportExpired(IHttpContext context)
		{
			context.Response.SetNonCacheable();
			context.Response.StatusCode = (HttpStatusCode)ErrorStatusCode;
			context.Response.StatusMessage = ErrorStatusMessage;
			return true;
		}
	}
}
