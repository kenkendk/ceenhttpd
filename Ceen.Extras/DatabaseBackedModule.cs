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
        /// The list of tables to create in the configuration step
        /// </summary>
        protected abstract Type[] UsedTypes { get; }

        /// <summary>
        /// Keeping track of the current executing transaction
        /// </summary>
        protected static readonly AsyncLocal<IDbConnection> m_current = new AsyncLocal<IDbConnection>();

        /// <summary>
        /// Gets the current executing transaction, if any
        /// </summary>
        public static IDbConnection Current => m_current.Value;

        /// <summary>
        /// The database connection
        /// </summary>
        protected System.Data.IDbConnection m_con;

        /// <summary>
        /// The lock guarding the database
        /// </summary>
        protected AsyncLock m_lock = new AsyncLock();

        /// <summary>
        /// Returns a value indicating if the database module is initialized
        /// </summary>
        public bool IsInitialized => m_con != null;

        /// <summary>
        /// Runs the given method in a transaction
        /// </summary>
        /// <param name="method">The method to run</param>
        /// <returns>An awaitable task</returns>
        public async Task RunInTransactionAsync(Func<System.Data.IDbConnection, Task> method)
        {
            using (await m_lock.LockAsync())
            using (var tr = m_con.BeginTransaction())
            {
                await method(m_current.Value = new Ceen.Database.TransactionConnection(m_con, tr));
                tr.Commit();
            }
        }

        /// <summary>
        /// Runs the given method in a transaction
        /// </summary>
        /// <param name="method">The method to run</param>
        /// <returns>An awaitable task</returns>
        public async Task<T> RunInTransactionAsync<T>(Func<System.Data.IDbConnection, Task<T>> method)
        {
            using (await m_lock.LockAsync())
            using (var tr = m_con.BeginTransaction())
            {
                var res = await method(m_current.Value = new Ceen.Database.TransactionConnection(m_con, tr));
                tr.Commit();
                return res;
            }
        }

        /// <summary>
        /// Runs the given method in a transaction
        /// </summary>
        /// <param name="method">The method to run</param>
        /// <returns>An awaitable task</returns>
        public async Task<T> RunInTransactionAsync<T>(Func<System.Data.IDbConnection, T> method)
        {
            using (await m_lock.LockAsync())
            using (var tr = m_con.BeginTransaction())
            {
                var res = method(m_current.Value = new Ceen.Database.TransactionConnection(m_con, tr));
                tr.Commit();
                return res;
            }
        }
        /// <summary>
        /// Runs the given method in a transaction
        /// </summary>
        /// <param name="method">The method to run</param>
        /// <returns>An awaitable task</returns>
        public async Task RunInTransactionAsync(Action<System.Data.IDbConnection> method)
        {
            using (await m_lock.LockAsync())
            using (var tr = m_con.BeginTransaction())
            {
                method(m_current.Value = new Ceen.Database.TransactionConnection(m_con, tr));
                tr.Commit();
            }
        }

        /// <summary>
        /// Gets the dialect for the current connection
        /// </summary>
        /// <returns>The dialect for the connection</returns>
        public Ceen.Database.IDatabaseDialect GetDialect()
        {
            return m_con.GetDialect();
        }

        /// <summary>
        /// Configures the database and sets it up
        /// </summary>
        public virtual void AfterConfigure()
        {
            if (m_con == null)
                m_con = Ceen.Database.DatabaseHelper.CreateConnection(ConnectionString, ConnectionClass);

            // Create all required tables
            foreach (var t in UsedTypes ?? new Type[0])
                m_con.CreateTable(t);
        }
    }
}