using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Ceen;
using Ceen.Database;

namespace Ceen.Extras
{
    /// <summary>
    /// A helper class that wraps the current connection with transactions and a single-use lock if the dialect requires it
    /// </summary>
    public class GuardedConnection
    {
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
        protected IDbConnection m_con;

        /// <summary>
        /// The lock guarding the database
        /// </summary>
        protected AsyncLock m_lock = new AsyncLock();

        /// <summary>
        /// Returns a value indicating if the database module is initialized
        /// </summary>
        public bool IsInitialized => m_con != null;

        /// <summary>
        /// Provides direct access to the underlying connection
        /// </summary>
        public IDbConnection UnguardedConnection => m_con;

        /// <summary>
        /// Gets the dialect for the current connection
        /// </summary>
        /// <returns>The dialect for the connection</returns>
        public Ceen.Database.IDatabaseDialect Dialect => m_dialect;

        /// <summary>
        /// A cached reference to the dialect for the connection
        /// </summary>
        private readonly IDatabaseDialect m_dialect;

        /// <summary>
        /// A single allocated lock task for thread-safe connections
        /// </summary>
        private readonly Task<AsyncLock.Releaser> m_locker;

        /// <summary>
        /// Constructs a new guarded connection
        /// </summary>
        /// <param name="connection">The connection to use</param>
        public GuardedConnection(IDbConnection connection)
        {
            m_con = connection ?? throw new ArgumentNullException(nameof(connection));
            if (m_con.State != ConnectionState.Open)
                m_con.Open();

            if (m_con.State != ConnectionState.Open)
                throw new ArgumentException("The database connection was not open and did not open");

            m_locker = (m_dialect = m_con.GetDialect()).IsMultiThreadSafe ? new Task<AsyncLock.Releaser>(null) : null;
        }

        /// <summary>
        /// Constructs a new guarded connection
        /// </summary>
        /// <param name="connectionString">The connection string to use</param>
        /// <param name="connectionClass">The connection class to use</param>
        public GuardedConnection(string connectionString, string connectionClass)
            : this(Ceen.Database.DatabaseHelper.CreateConnection(connectionString, connectionClass))
        {
        }

        /// <summary>
        /// Returns an awaitable lock task
        /// </summary>
        private Task<AsyncLock.Releaser> LockIfRequiredAsync() => m_locker ?? m_lock.LockAsync();

        /// <summary>
        /// Runs the given method in a transaction
        /// </summary>
        /// <param name="method">The method to run</param>
        /// <returns>An awaitable task</returns>
        public async Task RunInTransactionAsync(Func<IDbConnection, Task> method)
        {
            using (await LockIfRequiredAsync())
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
        public async Task<T> RunInTransactionAsync<T>(Func<IDbConnection, Task<T>> method)
        {
            using (await LockIfRequiredAsync())
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
        public async Task<T> RunInTransactionAsync<T>(Func<IDbConnection, T> method)
        {
            using (await LockIfRequiredAsync())
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
        public async Task RunInTransactionAsync(Action<IDbConnection> method)
        {
            using (await LockIfRequiredAsync())
            using (var tr = m_con.BeginTransaction())
            {
                method(m_current.Value = new Ceen.Database.TransactionConnection(m_con, tr));
                tr.Commit();
            }
        }

        /// <summary>
        /// Returns a new empty query
        /// </summary>
        /// <typeparam name="T">The type of query to use</typeparam>
        /// <returns>The query</returns>
        public Query<T> Query<T>() => new Query<T>(m_dialect);

    }
}
