using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Ceen;
using Ceen.Database;

namespace Ceen.Extras
{
    /// <summary>
    /// An abstract class for a database backed module
    /// </summary>
    public abstract class DatabaseBackedModule : IModuleWithSetup
    {
        /// <summary>
        /// The connection string to use
        /// </summary>
        public string ConnectionString { get; set; } = "";
        /// <summary>
        /// The connection class to use
        /// </summary>
        public string ConnectionClass { get; set; } = "sqlite3";

        /// <summary>
        /// Value used to toggle table validation on startup
        /// </summary>
        public bool ValidateTables { get; set; } = true;

        /// <summary>
        /// The list of tables to create in the configuration step
        /// </summary>
        protected abstract Type[] UsedTypes { get; }

        /// <summary>
        /// Gets the current executing transaction, if any
        /// </summary>
        public static IDbConnection Current => GuardedConnection.Current;

        /// <summary>
        /// The database connection
        /// </summary>
        protected GuardedConnection m_con;

        /// <summary>
        /// Returns a value indicating if the database module is initialized
        /// </summary>
        public bool IsInitialized => m_con != null && m_con.IsInitialized;

        /// <summary>
        /// Runs the given method in a transaction
        /// </summary>
        /// <param name="method">The method to run</param>
        /// <returns>An awaitable task</returns>
        public Task RunInTransactionAsync(Func<System.Data.IDbConnection, Task> method) => m_con.RunInTransactionAsync(method);

        /// <summary>
        /// Runs the given method in a transaction
        /// </summary>
        /// <param name="method">The method to run</param>
        /// <returns>An awaitable task</returns>
        public Task<T> RunInTransactionAsync<T>(Func<System.Data.IDbConnection, Task<T>> method) => m_con.RunInTransactionAsync(method);

        /// <summary>
        /// Runs the given method in a transaction
        /// </summary>
        /// <param name="method">The method to run</param>
        /// <returns>An awaitable task</returns>
        public Task<T> RunInTransactionAsync<T>(Func<System.Data.IDbConnection, T> method) => m_con.RunInTransactionAsync(method);

        /// <summary>
        /// Runs the given method in a transaction
        /// </summary>
        /// <param name="method">The method to run</param>
        /// <returns>An awaitable task</returns>
        public Task RunInTransactionAsync(Action<System.Data.IDbConnection> method) => m_con.RunInTransactionAsync(method);

        /// <summary>
        /// Returns a new empty query
        /// </summary>
        /// <typeparam name="T">The type of query to use</typeparam>
        /// <returns>The query</returns>
        public Query<T> Query<T>() => m_con.Query<T>();

        /// <summary>
        /// Gets the dialect for the current connection
        /// </summary>
        /// <returns>The dialect for the connection</returns>
        public Ceen.Database.IDatabaseDialect GetDialect() => m_con.Dialect;

        /// <summary>
        /// Configures the database and sets it up
        /// </summary>
        public virtual void AfterConfigure()
        {
            if (m_con == null)
                m_con = new GuardedConnection(ConnectionString, ConnectionClass);

            // Create all required tables
            foreach (var t in UsedTypes ?? new Type[0])
                m_con.UnguardedConnection.CreateTable(t);

            // Validate table contents, if not disabled
            if (ValidateTables)
                m_con.UnguardedConnection.ValidateTables(UsedTypes ?? new Type[0]);
        }
    }
}