﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

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
			
			EnsureConnected();
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

			var classname = ConnectionClass;
			var connstr = ConnectionString;
			if (string.Equals(classname, "sqlite", StringComparison.OrdinalIgnoreCase) || string.Equals(classname, "sqlite3", StringComparison.OrdinalIgnoreCase))
			{
				classname = "Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756";
				if (Type.GetType(classname) == null)
					classname = "System.Data.SQLite.SQLiteConnection, System.Data.SQLite, Version=1.0.104.0, Culture=neutral, PublicKeyToken=db937bc2d44ff139";

				if (!string.IsNullOrWhiteSpace(connstr) && !connstr.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
					connstr = "Data Source=" + connstr;
			}
			else if (string.Equals(classname, "odbc", StringComparison.OrdinalIgnoreCase))
				classname = "System.Data.Odbc.OdbcConnection, System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

			var contype = Type.GetType(classname);
			if (contype == null)
				throw new Exception($"Failed to locate the requested database type: {classname}");
			var e = Activator.CreateInstance(contype);
			if (!(e is System.Data.IDbConnection))
				throw new Exception($"The requested type {contype.FullName} is not implementing {typeof(System.Data.IDbConnection).FullName}");

			m_connection = e as System.Data.IDbConnection;

			m_connection.ConnectionString = connstr;
			m_connection.Open();

			SetupCommands();
		}

		/// <summary>
		/// Sets up all the required commands, must hold the lock before this method is called.
		/// </summary>
		protected virtual void SetupCommands()
		{
			CreateTable(SessionRecordTablename, typeof(SessionRecord), "Cookie");
			CreateTable(LongTermLoginTablename, typeof(LongTermToken), "Series");
			CreateTable(LoginEntryTablename, typeof(LoginEntry), "Username", "Token");

			m_addSessionCommand = SetupCommand(string.Format(@"INSERT INTO ""{0}"" (""UserID"", ""Cookie"", ""XSRFToken"", ""Expires"") VALUES (?, ?, ?, ?)", SessionRecordTablename));
			m_dropSessionCommand = SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? AND ""Cookie"" = ? AND ""XSRFToken"" = ?", SessionRecordTablename));
			m_updateSessionCommand = SetupCommand(string.Format(@"UPDATE ""{0}"" SET ""Expires"" = ? WHERE ""UserID"" = ? AND ""Cookie"" = ?  AND ""XSRFToken"" = ?", SessionRecordTablename));
			m_getSessionFromCookieCommand = SetupCommand(string.Format(@"SELECT ""UserID"", ""Cookie"", ""XSRFToken"", ""Expires"" FROM ""{0}"" WHERE ""Cookie"" = ?", SessionRecordTablename));
			m_getSessionFromXSRFCommand = SetupCommand(string.Format(@"SELECT ""UserID"", ""Cookie"", ""XSRFToken"", ""Expires"" FROM ""{0}"" WHERE ""XSRFToken"" = ?", SessionRecordTablename));

			m_addLongTermLoginCommand = SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""Series"" = ?; INSERT INTO ""{0}"" (""UserID"", ""Series"", ""Token"", ""Expires"") VALUES (?, ?, ?, ?)", LongTermLoginTablename));
			m_dropLongTermLoginCommand = SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? AND ""Series"" = ? AND ""Token"" = ?", LongTermLoginTablename));
			m_dropAllLongTermLoginCommand = SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? OR ""Series"" = ?", LongTermLoginTablename));
			m_getLongTermLoginCommand = SetupCommand(string.Format(@"SELECT ""UserID"", ""Series"", ""Token"", ""Expires"" FROM ""{0}"" WHERE ""Series"" = ?", LongTermLoginTablename));

			m_dropExpiredSessionsCommand = SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""Expires"" <= ?", SessionRecordTablename));
			m_dropExpiredLongTermCommand = SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""Expires"" <= ?", LongTermLoginTablename));

			m_addLoginEntryCommand = SetupCommand(string.Format(@"INSERT INTO ""{0}"" (""UserID"", ""Username"", ""Token"") VALUES (?, ?, ?)", LoginEntryTablename));
			m_getLoginEntriesCommand = SetupCommand(string.Format(@"SELECT ""UserID"", ""Username"", ""Token"" FROM ""{0}"" WHERE ""Username"" = ?", LoginEntryTablename));
			m_dropLoginEntryCommand = SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? AND ""Username"" = ? AND ""Token"" = ?", LoginEntryTablename));
			m_dropAllLoginEntryCommand = SetupCommand(string.Format(@"DELETE FROM ""{0}"" WHERE ""UserID"" = ? AND ""Username"" = ?", LoginEntryTablename));
			m_updateLoginEntryCommand = SetupCommand(string.Format(@"UPDATE ""{0}"" SET ""Token"" = ? WHERE ""UserID"" = ? AND ""Username"" = ?", LoginEntryTablename));
		}

		/// <summary>
		/// Gets the SQL type for a given property
		/// </summary>
		/// <returns>The sql column type.</returns>
		/// <param name="property">The property being examined.</param>
		protected virtual string GetSqlColumnType(System.Reflection.PropertyInfo property)
		{
			if (property.PropertyType == typeof(int) || property.PropertyType == typeof(uint) || property.PropertyType == typeof(short) || property.PropertyType == typeof(ushort) || property.PropertyType == typeof(long) || property.PropertyType == typeof(ulong))
			{
				if (property.Name == "ID")
					return "INTEGER PRIMARY KEY";
				
				return "INTEGER";
			}
			else if (property.PropertyType == typeof(DateTime))
				return "DATETIME";
			else
				return "STRING";
		}

		/// <summary>
		/// Creates the table for the given type.
		/// </summary>
		/// <param name="tablename">The name of the table to create.</param>
		/// <param name="recordtype">The datatype to store in the table.</param>
		/// <param name="unique">The list of unique columns.</param>
		protected virtual void CreateTable(string tablename, Type recordtype, params string[] unique)
		{
			var fields =
				string.Join(", ",
					recordtype
					.GetProperties()
					.Select(x => string.Format(@"""{0}"" {1}", x.Name, GetSqlColumnType(x)))
				);

			var constr =
				(unique == null || unique.Length == 0)
				? string.Empty
				: string.Format(@", CONSTRAINT ""{0}_unique"" UNIQUE({1})", tablename, string.Join(", ", unique));

			var sql = string.Format(
				@"CREATE TABLE ""{0}"" ({1} {2}) ",
				tablename,
				fields,
				constr
			);

			using (var cmd = m_connection.CreateCommand())
			{
				try
				{
					// Check if the table exists
					cmd.CommandText = string.Format(@"SELECT COUNT(*) FROM ""{0}""", tablename);
					var r = cmd.ExecuteScalar();
					if (r != null && r != DBNull.Value)
						return;
				}
				catch
				{
				}

				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Helper method for creating a command and initializing the parameters
		/// </summary>
		/// <returns>The command.</returns>
		/// <param name="commandtext">The commandtext.</param>
		protected IDbCommand SetupCommand(string commandtext, IDbTransaction transaction = null)
		{
			if (string.IsNullOrWhiteSpace(commandtext))
				throw new ArgumentNullException(nameof(commandtext));

			var cmd = m_connection.CreateCommand();
			cmd.CommandText = commandtext;
			cmd.Transaction = transaction;
			AddParameters(cmd, commandtext.Count(x => x == '?'));
			return cmd;
		}

		/// <summary>
		/// Adds a number of parameters to the command
		/// </summary>
		/// <param name="cmd">The command to add the parameters to.</param>
		/// <param name="count">The number of parameters to add.</param>
		public static void AddParameters(IDbCommand cmd, int count)
		{
			if (cmd == null)
				throw new ArgumentNullException(nameof(cmd));
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));

			if (cmd.Parameters.Count > count)
				cmd.Parameters.Clear();
			for (var i = cmd.Parameters.Count; i < count; i++)
				cmd.Parameters.Add(cmd.CreateParameter());
		}

		/// <summary>
		/// Sets the parameter values.
		/// </summary>
		/// <param name="cmd">The command to set parameter values on.</param>
		/// <param name="values">The values to set.</param>
		public static void SetParameterValues(IDbCommand cmd, params object[] values)
		{
			if (cmd == null)
				throw new ArgumentNullException(nameof(cmd));

			values = values ?? new object[0];

			AddParameters(cmd, values.Length);
			for (var i = 0; i < values.Length; i++)
				((IDbDataParameter)cmd.Parameters[i]).Value = values[i];
		}

		/// <summary>
		/// Fixes a deficiency in the database mapping,
		///  and returns string null values as null
		/// </summary>
		/// <returns>The string representation.</returns>
		/// <param name="rd">The reader to use.</param>
		/// <param name="index">The index to read the string from.</param>
		public static string GetAsString(IDataReader rd, int index)
		{
			var val = rd.GetValue(index);

			if (val == null || val == DBNull.Value)
				return null;
			else
				return (string)val;
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
                SetParameterValues(command, values);
                command.ExecuteNonQuery();
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
				SetParameterValues(m_getSessionFromCookieCommand, cookie);
				using (var rd = m_getSessionFromCookieCommand.ExecuteReader())
				{
					if (!rd.Read())
						return null;
					else
						return new SessionRecord()
						{
							UserID = GetAsString(rd, 0),
							Cookie = GetAsString(rd, 1),
							XSRFToken = GetAsString(rd, 2),
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
				SetParameterValues(m_getSessionFromXSRFCommand, xsrf);
				using (var rd = m_getSessionFromXSRFCommand.ExecuteReader())
				{
					if (!rd.Read())
						return null;
					else
						return new SessionRecord()
						{
							UserID = GetAsString(rd, 0),
							Cookie = GetAsString(rd, 1),
							XSRFToken = GetAsString(rd, 2),
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
				SetParameterValues(m_getLongTermLoginCommand, series);
				using (var rd = m_getLongTermLoginCommand.ExecuteReader())
				{
					if (!rd.Read())
						return null;
					else
						return new LongTermToken()
						{
							UserID = GetAsString(rd, 0),
							Series = GetAsString(rd, 1),
							Token = GetAsString(rd, 2),
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
                SetParameterValues(m_dropExpiredSessionsCommand, DateTime.Now);
                m_dropExpiredSessionsCommand.ExecuteNonQuery();
                SetParameterValues(m_dropExpiredLongTermCommand, DateTime.Now);
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
				SetParameterValues(m_getLoginEntriesCommand, username);
				using (var rd = m_getLoginEntriesCommand.ExecuteReader())
				{
					while (rd.Read())
						lst.Add(new LoginEntry()
						{
							UserID = GetAsString(rd, 0),
							Username = GetAsString(rd, 1),
							Token = GetAsString(rd, 2)
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
