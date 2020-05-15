using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ceen.Database
{
    /// <summary>
    /// Configuration options for the SQLite library
    /// </summary>
    public static class SQliteConfig
    {
        /// <summary>
        /// A flag indicating if the SQLite library is compiled with support for the LIMIT clause for DELETE
        /// </summary>
        /// <value></value>
        public static bool SupportsLimitOnDelete = false;

        /// <summary>
        /// A flag indicating if the SQLite library is compiled with support for the LIMIT clause for UPDATE
        /// </summary>
        /// <value></value>
        public static bool SupportsLimitOnUpdate = false;

        /// <summary>
        /// A flag indicating if the SQLite library is compiled with support for thread-safe access
        /// </summary>
        public static bool IsMultiThreadSafe = false;

    }

    /// <summary>
    /// The Implementation of the database dialect for SQLite
    /// </summary>
    public class DatabaseDialectSQLite : DatabaseDialectBase
    {
        /// <summary>
        /// The integer types
        /// </summary>
        private static readonly Type[] INTTYPES = { typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(long), typeof(ulong) };

        /// <summary>
        /// The SQLite library needs to be compiled with multi-threading support, and most distros package the non-thread-safe version.
        /// Later versions of SQLite do have methods for enabling threading, but no good interface to query it
        /// </summary>
        public override bool IsMultiThreadSafe => SQliteConfig.IsMultiThreadSafe;

        /// <summary>
        /// Gets the SQL type for a given property
        /// </summary>
        /// <returns>The sql column type.</returns>
        /// <param name="property">The property being examined.</param>
        public override Tuple<string, AutoGenerateAction> GetSqlColumnType(MemberInfo member)
        {
            var action = AutoGenerateAction.None;            
            if (member.GetCustomAttributes(true).OfType<CreatedTimestampAttribute>().Any())
                action = AutoGenerateAction.ClientCreateTimestamp;
            if (member.GetCustomAttributes(true).OfType<ChangedTimestampAttribute>().Any())
            {
                if (action != AutoGenerateAction.None)
                    throw new ArgumentException($"The {nameof(CreatedTimestampAttribute)} and {nameof(ChangedTimestampAttribute)} attributes are mutually exclusive");
                action = AutoGenerateAction.ClientChangeTimestamp;
            }

            var custom = member.GetCustomAttributes(true).OfType<DbTypeAttribute>().Select(x => x.Type).FirstOrDefault();
            if (custom != null)
                return new Tuple<string, AutoGenerateAction>(custom, action);

            Type elType;
            if (member is PropertyInfo pi)
                elType = pi.PropertyType;
            else if (member is FieldInfo fi)
                elType = fi.FieldType;
            else
                throw new Exception($"Unexpected member type: {member?.GetType()}");

            var isPrimaryKey = member.GetCustomAttributes<PrimaryKeyAttribute>(true).Any();
            var otherPrimaries = member.DeclaringType
                .GetProperties()
                .Select(x => x.GetCustomAttributes<PrimaryKeyAttribute>(true).Any())
                .Concat(
                    member.DeclaringType
                        .GetFields()
                        .Select(x => x.GetCustomAttributes<PrimaryKeyAttribute>(true).Any())
                )
                .Any(x => x);

            if (INTTYPES.Contains(elType))
            {
                // If this member is decorated primary, or named ID with no other primary key members,
                // we make this item the primary key
                if (isPrimaryKey || (member.Name == "ID" && !otherPrimaries && action == AutoGenerateAction.None))
                {
                    if (action != AutoGenerateAction.None)
                        throw new ArgumentException($"The primary key cannot also be a timestamp");

                    return new Tuple<string, AutoGenerateAction>("INTEGER PRIMARY KEY AUTOINCREMENT", AutoGenerateAction.DatabaseAutoID);
                }

                return new Tuple<string, AutoGenerateAction>("INTEGER", action);
            }
            else if (elType == typeof(DateTime))
                return new Tuple<string, AutoGenerateAction>("DATETIME", action);
            else if (elType == typeof(bool))
                return new Tuple<string, AutoGenerateAction>("BOOLEAN", action);
            else
            {
                if (isPrimaryKey || ((member.Name == "ID" || member.Name == "GUID") && !otherPrimaries && action == AutoGenerateAction.None))
                {
                    if (action != AutoGenerateAction.None)
                        throw new ArgumentException($"The primary key cannot also be a timestamp");

                    return new Tuple<string, AutoGenerateAction>("STRING PRIMARY KEY", AutoGenerateAction.ClientGenerateGuid);
                }

                return new Tuple<string, AutoGenerateAction>("STRING", action);
            }
        }


        /// <summary>
        /// Returns a create-table sql statement
        /// </summary>
        /// <param name="recordtype">The datatype to store in the table.</param>
        /// <param name="ifNotExists">Only create table if it does not exist</param>
        public override string CreateTableSql(Type recordtype, bool ifNotExists = true)
        {
            var mapping = GetTypeMap(recordtype);

            var fields =
                string.Join(", ", mapping
                            .AllColumns
                            .Select(x => string.Format(@"{0} {1}", QuoteName(x.ColumnName), x.SqlType))
                );

            var constr =
                (mapping.Uniques == null || mapping.Uniques.Length == 0)
                ? string.Empty
                : ", " + string.Join(", ",
                    mapping.Uniques.Select(x =>
                       string.Format($@"CONSTRAINT {QuoteName(mapping.Name + (x.Group == null ? string.Empty : EscapeName(x.Group)) + "_unique")} UNIQUE({string.Join(", ", x.Columns.Select(y => QuoteName(y.ColumnName)))})")
                    )
                );

            var sql = string.Format(
                @"CREATE TABLE{0} {1} ({2} {3}) ",
                ifNotExists ? " IF NOT EXISTS" : "",
                QuoteName(mapping.Name),
                fields,
                constr
            );

            return sql;
        }

        /// <summary>
        /// Returns a delete-table sql statement
        /// </summary>
        /// <param name="recordtype">The datatype to delete from the table.</param>
        /// <param name="ifExists">Only delete table if it exists</param>
        public override string DeleteTableSql(Type recordtype, bool ifExists = true)
        {
            var mapping = GetTypeMap(recordtype);

            return string.Format(
                @"DROP TABLE{0} {1}",
                ifExists ? " IF EXISTS" : "",
                QuoteName(mapping.Name)
            );
        }


        /// <summary>
        /// Creates a command that checks if a table exists
        /// </summary>
        /// <returns>The table exists command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        public override string CreateTableExistsCommand(Type type)
        {
            var mapping = GetTypeMap(type);
            return
                $"SELECT {QuoteName("name")} FROM sqlite_master WHERE type = {QuoteName("table")} AND name = {QuoteName(mapping.Name)}";
        }

        /// <summary>
        /// Creates a command that returns the names of the columns in a table
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <returns>The table column select command</returns>
        public override string CreateSelectTableColumnsSql(Type type)
        {
            var mapping = GetTypeMap(type);
            return 
                $"SELECT \"name\",\"type\" FROM pragma_table_info('{mapping.Name}')";

        }

        /// <summary>
        /// Creates a command that adds columns to a table
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <param name="columns">The columns to add</param>
        /// <returns>The command that adds columns</returns>
        public override string CreateAddColumnSql(Type type, IEnumerable<ColumnMapping> columns)
        {
            var mapping = GetTypeMap(type);
            if (columns == null || !columns.Any())
                throw new ArgumentException("Cannot create an SQL statement to add zero columns");

            return
                string.Join(";",
                    columns.Select(
                        x =>
                            $"ALTER TABLE {QuoteName(mapping.Name)} ADD COLUMN {QuoteName(x.ColumnName)} {x.SqlType}"
                    )
                );
        }

        /// <summary>
        /// Returns an OrderBy fragment
        /// </summary>
        /// <param name="type">The type to generate the clause for.</param>
        /// <param name="order">The order to render</param>
        /// <returns>The SQL order-by fragment</returns>
        public override string OrderBy(Type type, QueryOrder order)
        {
            var map = GetTypeMap(type);
            if (order == null)
                return string.Empty;

            var o = order;
            var sb = new System.Text.StringBuilder();
            while(o != null)
            {
                if (sb.Length != 0)
                    sb.Append(", ");
                sb.Append(map.QuotedColumnName(o.Property));
                sb.Append(o.Descending ? " DESC" : " ASC");
                o = o.Next;
            }

            if (sb.Length == 0)
                return string.Empty;

            return "ORDER BY " + sb.ToString();
        }

        /// <summary>
        /// Renders a full query clause
        /// </summary>
        /// <param name="type">The type the query is for</param>
        /// <param name="query">The query to render</param>
        /// <param name="finalize">Flag indicating if the complete method should be called on the query</param>
        /// <returns>The sql statement</returns>
        public override KeyValuePair<string, object[]> RenderStatement(Query query, bool finalize = true)
        {
            var q = query.Parsed;

            var map = GetTypeMap(q.DataType);
            if (finalize)
                q.Complete();

            if (q.Type == QueryType.Insert)
            {
                var cols = q.UpdateValues.Keys;
                var sql = $"INSERT{(q.IgnoresInsert ? " OR IGNORE " : " ")}INTO {map.QuotedTableName} ({string.Join(", ", cols.Select(x => map.QuotedColumnName(x)))}) VALUES ({string.Join(", ", cols.Select(x => "?"))})";

                if (map.IsPrimaryKeyAutogenerated)
                    sql += "; SELECT last_insert_rowid();";

                return new KeyValuePair<string, object[]>(
                    sql,
                    q.UpdateValues.Values.ToArray()
                );
            }

            var w = RenderWhereClause(q.DataType, q.WhereQuery);
            var order = OrderBy(q.DataType, q.OrderClause);
            var limit = string.Empty;
            if (q.LimitParams != null && q.LimitParams.Item1 > 0)
                limit = Limit(q.LimitParams.Item1, q.LimitParams.Item2 > 0 ? (int?)q.LimitParams.Item2 : null);

            var where = w.Key;
            var values = w.Value;
            
            if (!string.IsNullOrWhiteSpace(where))
                where = " " + where;
            if (!string.IsNullOrWhiteSpace(limit))
                limit = " " + limit;
            if (!string.IsNullOrWhiteSpace(order))
                order = " " + order;

            switch(q.Type)
            {
                case QueryType.Select:
                {
                    var cols = (q.SelectColumns ?? new string[0]).ToArray();
                    if (cols.Length == 0)
                        cols = map.AllColumnsByMemberName.Keys.ToArray();

                    return new KeyValuePair<string, object[]>(
                        $"SELECT {string.Join(", ", cols.Select(x => map.QuotedColumnName(x)))} FROM {map.QuotedTableName}{where}{order}{limit}",
                        w.Value
                    );
                }
                case QueryType.Delete:
                    if (SQliteConfig.SupportsLimitOnDelete || (string.IsNullOrWhiteSpace(order) && string.IsNullOrWhiteSpace(limit)))
                    {
                        return new KeyValuePair<string, object[]>(
                            $"DELETE FROM {map.QuotedTableName}{where}{order}{limit}",
                            w.Value
                        );
                    }
                    else
                    {
                        return new KeyValuePair<string, object[]>(
                            $"DELETE FROM {map.QuotedTableName} WHERE rowid IN (SELECT rowid FROM {map.QuotedTableName}{where}{order}{limit})",
                            w.Value
                        );
                    }
                case QueryType.Update:
                {
                    var cols = q.UpdateValues.Keys;

                    if (SQliteConfig.SupportsLimitOnUpdate || (string.IsNullOrWhiteSpace(order) && string.IsNullOrWhiteSpace(limit)))
                    {
                        return new KeyValuePair<string, object[]>(
                            $"UPDATE {map.QuotedTableName} SET {string.Join(", ", cols.Select(x => map.QuotedColumnName(x) + " = ?"))}{where}{order}{limit}",
                            q.UpdateValues.Values.Concat(w.Value).ToArray()
                        );
                    }
                    else
                    {
                        return new KeyValuePair<string, object[]>(
                            $"UPDATE {map.QuotedTableName} SET {string.Join(", ", cols.Select(x => map.QuotedColumnName(x) + " = ?"))} WHERE rowid IN (SELECT rowid FROM {map.QuotedTableName}{where}{order}{limit})",
                            q.UpdateValues.Values.Concat(w.Value).ToArray()
                        );
                    }

                }
                default:
                    throw new Exception($"Unsupported query type: {q.Type}");
            }

        }

        /// <summary>
        /// Returns a where fragment that limits the query
        /// </summary>
        /// <param name="offset">The optional offset to use</param>
        /// <param name="limit">The maximum number of items to use</param>
        /// <returns>The limit fragment</returns>
        public override string Limit(long limit, long? offset)
        {
            if (offset == null)
                return $"LIMIT {limit}";

            return $"LIMIT {limit} OFFSET {offset.Value}";
        }

        /// <summary>
        /// Parses a query element and returns the SQL and arguments
        /// </summary>
        /// <param name="type">The type to query</param>
        /// <param name="element">The query element</param>
        /// <returns>The parsed query and the arguments</returns>
        public override KeyValuePair<string, object[]> RenderWhereClause(Type type, QueryElement element)
        {
            var lst = new List<object>();
            var q = RenderWhereClause(type, element, lst);
            if (!string.IsNullOrWhiteSpace(q))
                q = "WHERE " + q;
            return new KeyValuePair<string, object[]>(q, lst.ToArray());
        }

        /// <summary>
        /// Renders an SQL where clause from a query element
        /// </summary>
        /// <param name="element">The element to use</param>
        /// <returns>The SQL where clause</returns>
        private string RenderWhereClause(Type type, object element, List<object> args)
        {
            if (element == null || element is Empty)
                return string.Empty;

            if (element is And andElement)
                return string.Join(
                    " AND ", 
                    andElement
                        .Items
                        .Select(x => RenderWhereClause(type, x, args))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => $"({x})")
                );
            else if (element is Or orElement)
                return string.Join(
                    " OR ",
                    orElement
                        .Items
                        .Select(x => RenderWhereClause(type, x, args))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => $"({x})")
                );
            else if (element is Property property)
                return GetTypeMap(type).QuotedColumnName(property.PropertyName);
            else if (element is UnaryOperator unop)
                return $"{unop.Operator} ({RenderWhereClause(type, unop.Expression, args)})";
            else if (element is ParenthesisExpression pex)
                return $"({RenderWhereClause(type, pex.Expression, args)})";
            else if (element is CustomQuery cq)
            {
                args.AddRange(cq.Arguments ?? new object[0]);
                return cq.Value;
            }
            else if (element is Compare compare)
            {
                if (
                    string.Equals(compare.Operator, "IN", StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(compare.Operator, "NOT IN", StringComparison.OrdinalIgnoreCase)
                )
                {
                    // Support for "IN" with sub-query
                    if (compare.RightHandSide is Query rhq) 
                    {
                        if (rhq.Parsed.Type != QueryType.Select)
                            throw new ArgumentException("The query must be a select statement for exactly one column", nameof(compare.RightHandSide));
                        if (rhq.Parsed.SelectColumns.Count() != 1)
                            throw new ArgumentException("The query must be a select statement for exactly one column", nameof(compare.RightHandSide));

                        var rvp = RenderStatement(rhq);
                        args.AddRange(rvp.Value);
                        return $"{RenderWhereClause(type, compare.LeftHandSide, args)} {compare.Operator} ({rvp.Key})";
                    }

                    var rhsel = compare.RightHandSide;
                    IEnumerable items = null;
                    
                    // Unwrap a list in parenthesis
                    if (rhsel is ParenthesisExpression rhspe)
                    {
                        var ve = (rhspe.Expression is Value rhspev) ? rhspev.Item : rhspe.Expression;
                        if (ve is IEnumerable enve)
                            items = enve;
                        else
                        {
                            var a = Array.CreateInstance(ve?.GetType() ?? typeof(object), 1);
                            a.SetValue(ve, 0);
                            items = a;
                        }
                    }
                    // If no parenthesis, look for a sequence inside
                    if (items == null && compare.RightHandSide is Value rhsv)
                        items = rhsv.Item as IEnumerable;
                    // No value, check for sequnence as a plain object
                    if (items == null && compare.RightHandSide is IEnumerable rhsen)
                        items = rhsen;

                    // Bounce back attempts to use a string as a char[] sequence (it implements IEnumerable)
                    if (items is string its)
                        items = new string[] { its };

                    if (items == null)
                        return RenderWhereClause(type, QueryUtil.Equal(compare.LeftHandSide, null), args);

                    var op = 
                        string.Equals(compare.Operator, "IN", StringComparison.OrdinalIgnoreCase)
                        ? "="
                        : "!=";

                    // Special handling of null in lists
                    if (items.Cast<object>().Any(x => x == null))
                        return RenderWhereClause(
                            type, 
                            QueryUtil.Or(
                                QueryUtil.In(compare.LeftHandSide, items.Cast<object>().Where(x => x != null)),
                                QueryUtil.Compare(compare.LeftHandSide, op, null)
                            ),
                            args
                        );

                    // No nulls, just return plain "IN" or "NOT IN"

                    // Does not work, it does not bind correctly to the array for some reason
                    // args.Add(items);
                    // return $"{RenderWhereClause(type, compare.LeftHandSide, args)} {compare.Operator} (?)";
                    
                    // Workaround is to expand to comma separated list
                    var qs = new List<string>();
                    foreach (var n in items)
                    {
                        args.Add(n);
                        qs.Add("?");
                    }
                    return $"{RenderWhereClause(type, compare.LeftHandSide, args)} {compare.Operator} ({string.Join(",", qs)})";
                }

                // Extract the arguments, if they are arguments
                var lhs = compare.LeftHandSide is Value lhsVal ? lhsVal.Item : compare.LeftHandSide;
                var rhs = compare.RightHandSide is Value rhsVal ? rhsVal.Item : compare.RightHandSide;

                // Special handling for enums, as they are string serialized in the database
                if (IsQueryItemEnum(type, lhs) || IsQueryItemEnum(type, rhs))
                {
                    if (!new string[] {"=", "LIKE", "!=", "NOT LIKE"}.Any(x => string.Equals(x, compare.Operator, StringComparison.InvariantCultureIgnoreCase)))
                        throw new ArgumentException("Can only compare enums with equal or not equal as they are stored as strings in the database");

                    // Force enum arguments to strings
                    if (lhs != null && !(lhs is QueryElement))
                        lhs = lhs.ToString();
                    if (rhs != null && !(rhs is QueryElement))
                        rhs = rhs.ToString();
                }

                // Special handling of null values to be more C# like
                var anyNulls = lhs == null || rhs == null;

                // Rewire gteq and lteq to handle nulls like C#
                if (anyNulls && string.Equals(compare.Operator, "<="))
                    return RenderWhereClause(type, 
                        QueryUtil.Or(
                            QueryUtil.Compare(lhs, "<", rhs),
                            QueryUtil.Compare(lhs, "=", rhs)
                        )
                    , args);

                if (anyNulls && string.Equals(compare.Operator, ">="))
                    return RenderWhereClause(type,
                        QueryUtil.Or(
                            QueryUtil.Compare(lhs, ">", rhs),
                            QueryUtil.Compare(lhs, "=", rhs)
                        )
                    , args);

                // Rewire compare operator to also match nulls
                if (anyNulls && (string.Equals(compare.Operator, "=") || string.Equals(compare.Operator, "LIKE", StringComparison.OrdinalIgnoreCase)))
                {
                    if (lhs == null)
                        return $"{RenderWhereClause(type, rhs, args)} IS NULL";
                    else
                        return $"{RenderWhereClause(type, lhs, args)} IS NULL";
                }

                if (anyNulls && (string.Equals(compare.Operator, "!=") || string.Equals(compare.Operator, "NOT LIKE", StringComparison.OrdinalIgnoreCase)))
                {
                    if (lhs == null)
                        return $"{RenderWhereClause(type, rhs, args)} IS NOT NULL";
                    else
                        return $"{RenderWhereClause(type, lhs, args)} IS NOT NULL";
                }

                return $"{RenderWhereClause(type, lhs, args)} {compare.Operator} {RenderWhereClause(type, rhs, args)}";
            }
            else if (element is Value ve)
            {
                args.Add(ve.Item);
                return "?";
            }
            else if (element is Arithmetic arithmetic)
            {
                return $"{RenderWhereClause(type, arithmetic.LeftHandSide, args)} {arithmetic.Operator} {RenderWhereClause(type, arithmetic.RightHandSide, args)}";
            }
            else if (element is QueryElement)
            {
                throw new Exception($"Unexpected query element: {element.GetType()}");
            }
            else
            {
                args.Add(element);
                return "?";
            }
        }

        /// <summary>
        /// Checks if a query item is an enum
        /// </summary>
        /// <param name="type">The type used for properties</param>
        /// <param name="item">The item to check</param>
        /// <returns>A value indicating if the item is an enum type</returns>
        private bool IsQueryItemEnum(Type type, object item)
        {
            if (item is Value v)
                item = v.Item;

            if (item == null)
                return false;

            if (item is Property p)
                return GetTypeMap(type).AllColumnsByMemberName[p.PropertyName].MemberType.IsEnum;

            if (item is QueryElement)
                return false;

            return item.GetType().IsEnum;
        }
    }
}
