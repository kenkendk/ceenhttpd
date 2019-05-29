using System;
using System.Data;

namespace Ceen.Database
{
    /// <summary>
    /// Helper class for passing around a connection with an active transaction
    /// </summary>
    public class TransactionConnection : IDbConnection, IDbTransaction
    {
        /// <summary>
        /// The connection element
        /// </summary>
        private readonly IDbConnection m_connection = null;
        /// <summary>
        /// The transaction element
        /// </summary>
        private IDbTransaction m_transaction = null;

        /// <summary>
        /// The connection
        /// </summary>
        public IDbConnection Connection => m_connection;
        /// <summary>
        /// The transaction
        /// </summary>
        public IDbTransaction Transaction => m_transaction;

        /// <summary>
        /// Creates a new connection wrapper
        /// </summary>
        /// <param name="connection">The connection to wrap</param>
        public TransactionConnection(IDbConnection connection)
        {
            m_connection = connection;
        }

        /// <summary>
        /// Creates a new connection wrapper
        /// </summary>
        /// <param name="connection">The connection to wrap</param>
        /// <param name="transaction">The transaction to use</param>
        public TransactionConnection(IDbConnection connection, IDbTransaction transaction)
        {
            m_connection = connection;
            m_transaction = transaction;
        }

        /// <inheritdoc />
        public IDbTransaction BeginTransaction(IsolationLevel il) => m_transaction = m_connection.BeginTransaction(il);
        /// <inheritdoc />
        public IDbTransaction BeginTransaction() => m_transaction = m_connection.BeginTransaction();
        /// <inheritdoc />
        public int ConnectionTimeout => m_connection.ConnectionTimeout;
        /// <inheritdoc />
        public IDbCommand CreateCommand()
        {
             var cmd = m_connection.CreateCommand();
             cmd.Transaction = m_transaction;
             return cmd;

        }
        /// <inheritdoc />
        public string Database => m_connection.Database;
        /// <inheritdoc />
        public ConnectionState State => m_connection.State;
        /// <inheritdoc />
        public void Dispose() => m_transaction.Dispose();

        /// <inheritdoc />
        public IsolationLevel IsolationLevel => m_transaction.IsolationLevel;
        /// <inheritdoc />
        public void Commit() => m_transaction.Commit();
        /// <inheritdoc />
        public void Rollback() => m_transaction.Rollback();

        /// <inheritdoc />
        public string ConnectionString
        {
            get => m_connection.ConnectionString;
            set => m_connection.ConnectionString = value;
        }

        /// <inheritdoc />
        public void ChangeDatabase(string databaseName) => m_connection.ChangeDatabase(databaseName);
        /// <inheritdoc />
        public void Close() => m_connection.Close();
        /// <inheritdoc />
        public void Open() => m_connection.Open();
    }
}
