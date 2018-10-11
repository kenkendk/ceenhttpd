using System;
using System.Data;
using System.Linq;

namespace Ceen.Database
{
    public static class DbSupport
    {
        /// <summary>
        /// Helper method for creating a command and initializing the parameters
        /// </summary>
        /// <returns>The command.</returns>
        /// <param name="commandtext">The commandtext.</param>
        public static IDbCommand SetupCommand(this IDbConnection connection, string commandtext, IDbTransaction transaction = null)
        {
            if (string.IsNullOrWhiteSpace(commandtext))
                throw new ArgumentNullException(nameof(commandtext));

            var cmd = connection.CreateCommand();
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
        public static void AddParameters(this IDbCommand cmd, int count)
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
        public static void SetParameterValues(this IDbCommand cmd, params object[] values)
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
        public static string GetAsString(this IDataReader rd, int index)
        {
            var val = rd.GetValue(index);

            if (val == null || val == DBNull.Value)
                return null;
            else
                return (string)val;
        }

        /// <summary>
        /// Creates a new command with the given command text
        /// </summary>
        /// <returns>The new command.</returns>
        /// <param name="connection">The connection to create the command on.</param>
        /// <param name="command">The command text to use.</param>
        /// <param name="transaction">The transaction to use.</param>
        public static IDbCommand CreateCommand(this IDbConnection connection, string command, IDbTransaction transaction = null)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            if (command != null)
                cmd.CommandText = command;
            return cmd;
        }

        /// <summary>
        /// Creates a new command with the given command text
        /// </summary>
        /// <returns>The new command.</returns>
        /// <param name="connection">The connection to create the command on.</param>
        /// <param name="command">The command text to use.</param>
        /// <param name="transaction">The transaction to use.</param>
        public static IDbCommand CreateCommandWithParameters(this IDbConnection connection, string command, IDbTransaction transaction = null)
        {
            var cmd = connection.CreateCommand(command ?? throw new ArgumentNullException(nameof(command)), transaction);
            AddParameters(cmd, command.Count(x => x == '?'));
            return cmd;
        }

        /// <summary>
        /// Executes the command and returns the reader
        /// </summary>
        /// <returns>The reader.</returns>
        /// <param name="command">The command to execute.</param>
        /// <param name="values">The values to set.</param>
        public static IDataReader ExecuteReader(this IDbCommand command, params object[] values)
        {
            SetParameterValues(command, values);
            return command.ExecuteReader();
        }

        /// <summary>
        /// Executes the command and returns the reader
        /// </summary>
        /// <returns>The reader.</returns>
        /// <param name="command">The command to execute.</param>
        /// <param name="values">The values to set.</param>
        public static int ExecuteNonQuery(this IDbCommand command, params object[] values)
        {
            SetParameterValues(command, values);
            return command.ExecuteNonQuery();
        }
    }
}
