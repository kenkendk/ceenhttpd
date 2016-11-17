using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ceen.Httpd.Handler
{
	public class SetXSRFCookieHandler : IHttpModule, IDisposable
	{
		/// <summary>
		/// The name of the storage module
		/// </summary>
		public const string STORAGE_MODULE_NAME = "xsfr-tokens";

		/// <summary>
		/// Gets or sets the name of the cookie with the token.
		/// </summary>
		public string CookieName { get; set; } = "xsrf-token";
		/// <summary>
		/// Gets or sets the number of seconds an XSRF token is valid.
		/// </summary>
		public int ExpirationSeconds
		{
			get { return m_expirationSeconds; }
			set 
			{ 
				m_expirationSeconds = value;
				if (m_expirationHandler != null)
					m_expirationHandler.Interval = TimeSpan.FromSeconds(m_expirationSeconds);
			}
		}

		/// <summary>
		/// The expiration internval in seconds.
		/// </summary>
		private int m_expirationSeconds;

		/// <summary>
		/// The storage element
		/// </summary>
		protected IStorageEntry m_storage;

		/// <summary>
		/// The expiration handler
		/// </summary>
		private PeriodicTask m_expirationHandler;

		/// <summary>
		/// The lock
		/// </summary>
		private AsyncLock m_lock;

		/// <summary>
		/// A lock object to ensure we only have a single expiration handler pr storage
		/// </summary>
		private static object _lock = new object();

		/// <summary>
		/// The lookup table with expiration handlers
		/// </summary>
		private static Dictionary<IStorageEntry, PeriodicTask> _handlers = new Dictionary<IStorageEntry, PeriodicTask>();

		/// <summary>
		/// The lookup table with locks
		/// </summary>
		private static Dictionary<IStorageEntry, AsyncLock> _locks = new Dictionary<IStorageEntry, AsyncLock>();

		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			if (m_storage == null)
				m_storage = await context.Storage.GetStorageAsync(STORAGE_MODULE_NAME, string.Empty, -1, true);

			if (m_lock == null)
				m_lock = GetLockForStorage(m_storage);
			
			if (m_expirationHandler != null)
				SetupExpirationHandler();

			var token = context.Request.Cookies[CookieName];

			// If we get a token, check if it exists and is not expired
			if (!string.IsNullOrWhiteSpace(token))
			{
				var exp = m_storage[token];
				if (!string.IsNullOrWhiteSpace(exp))
				{
					if (IsExpired(exp))
						using (await m_lock.LockAsync())
							m_storage.Remove(token);
				}
				else
				{
					token = null;
				}
			}

			// If we are not re-using a token, make a new one
			if (string.IsNullOrWhiteSpace(token))
				token = Guid.NewGuid().ToString();

			// Refresh the expiration time
			using (await m_lock.LockAsync())
				m_storage[token] = DateTime.UtcNow.Ticks.ToString() + "," + TimeSpan.FromSeconds(ExpirationSeconds).Ticks;
			
			context.Response.AddCookie(CookieName, token);

			return false;
		}

		/// <summary>
		/// Gets a value indicating if a particular token is expired
		/// </summary>
		/// <returns><c>true</c>, if expired was ised, <c>false</c> otherwise.</returns>
		/// <param name="tokenvalue">The stored token.</param>
		public static bool IsExpired(string tokenvalue)
		{
			var parts = tokenvalue.Split(',');

			if (parts == null || parts.Length != 2)
			{
				return true;
			}
			else
			{
				long time;
				long duration;
				if (!(long.TryParse(parts[0], out time) && long.TryParse(parts[1], out duration)))
					return true;
				
				if (new DateTime(time, DateTimeKind.Utc).AddTicks(duration) < DateTime.UtcNow)
					return true;
				
				return false;
			}
		}

		/// <summary>
		/// Renews a token by setting the timestamp to now
		/// </summary>
		/// <returns>The renewed token.</returns>
		/// <param name="prevtoken">The previous token.</param>
		public static string RenewToken(string prevtoken)
		{
			var parts = prevtoken.Split(',');

			if (parts == null || parts.Length != 2)
			{
				return null;
			}
			else
			{
				long duration;
				if (!(long.TryParse(parts[1], out duration)))
					return null;

				return DateTime.UtcNow.Ticks.ToString() + "," + parts[1];
			}
		}

		/// <summary>
		/// Setup the expiration handler.
		/// </summary>
		protected void SetupExpirationHandler()
		{
			if (m_storage == null)
				return;
			
			lock(_lock)
			{
				_handlers.TryGetValue(m_storage, out m_expirationHandler);
				if (m_expirationHandler == null || m_expirationHandler.IsStopped)
				{
					m_expirationHandler = _handlers[m_storage] = new PeriodicTask(HandleExpiration, TimeSpan.FromSeconds(ExpirationSeconds));
					if (!AppDomain.CurrentDomain.IsDefaultAppDomain())
						AppDomain.CurrentDomain.DomainUnload += (sender, e) => { m_expirationHandler.StopAsync(); };
				}
			}
		}

		/// <summary>
		/// Gets the lock for the storage.
		/// </summary>
		/// <returns>The lock for storage.</returns>
		/// <param name="storage">The storage to find a lock for.</param>
		public static AsyncLock GetLockForStorage(IStorageEntry storage)
		{
			if (storage == null)
				throw new ArgumentNullException(nameof(storage));

			AsyncLock res;

			lock (_lock)
			{
				_locks.TryGetValue(storage, out res);
				if (res == null)
					res = _locks[storage] = new AsyncLock();
			}

			return res;
		}
		/// <summary>
		/// Handles the expiration checks.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="forced">If set to <c>true</c>, the expiration check is forced.</param>
		protected async Task HandleExpiration(bool forced)
		{
			var index = 0;
			var chunksize = 1000;
			while(m_storage.Count < index)
				using (await m_lock.LockAsync())
				{
					try
					{
						// Process a few items at a time
						var items = m_storage.Skip(index).Take(chunksize).ToArray();
						index += chunksize;

						foreach (var k in items)
							if (IsExpired(k.Value) && m_storage.Remove(k.Key))
								index--;
					}
					catch
					{
					}
				}
		}

		/// <summary>
		/// Releases all resource used by the <see cref="T:Ceen.Httpd.Handler.SetXSRFCookieHandler"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the
		/// <see cref="T:Ceen.Httpd.Handler.SetXSRFCookieHandler"/>. The <see cref="Dispose"/> method leaves the
		/// <see cref="T:Ceen.Httpd.Handler.SetXSRFCookieHandler"/> in an unusable state. After calling <see cref="Dispose"/>,
		/// you must release all references to the <see cref="T:Ceen.Httpd.Handler.SetXSRFCookieHandler"/> so the garbage
		/// collector can reclaim the memory that the <see cref="T:Ceen.Httpd.Handler.SetXSRFCookieHandler"/> was occupying.</remarks>
		public void Dispose()
		{
			if (m_expirationHandler != null)
				m_expirationHandler.StopAsync();
		}

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="T:Ceen.Httpd.Handler.SetXSRFCookieHandler"/> is reclaimed by garbage collection.
		/// </summary>
		~SetXSRFCookieHandler()
		{
			Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
