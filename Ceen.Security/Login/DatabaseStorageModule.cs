﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Ceen.Database;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Implementation of a database-based storage module
	/// </summary>
	public class DatabaseStorageModule : IModuleWithSetup, IShortTermStorageModule, ILongTermStorageModule, ILoginEntryModule, IDisposable
	{
		/// <summary>
		/// Gets or sets the connection string use to connect to the database.
		/// If the database provider is &quot;sqlite&quot; and the string does not start with
		/// &quot;Data Source=&quot; the string is assumed to be a filename
		/// </summary>
		public string ConnectionString { get; set; } = "sessiondata.sqlite";

		/// <summary>
		/// Gets or sets the full name of the connection class, in standard .Net notation.
		/// The ODBC provider is &quot;System.Data.Odbc.OdbcConnection, System.Data&quot;.
		/// Specially recognized names are: &quot;sqlite&quot;, &quot;sqlite3&quot;, and &quot;odbc&quot;.
		/// </summary>
		/// <value>The connection class.</value>
		public string ConnectionClass { get; set; } = "sqlite";

		/// <summary>
		/// Gets or sets the name of the long-term login table
		/// </summary>
		public string LongTermLoginTablename { get; set; } = "LongTermLogin";

		/// <summary>
		/// Gets or sets the name of the session token table
		/// </summary>
		public string SessionRecordTablename { get; set; } = "Session";

		/// <summary>
		/// Gets or sets the name of the session token table
		/// </summary>
		public string LoginEntryTablename { get; set; } = "Login";

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Handler.Login.DatabaseStorageModule"/> class.
		/// </summary>
		public DatabaseStorageModule()
			: this(true, true, true)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Handler.Login.DatabaseStorageModule"/> class.
		/// </summary>
		/// <param name="default_short_term">If set to <c>true</c> this instance is used as the storage for the loginhandler.</param>
		/// <param name="default_authentication">If set to <c>true</c> this instance is used as the authentication for the loginhandler.</param>
		public DatabaseStorageModule(bool default_short_term, bool default_long_term, bool default_authentication)
		{
			if (default_short_term)
				new LoginSettingsModule().ShortTermStorage = this;
			if (default_long_term)
				new LoginSettingsModule().LongTermStorage = this;
			if (default_authentication)
				new LoginSettingsModule().Authentication = this;
		}

		/// <summary>
		/// The lock used to guard access to the database
		/// </summary>
		protected AsyncLock m_lock = new AsyncLock();

		/// <summary>
		/// The database connection
		/// </summary>
		protected System.Data.IDbConnection m_connection;

        /// <summary>
        /// Establishes a connection to the database, must hold the lock before this method is called.
        /// </summary>
        protected virtual void EnsureConnected()
        {
            if (m_connection != null && m_connection.State == System.Data.ConnectionState.Open)
                return;

            if (m_connection != null)
                try
                {
                    m_connection.Close();
                }
                catch
                {
                }
                finally
                {
                    m_connection = null;
                }

            m_connection = DatabaseHelper.CreateConnection(ConnectionString, ConnectionClass);
        }

		/// <summary>
		/// Creates the required tables
		/// </summary>
		protected virtual void CreateTables()
		{
            var dialect = m_connection.GetDialect();
            dialect.CreateTypeMap<SessionRecord>(SessionRecordTablename);
            dialect.CreateTypeMap<LongTermToken>(LongTermLoginTablename);
            dialect.CreateTypeMap<LoginEntry>(LoginEntryTablename);

            m_connection.CreateTable(typeof(SessionRecord));
            m_connection.CreateTable(typeof(LongTermToken));
            m_connection.CreateTable(typeof(LoginEntry));
        }

		/// <summary>
		/// Adds a new session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		public virtual Task AddSessionAsync(SessionRecord record)
		{
			return m_lock.LockedAsync(() => m_connection.InsertItem(record));
		}

		/// <summary>
		/// Drops a session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to drop.</param>
		public virtual Task DropSessionAsync(SessionRecord record)
		{
			return m_lock.LockedAsync(() => 
				m_connection.Delete<SessionRecord>(x => 
					x.UserID == record.UserID
					&& x.Cookie == record.Cookie
					&& x.XSRFToken == record.XSRFToken
				)
			);
		}

		/// <summary>
		/// Gets a session record from a cookie identifier
		/// </summary>
		/// <returns>The session record.</returns>
		/// <param name="cookie">The cookie identifier.</param>
		public virtual Task<SessionRecord> GetSessionFromCookieAsync(string cookie)
		{
            return m_lock.LockedAsync(() =>
				m_connection.SelectSingle<SessionRecord>(
					x => x.Cookie == cookie
				)
			);

		}

		/// <summary>
		/// Gets a session record from an XSRF token
		/// </summary>
		/// <returns>The session record.</returns>
		/// <param name="xsrf">The XSRF token.</param>
		public virtual Task<SessionRecord> GetSessionFromXSRFAsync(string xsrf)
		{
			return m_lock.LockedAsync(() =>
				m_connection.SelectSingle<SessionRecord>(
					x => x.XSRFToken == xsrf
				)
			);
		}

		/// <summary>
		/// Updates the expiration time on the given session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to update.</param>
		public virtual Task UpdateSessionExpirationAsync(SessionRecord record)
		{
			return m_lock.LockedAsync(() =>
				m_connection.Update<SessionRecord>(
					new { record.Expires },
					x => x.UserID == record.UserID
						&& x.Cookie == record.Cookie
						&& x.XSRFToken == record.XSRFToken
                )
			);
		}

		/// <summary>
		/// Adds a long term login entry
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		public virtual Task AddOrUpdateLongTermLoginAsync(LongTermToken record)
		{
			return m_lock.LockedAsync(() => {
				using(var con = new TransactionConnection(m_connection, m_connection.BeginTransaction()))
				{
					con.Delete<LongTermToken>(x => x.Series == record.Series);
					con.InsertItem(record);
					con.Commit();
				}
			});
		}

		/// <summary>
		/// Drops all long term logins for a given user.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="userid">The user for whom the long term logins must be dropped.</param>
		/// <param name="series">The series identifier for the login token that caused the issuance.</param>
		public virtual Task DropAllLongTermLoginsAsync(string userid, string series)
		{
			return m_lock.LockedAsync(() =>
				m_connection.Delete<LongTermToken>(x => 
					x.UserID == userid || x.Series == series
			));
		}

		/// <summary>
		/// Drops the given long term login entry.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record indentifying the login to drop.</param>
		public virtual Task DropLongTermLoginAsync(LongTermToken record)
		{
			return m_lock.LockedAsync(() =>
				m_connection.Delete<LongTermToken>(x => 
					x.UserID == record.UserID
					&& x.Series == record.Series
					&& x.Token == record.Token
			));
		}

		/// <summary>
		/// Gets a long-term login entry
		/// </summary>
		/// <returns>The long term login entry.</returns>
		/// <param name="series">The series identifier to use for lookup.</param>
		public virtual Task<LongTermToken> GetLongTermLoginAsync(string series)
		{
			return m_lock.LockedAsync(() =>
				m_connection.SelectSingle<LongTermToken>(x => x.Series == series)
			);
		}

		/// <summary>
		/// Called periodically to expire old items
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public virtual Task ExpireOldItemsAsync()
		{
            return m_lock.LockedAsync(() =>
            {
				var time = DateTime.Now;
				m_connection.Delete<SessionRecord>(x => x.Expires <= time);
                m_connection.Delete<LongTermToken>(x => x.Expires <= time);
            });
		}

		/// <summary>
		/// Releases all resource used by the <see cref="T:Ceen.Httpd.Handler.Login.DatabaseStorageModule"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the
		/// <see cref="T:Ceen.Httpd.Handler.Login.DatabaseStorageModule"/>. The <see cref="Dispose"/> method leaves the
		/// <see cref="T:Ceen.Httpd.Handler.Login.DatabaseStorageModule"/> in an unusable state. After calling
		/// <see cref="Dispose"/>, you must release all references to the
		/// <see cref="T:Ceen.Httpd.Handler.Login.DatabaseStorageModule"/> so the garbage collector can reclaim the memory
		/// that the <see cref="T:Ceen.Httpd.Handler.Login.DatabaseStorageModule"/> was occupying.</remarks>
		public void Dispose()
		{
			foreach (var f in this.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
				if (typeof(System.Data.IDbCommand).IsAssignableFrom(f.FieldType))
					try { ((System.Data.IDbCommand)f.GetValue(this)).Dispose(); }
					catch { }

			if (m_connection != null)
			{
				m_connection.Dispose();
				m_connection = null;
			}
		}

		/// <summary>
		/// Returns the user information, or null, for a user with the given name
		/// </summary>
		/// <returns>The login entries.</returns>
		/// <param name="username">The username to get the login tokens for.</param>
		public virtual Task<IEnumerable<LoginEntry>> GetLoginEntriesAsync(string username)
		{
			return m_lock.LockedAsync(() =>
				m_connection
				.Select<LoginEntry>(x => x.Username == username)
				
				// Force allocation while having the lock
				.ToList()
				
				// And return as enumerable
				.AsEnumerable()
			);
		}

		/// <summary>
		/// Adds a login entry to the storage
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		public virtual Task AddLoginEntryAsync(LoginEntry record)
		{
			return m_lock.LockedAsync(() =>
				m_connection.InsertItem(record)
			);
		}

		/// <summary>
		/// Deletes a login entry from the storage
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to drop.</param>
		public virtual Task DropLoginEntryAsync(LoginEntry record)
		{
			return m_lock.LockedAsync(() =>
				m_connection.Delete<LoginEntry>(x =>
					x.UserID == record.UserID
					&& x.Username == record.Username
					&& x.Token == record.Token
				)
			);
		}

		/// <summary>
		/// Drops all login entries for the given userid or username.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="userid">The user ID.</param>
		/// <param name="username">The user name.</param>
		public virtual Task DropAllLoginEntriesAsync(string userid, string username)
		{
			return m_lock.LockedAsync(() =>
				m_connection.Delete<LoginEntry>(x =>
					x.UserID == userid
					&& x.Username == username
                )
			);
		}

		/// <summary>
		/// Updates the login entry.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to update.</param>
		public virtual Task UpdateLoginTokenAsync(LoginEntry record)
		{
			return m_lock.LockedAsync(() =>
				m_connection.Update<LoginEntry>(
					new { record.Token },
					x => x.UserID == record.UserID
                    && x.Username == record.Username
				)
			);
		}

		/// <summary>
		/// Configuration method to set up everything
		/// </summary>
        public void AfterConfigure()
        {
            EnsureConnected();
            CreateTables();
        }
    }
}
