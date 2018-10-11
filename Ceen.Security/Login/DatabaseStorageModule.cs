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
	public class DatabaseStorageModule : IModule, IShortTermStorageModule, ILongTermStorageModule, ILoginEntryModule, IDisposable
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
		/// The command used to add a session record
		/// </summary>
		protected System.Data.IDbCommand m_addSessionCommand;
		/// <summary>
		/// The command used to drop a session record
		/// </summary>
		protected System.Data.IDbCommand m_dropSessionCommand;
		/// <summary>
		/// The command used to update the session records expiration valued
		/// </summary>
		protected System.Data.IDbCommand m_updateSessionCommand;
		/// <summary>
		/// The command used to get a session record from a cookie identifier
		/// </summary>
		protected System.Data.IDbCommand m_getSessionFromCookieCommand;
		/// <summary>
		/// The command used to get a session record from an XSRF token
		/// </summary>
		protected System.Data.IDbCommand m_getSessionFromXSRFCommand;

		/// <summary>
		/// The command used to get the long term login record
		/// </summary>
		protected System.Data.IDbCommand m_getLongTermLoginCommand;
		/// <summary>
		/// The comand used to drop a long term login record
		/// </summary>
		protected System.Data.IDbCommand m_dropLongTermLoginCommand;
		/// <summary>
		/// The command used to drop all long term login records
		/// </summary>
		protected System.Data.IDbCommand m_dropAllLongTermLoginCommand;
		/// <summary>
		/// The command used to add a long term login record
		/// </summary>
		protected System.Data.IDbCommand m_addLongTermLoginCommand;

		/// <summary>
		/// The command used to drop expired sessions
		/// </summary>
		protected System.Data.IDbCommand m_dropExpiredSessionsCommand;
		/// <summary>
		/// The command used to drop expired long term logins
		/// </summary>
		protected System.Data.IDbCommand m_dropExpiredLongTermCommand;

		/// <summary>
		/// The command used to add a user login entry
		/// </summary>
		protected System.Data.IDbCommand m_addLoginEntryCommand;
		/// <summary>
		/// The command used to get the user login entries
		/// </summary>
		protected System.Data.IDbCommand m_getLoginEntriesCommand;
		/// <summary>
		/// The command used to drop a login entry
		/// </summary>
		protected System.Data.IDbCommand m_dropLoginEntryCommand;
		/// <summary>
		/// The command used to drop all login entries
		/// </summary>
		protected System.Data.IDbCommand m_dropAllLoginEntryCommand;
		/// <summary>
		/// The command used to update login entries
		/// </summary>
		protected System.Data.IDbCommand m_updateLoginEntryCommand;

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
        /// Sets up all the required commands, must hold the lock before this method is called.
        /// </summary>
        protected virtual void SetupCommands()
		{
            var dialect = m_connection.GetDialect();
            dialect.CreateTypeMap<SessionRecord>(SessionRecordTablename);
            dialect.CreateTypeMap<LongTermToken>(LongTermLoginTablename);
            dialect.CreateTypeMap<LoginEntry>(LoginEntryTablename);

			m_addSessionCommand = m_connection.SetupCommand(string.Format(@"INSERT INTO ""{0}"" (""UserID"", ""Cookie"", ""XSRFToken"", ""Expires"") VALUES (?, ?, ?, ?)", SessionRecordTablename));
			m_dropSessionCommand = m_connection.SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? AND ""Cookie"" = ? AND ""XSRFToken"" = ?", SessionRecordTablename));
			m_updateSessionCommand = m_connection.SetupCommand(string.Format(@"UPDATE ""{0}"" SET ""Expires"" = ? WHERE ""UserID"" = ? AND ""Cookie"" = ?  AND ""XSRFToken"" = ?", SessionRecordTablename));
			m_getSessionFromCookieCommand = m_connection.SetupCommand(string.Format(@"SELECT ""UserID"", ""Cookie"", ""XSRFToken"", ""Expires"" FROM ""{0}"" WHERE ""Cookie"" = ?", SessionRecordTablename));
			m_getSessionFromXSRFCommand = m_connection.SetupCommand(string.Format(@"SELECT ""UserID"", ""Cookie"", ""XSRFToken"", ""Expires"" FROM ""{0}"" WHERE ""XSRFToken"" = ?", SessionRecordTablename));

			m_addLongTermLoginCommand = m_connection.SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""Series"" = ?; INSERT INTO ""{0}"" (""UserID"", ""Series"", ""Token"", ""Expires"") VALUES (?, ?, ?, ?)", LongTermLoginTablename));
			m_dropLongTermLoginCommand = m_connection.SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? AND ""Series"" = ? AND ""Token"" = ?", LongTermLoginTablename));
			m_dropAllLongTermLoginCommand = m_connection.SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? OR ""Series"" = ?", LongTermLoginTablename));
			m_getLongTermLoginCommand = m_connection.SetupCommand(string.Format(@"SELECT ""UserID"", ""Series"", ""Token"", ""Expires"" FROM ""{0}"" WHERE ""Series"" = ?", LongTermLoginTablename));

			m_dropExpiredSessionsCommand = m_connection.SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""Expires"" <= ?", SessionRecordTablename));
			m_dropExpiredLongTermCommand = m_connection.SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""Expires"" <= ?", LongTermLoginTablename));

			m_addLoginEntryCommand = m_connection.SetupCommand(string.Format(@"INSERT INTO ""{0}"" (""UserID"", ""Username"", ""Token"") VALUES (?, ?, ?)", LoginEntryTablename));
			m_getLoginEntriesCommand = m_connection.SetupCommand(string.Format(@"SELECT ""UserID"", ""Username"", ""Token"" FROM ""{0}"" WHERE ""Username"" = ?", LoginEntryTablename));
			m_dropLoginEntryCommand = m_connection.SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? AND ""Username"" = ? AND ""Token"" = ?", LoginEntryTablename));
			m_dropAllLoginEntryCommand = m_connection.SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? AND ""Username"" = ?", LoginEntryTablename));
			m_updateLoginEntryCommand = m_connection.SetupCommand(string.Format(@"UPDATE ""{0}"" SET ""Token"" = ? WHERE ""UserID"" = ? AND ""Username"" = ?", LoginEntryTablename));
		}

        /// <summary>
        /// Executes a command while being locked
        /// </summary>
        /// <returns>The command async.</returns>
        /// <param name="command">Command.</param>
        /// <param name="values">Values.</param>
        public virtual Task ExecuteCommandAsync(IDbCommand command, params object[] values)
        {
            return m_lock.LockedAsync(() => {

                EnsureConnected();
                command.ExecuteNonQuery(values);
            });
        }


		/// <summary>
		/// Adds a new session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		public virtual Task AddSessionAsync(SessionRecord record)
		{
            return ExecuteCommandAsync(
                m_addSessionCommand,
				record.UserID,
				record.Cookie,
				record.XSRFToken,
				record.Expires
			);
		}

		/// <summary>
		/// Drops a session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to drop.</param>
		public virtual Task DropSessionAsync(SessionRecord record)
		{
            return ExecuteCommandAsync(
				m_dropSessionCommand,
				record.UserID,
				record.Cookie,
				record.XSRFToken
			);
		}

		/// <summary>
		/// Gets a session record from a cookie identifier
		/// </summary>
		/// <returns>The session record.</returns>
		/// <param name="cookie">The cookie identifier.</param>
		public virtual async Task<SessionRecord> GetSessionFromCookieAsync(string cookie)
		{
			using (await m_lock.LockAsync())
			{
				EnsureConnected();
                m_getSessionFromCookieCommand.SetParameterValues(cookie);
				using (var rd = m_getSessionFromCookieCommand.ExecuteReader())
				{
					if (!rd.Read())
						return null;
					else
						return new SessionRecord()
						{
							UserID = rd.GetAsString(0),
							Cookie = rd.GetAsString(1),
							XSRFToken = rd.GetAsString(2),
							Expires = rd.GetDateTime(3)
						};
				}
			}
		}

		/// <summary>
		/// Gets a session record from an XSRF token
		/// </summary>
		/// <returns>The session record.</returns>
		/// <param name="xsrf">The XSRF token.</param>
		public virtual async Task<SessionRecord> GetSessionFromXSRFAsync(string xsrf)
		{
			using (await m_lock.LockAsync())
			{
				EnsureConnected();
                m_getSessionFromXSRFCommand.SetParameterValues(xsrf);
				using (var rd = m_getSessionFromXSRFCommand.ExecuteReader())
				{
					if (!rd.Read())
						return null;
					else
						return new SessionRecord()
						{
							UserID = rd.GetAsString(0),
							Cookie = rd.GetAsString(1),
							XSRFToken = rd.GetAsString(2),
							Expires = rd.GetDateTime(3)
						};
				}
			}
		}

		/// <summary>
		/// Updates the expiration time on the given session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to update.</param>
		public virtual Task UpdateSessionExpirationAsync(SessionRecord record)
		{
            return ExecuteCommandAsync(
				m_updateSessionCommand,
				record.Expires,
				record.UserID,
				record.Cookie,
				record.XSRFToken
			);
		}

		/// <summary>
		/// Adds a long term login entry
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		public virtual Task AddOrUpdateLongTermLoginAsync(LongTermToken record)
		{
            return ExecuteCommandAsync(
				m_addLongTermLoginCommand,
				record.Series,
				record.UserID,
				record.Series,
				record.Token,
				record.Expires
			);
		}

		/// <summary>
		/// Drops all long term logins for a given user.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="userid">The user for whom the long term logins must be dropped.</param>
		/// <param name="series">The series identifier for the login token that caused the issuance.</param>
		public virtual Task DropAllLongTermLoginsAsync(string userid, string series)
		{
            return ExecuteCommandAsync(
				m_dropAllLongTermLoginCommand,
				userid,
				series
			);
		}

		/// <summary>
		/// Drops the given long term login entry.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">Record.</param>
		public virtual  Task DropLongTermLoginAsync(LongTermToken record)
		{
            return ExecuteCommandAsync(
				m_dropLongTermLoginCommand,
				record.UserID,
				record.Series,
				record.Token
			);
		}

		/// <summary>
		/// Gets a long-term login entry
		/// </summary>
		/// <returns>The long term login entry.</returns>
		/// <param name="series">The series identifier to use for lookup.</param>
		public virtual async Task<LongTermToken> GetLongTermLoginAsync(string series)
		{
			using (await m_lock.LockAsync())
			{
				EnsureConnected();
                m_getLongTermLoginCommand.SetParameterValues(series);
				using (var rd = m_getLongTermLoginCommand.ExecuteReader())
				{
					if (!rd.Read())
						return null;
					else
						return new LongTermToken()
						{
							UserID = rd.GetAsString(0),
							Series = rd.GetAsString(1),
							Token = rd.GetAsString(2),
							Expires = rd.GetDateTime(3)
						};
				}
			}
		}

		/// <summary>
		/// Called periodically to expire old items
		/// </summary>
		/// <returns>An awaitable task.</returns>
		public virtual Task ExpireOldItemsAsync()
		{
            return m_lock.LockedAsync(() =>
            {
                m_dropExpiredSessionsCommand.SetParameterValues(DateTime.Now);
                m_dropExpiredSessionsCommand.ExecuteNonQuery();
                m_dropExpiredLongTermCommand.SetParameterValues(DateTime.Now);
                m_dropExpiredLongTermCommand.ExecuteNonQuery();
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
		public virtual async Task<IEnumerable<LoginEntry>> GetLoginEntriesAsync(string username)
		{
			var lst = new List<LoginEntry>();
			using (await m_lock.LockAsync())
			{
				EnsureConnected();
                m_getLoginEntriesCommand.SetParameterValues(username);
				using (var rd = m_getLoginEntriesCommand.ExecuteReader())
				{
					while (rd.Read())
						lst.Add(new LoginEntry()
						{
							UserID = rd.GetAsString(0),
							Username = rd.GetAsString(1),
							Token = rd.GetAsString(2)
						});
				}
			}

			// Not using enumerable, because it messes with the lock if the caller does not exhaust the enumerable
			return lst;
		}

		/// <summary>
		/// Adds a login entry to the storage
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		public virtual Task AddLoginEntryAsync(LoginEntry record)
		{
            return ExecuteCommandAsync(
				m_addLoginEntryCommand,
				record.UserID,
				record.Username,
				record.Token
			);
		}

		/// <summary>
		/// Deletes a login entry from the storage
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to drop.</param>
		public virtual Task DropLoginEntryAsync(LoginEntry record)
		{
            return ExecuteCommandAsync(
				m_dropLoginEntryCommand,
				record.UserID,
				record.Username,
				record.Token
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
            return ExecuteCommandAsync(
				m_dropAllLoginEntryCommand,
				userid,
				username
			);
		}

		/// <summary>
		/// Updates the login entry.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to update.</param>
		public virtual Task UpdateLoginTokenAsync(LoginEntry record)
		{
            return ExecuteCommandAsync(
				m_updateLoginEntryCommand,
				record.Token,
				record.UserID,
				record.Username
			);
		}
	}
}
