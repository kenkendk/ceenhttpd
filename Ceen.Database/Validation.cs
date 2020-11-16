using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Ceen.Database
{
    /// <summary>
    /// Class to aid in verifying that the tables are in a state that is compatible with the code types
    /// </summary>
    public static class Validation
    {
        /// <summary>
        /// Performs basic table validation
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="targets">The types to evaluate</param>
        public static void ValidateTables(this IDbConnection connection, Type[] targets)
        {
            foreach (var t in targets)
                ValidateTable(connection, t);
        }

        /// <summary>
        /// Performs basic table validation
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="target">The type to evaluate</param>
        public static void ValidateTable(this IDbConnection connection, Type target)
        {
            var dialect = connection.GetDialect();
            var mapping = connection.GetTypeMap(target);

            var sql = dialect.CreateSelectTableColumnsSql(target);
            var columns = new List<string>();
            using (var cmd = connection.CreateCommand(sql))
            using (var rd = cmd.ExecuteReader())
                while (rd.Read())
                    columns.Add(rd.GetAsString(0));

            var missingcolumns = mapping.AllColumns.Where(x => !columns.Contains(x.ColumnName)).ToArray();
            if (missingcolumns.Length != 0)
                throw new DataException($"The table {mapping.Name} is missing the column(s): {string.Join(", ", missingcolumns.Select(x => x.ColumnName))}");

            foreach (var item in mapping.AllColumns.Where(x => x.MemberType.IsEnum))
            {
                var invalidnames = new List<string>();
                var names = Enum.GetNames(item.MemberType);
                using (var cmd = connection.CreateCommand($"SELECT DISTINCT {item.QuotedColumnName} FROM {mapping.QuotedTableName} WHERE {item.QuotedColumnName} NOT IN ({string.Join(",", names.Select(x => "?")) })"))
                using (var rd = cmd.ExecuteReader(names.AsEnumerable()))
                    while (rd.Read())
                        invalidnames.Add(rd.GetAsString(0));

                if (invalidnames.Count > 0)
                    throw new DataException($"The table {mapping.Name} has the following invalid value(s) for column {item.ColumnName} (of type {item.MemberType}): {string.Join(", ", invalidnames)}");
            }
        }
    }
}
