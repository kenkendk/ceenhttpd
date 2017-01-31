using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Module that stores short-term information in memory.
	/// Note that a restart of the server will destroy all active sessions,
	/// if you are using this module.
	/// </summary>
	public class MemoryStorageModule : IHttpModule, IShortTermStorageModule
	{
		/// <summary>
		/// The name of the XSRF storage module
		/// </summary>
		public const string XSRF_MODULE_NAME = "xsfr-tokens";

		/// <summary>
		/// The name of the cookie storage module
		/// </summary>
		public const string COOKIE_MODULE_NAME = "cookie-tokens";

		/// <summary>
		/// The storage element
		/// </summary>
		protected IStorageEntry m_xsrf_storage;

		/// <summary>
		/// The storage element
		/// </summary>
		protected IStorageEntry m_cookie_storage;

		/// <summary>
		/// The lock
		/// </summary>
		private AsyncLock m_lock;

		/// <summary>
		/// Adds a new session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		public async Task AddSessionAsync(SessionRecord record)
		{
			var txt = PrimitiveSerializer.Serialize(record);
			using (await m_lock.LockAsync())
			{
				if (!string.IsNullOrWhiteSpace(record.XSRFToken))
				{
					if (m_xsrf_storage.ContainsKey(record.XSRFToken))
						throw new ArgumentException("Attempted to re-insert an entry into the XSRF token table");

					if (!string.IsNullOrWhiteSpace(record.Cookie) && m_cookie_storage.ContainsKey(record.Cookie))
						throw new ArgumentException("Attempted to re-insert an entry into the cookie token table");

					m_xsrf_storage[record.XSRFToken] = txt;
					m_cookie_storage[record.Cookie] = txt;
				}
				else
				{
					if (string.IsNullOrWhiteSpace(record.Cookie))
						throw new ArgumentException("Attempted to inser a record that has neither XSRF nor a cookie");

					if (m_cookie_storage.ContainsKey(record.Cookie))
						throw new ArgumentException("Attempted to re-insert an entry into the cookie token table");

					m_cookie_storage[record.Cookie] = txt;
				}
			}
		}

		/// <summary>
		/// Drops a session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to drop.</param>
		public async Task DropSessionAsync(SessionRecord record)
		{
			using (await m_lock.LockAsync())
			{
				if (!string.IsNullOrWhiteSpace(record.Cookie))
					m_cookie_storage.Remove(record.Cookie);
				if (!string.IsNullOrWhiteSpace(record.XSRFToken))
					m_xsrf_storage.Remove(record.XSRFToken);
			}
		}

		/// <summary>
		/// Called periodically to expire old items
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public async Task ExpireOldItemsAsync()
		{
			using (await m_lock.LockAsync())
			{
				var to_remove = new List<string>();

				foreach (var e in m_cookie_storage)
				{
					var ds = PrimitiveSerializer.Deserialize<SessionRecord>(e.Value);
					if (ds.Expires <= DateTime.Now)
					{
						to_remove.Add(e.Key);
						
						if (!string.IsNullOrWhiteSpace(ds.XSRFToken))
							m_xsrf_storage.Remove(ds.XSRFToken);
					}
				}

				foreach (var r in to_remove)
					m_cookie_storage.Remove(r);

				to_remove.Clear();

				foreach (var e in m_xsrf_storage)
				{
					var ds = PrimitiveSerializer.Deserialize<SessionRecord>(e.Value);
					if (ds.Expires <= DateTime.Now)
					{
						to_remove.Add(e.Key);

						if (!string.IsNullOrWhiteSpace(ds.Cookie))
							m_cookie_storage.Remove(ds.Cookie);
					}
				}

				foreach (var r in to_remove)
					m_xsrf_storage.Remove(r);
			}
		}

		/// <summary>
		/// Gets a session record from a cookie identifier
		/// </summary>
		/// <returns>The session record.</returns>
		/// <param name="cookie">The cookie identifier.</param>
		public async Task<SessionRecord> GetSessionFromCookieAsync(string cookie)
		{
			string txt;
			using (await m_lock.LockAsync())
				m_cookie_storage.TryGetValue(cookie, out txt);
			
			if (string.IsNullOrWhiteSpace(txt))
				return null;

			return PrimitiveSerializer.Deserialize<SessionRecord>(txt);
		}

		/// <summary>
		/// Gets a session record from an XSRF token
		/// </summary>
		/// <returns>The session record.</returns>
		/// <param name="xsrf">The XSRF token.</param>
		public async Task<SessionRecord> GetSessionFromXSRFAsync(string xsrf)
		{
			string txt;
			using (await m_lock.LockAsync())
				m_xsrf_storage.TryGetValue(xsrf, out txt);
			
			if (string.IsNullOrWhiteSpace(txt))
				return null;
			
			return PrimitiveSerializer.Deserialize<SessionRecord>(txt);
		}

		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			if (m_xsrf_storage == null)
				m_xsrf_storage = await context.Storage.GetStorageAsync(XSRF_MODULE_NAME, string.Empty, -1, true);
			if (m_cookie_storage == null)
				m_cookie_storage = await context.Storage.GetStorageAsync(COOKIE_MODULE_NAME, string.Empty, -1, true);
			
			return false;
		}

		/// <summary>
		/// Updates the expiration time on the given session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to update.</param>
		public async Task UpdateSessionExpirationAsync(SessionRecord record)
		{
			var txt = PrimitiveSerializer.Serialize(record);

			using (await m_lock.LockAsync())
			{
				if (!string.IsNullOrWhiteSpace(record.XSRFToken))
					m_xsrf_storage[record.XSRFToken] = txt;

				if (!string.IsNullOrWhiteSpace(record.Cookie))
					m_cookie_storage[record.Cookie] = txt;
			}
		}
	}
}
