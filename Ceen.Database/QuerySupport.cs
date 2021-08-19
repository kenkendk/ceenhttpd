using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace Ceen.Database
{
    /// <summary>
    /// Helper class for constructing a where clause programatically.
    /// This class should be used with:
    /// using static Ceen.Database.QueryUtil
    /// </summary>
    public static class QueryUtil
    {
        /// <summary>
        /// Returns an empty query
        /// </summary>
        /// <returns>The empty query</returns>
        public static Empty Empty => new Empty();

        /// <summary>
        /// Creates a new query order (ascending)
        /// </summary>
        /// <param name="name">The property to order by</param>
        /// <param name="next">The next order property</param>
        /// <returns>The query order</returns>
        public static QueryOrder Order(string name, QueryOrder next = null) => new QueryOrder(name, false, next);
        /// <summary>
        /// Creates a new ascending query order
        /// </summary>
        /// <param name="name">The property to order by</param>
        /// <param name="next">The next order property</param>
        /// <returns>The query order</returns>
        public static QueryOrder OrderAsc(string name, QueryOrder next = null) => new QueryOrder(name, false, next);
        /// <summary>
        /// Creates a new descending query order
        /// </summary>
        /// <param name="name">The property to order by</param>
        /// <param name="next">The next order property</param>
        /// <returns>The query order</returns>
        public static QueryOrder OrderDesc(string name, QueryOrder next = null) => new QueryOrder(name, true, next);

        /// <summary>
        /// Constructs an And sequence
        /// </summary>
        /// <param name="args">The arguments to and together</param>
        /// <returns>A query element</returns>
        public static QueryElement And(params QueryElement[] args) => new And(args);
        /// <summary>
        /// Constructs an And sequence
        /// </summary>
        /// <param name="args">The arguments to and together</param>
        /// <returns>A query element</returns>
        public static QueryElement And(IEnumerable<QueryElement> args) => new And(args);
        /// <summary>
        /// Constructs an or sequence
        /// </summary>
        /// <param name="args">The arguments to or together</param>
        /// <returns>A query element</returns>
        public static QueryElement Or(params QueryElement[] args) => new Or(args);
        /// <summary>
        /// Constructs an or sequence
        /// </summary>
        /// <param name="args">The arguments to or together</param>
        /// <returns>A query element</returns>
        public static QueryElement Or(IEnumerable<QueryElement> args) => new Or(args);

        /// <summary>
        /// Constructs a property access
        /// </summary>
        /// <param name="name">The name of the property to query</param>
        /// <returns>A query element</returns>
        public static QueryElement Property(string name) => new Property(name);
        /// <summary>
        /// Compares two items for equality
        /// </summary>
        /// <param name="lhs">Either a property or a value</param>
        /// <param name="rhs">Either a property or a value</param>
        /// <returns>A query element</returns>
        public static QueryElement Equal(object lhs, object rhs) => new Compare(lhs, "=", rhs);
        /// <summary>
        /// Compares two items for equality
        /// </summary>
        /// <param name="lhs">Either a property or a value</param>
        /// <param name="rhs">Either a property or a value</param>
        /// <returns>A query element</returns>
        public static QueryElement Like(object lhs, object rhs) => new Compare(lhs, "LIKE", rhs);
        /// <summary>
        /// Compares two items with the given operator
        /// </summary>
        /// <param name="lhs">Either a property or a value</param>
        /// <param name="operator">The operator to use</param>
        /// <param name="rhs">Either a property or a value</param>
        /// <returns>A query element</returns>
        public static QueryElement Compare(object lhs, string @operator, object rhs) => new Compare(lhs, @operator, rhs);
        /// <summary>
        /// Applies an arithmetic operator to two operands
        /// </summary>
        /// <param name="lhs">Either a property or a value</param>
        /// <param name="operator">The operator to use</param>
        /// <param name="rhs">Either a property or a value</param>
        /// <returns>A query element</returns>
        public static QueryElement Arithmetic(object lhs, string @operator, object rhs)
        {
            // Check if we can reduce this to a single value
            if ((lhs is Value || !(lhs is QueryElement)) && (rhs is Value || !(rhs is QueryElement)))
            {
                var lh = (lhs is Value lval) ? lval.Item : lhs;
                var rh = (rhs is Value rval) ? rval.Item : rhs;
                if (IsNumericType(lh) && IsNumericType(rh))
                {
                    if (lh is double || rh is double)
                    {
                        var dbl = (double)Convert.ChangeType(lh, typeof(double));
                        var dbr = (double)Convert.ChangeType(rh, typeof(double));

                        switch (@operator)
                        {
                            case "+": return new Value(dbl + dbr);
                            case "-": return new Value(dbl - dbr);
                            case "*": return new Value(dbl * dbr);
                            case "/": return new Value(dbl / dbr);
                            case "%": return new Value(dbl % dbr);
                        }
                    }
                    else if (lh is float || rh is float)
                    {
                        var dbl = (float)Convert.ChangeType(lh, typeof(float));
                        var dbr = (float)Convert.ChangeType(rh, typeof(float));

                        switch (@operator)
                        {
                            case "+": return new Value(dbl + dbr);
                            case "-": return new Value(dbl - dbr);
                            case "*": return new Value(dbl * dbr);
                            case "/": return new Value(dbl / dbr);
                            case "%": return new Value(dbl % dbr);
                        }
                    }
                    else if (lh is ulong || rh is ulong)
                    {
                        var dbl = (ulong)Convert.ChangeType(lh, typeof(ulong));
                        var dbr = (ulong)Convert.ChangeType(rh, typeof(ulong));

                        switch (@operator)
                        {
                            case "+": return new Value(dbl + dbr);
                            case "-": return new Value(dbl - dbr);
                            case "*": return new Value(dbl * dbr);
                            case "/": return new Value(dbl / dbr);
                            case "%": return new Value(dbl % dbr);
                        }
                    }
                    else
                    {
                        var dbl = (long)Convert.ChangeType(lh, typeof(long));
                        var dbr = (long)Convert.ChangeType(rh, typeof(long));

                        switch (@operator)
                        {
                            case "+": return new Value(dbl + dbr);
                            case "-": return new Value(dbl - dbr);
                            case "*": return new Value(dbl * dbr);
                            case "/": return new Value(dbl / dbr);
                            case "%": return new Value(dbl % dbr);
                        }

                    }
                }
                else if ((lh is DateTime || lh is TimeSpan) && (rh is DateTime || rh is TimeSpan))
                {
                    if (lh is DateTime && rh is DateTime)
                    {
                        switch (@operator)
                        {
                            case "-": return new Value((DateTime)lh - (DateTime)rh);
                        }
                    }
                    else if (lh is TimeSpan && rh is TimeSpan)
                    {
                        switch (@operator)
                        {
                            case "+": return new Value((TimeSpan)lh + (TimeSpan)rh);
                            case "-": return new Value((TimeSpan)lh - (TimeSpan)rh);
                        }
                    }
                    else if (lh is DateTime && rh is TimeSpan)
                    {
                        switch (@operator)
                        {
                            case "+": return new Value((DateTime)lh + (TimeSpan)rh);
                            case "-": return new Value((DateTime)lh - (TimeSpan)rh);
                        }
                    }
                }
            }

            // Unable to shorten, just return "as-is"
            return new Arithmetic(lhs, @operator, rhs);
        }

        /// <summary>
        /// Checks if the item is a numeric type
        /// </summary>
        /// <param name="o">The item to check</param>
        /// <returns><c>true</c> if the item is numeric, <c>false</c> otherwise</returns>
        private static bool IsNumericType(object o)
        {
            if (o == null)
                return false;

            switch (Type.GetTypeCode(o.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                //case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Adds two operands
        /// </summary>
        /// <param name="lhs">Either a property or a value</param>
        /// <param name="rhs">Either a property or a value</param>
        /// <returns>A query element</returns>
        public static QueryElement Add(object lhs, object rhs) => Arithmetic(lhs, "+", rhs);
        /// <summary>
        /// Subtracts two operands
        /// </summary>
        /// <param name="lhs">Either a property or a value</param>
        /// <param name="rhs">Either a property or a value</param>
        /// <returns>A query element</returns>
        public static QueryElement Subtract(object lhs, object rhs) => Arithmetic(lhs, "-", rhs);
        /// <summary>
        /// Multiplies two operands
        /// </summary>
        /// <param name="lhs">Either a property or a value</param>
        /// <param name="rhs">Either a property or a value</param>
        /// <returns>A query element</returns>
        public static QueryElement Multiply(object lhs, object rhs) => Arithmetic(lhs, "*", rhs);
        /// <summary>
        /// Divides two operands
        /// </summary>
        /// <param name="lhs">Either a property or a value</param>
        /// <param name="rhs">Either a property or a value</param>
        /// <returns>A query element</returns>
        public static QueryElement Divide(object lhs, object rhs) => Arithmetic(lhs, "/", rhs);
        /// <summary>
        /// Computes the modulo two operands
        /// </summary>
        /// <param name="lhs">Either a property or a value</param>
        /// <param name="rhs">Either a property or a value</param>
        /// <returns>A query element</returns>
        public static QueryElement Modulo(object lhs, object rhs) => Arithmetic(lhs, "%", rhs);

        /// <summary>
        /// Checks if an element is in a list
        /// </summary>
        /// <param name="lhs">The item to examine</param>
        /// <param name="args">The list of options</param>
        /// <returns>A query element</returns>
        public static QueryElement In(object lhs, IEnumerable<object> args) => new Compare(lhs, "IN", args);
        /// <summary>
        /// Checks if an element is not in a list
        /// </summary>
        /// <param name="lhs">The item to examine</param>
        /// <param name="args">The list of options</param>
        /// <returns>A query element</returns>
        public static QueryElement NotIn(object lhs, IEnumerable<object> args) => new Compare(lhs, "NOT IN", args);

        /// <summary>
        /// Checks if an element is in a list
        /// </summary>
        /// <param name="lhs">The item to examine</param>
        /// <param name="other">The query to check if the value is in</param>
        /// <returns>A query element</returns>
        public static QueryElement In(object lhs, Query other) => new Compare(lhs, "IN", other);
        /// <summary>
        /// Checks if an element is in a list
        /// </summary>
        /// <param name="lhs">The item to examine</param>
        /// <param name="other">The query to check if the value is not in</param>
        /// <returns>A query element</returns>
        public static QueryElement NotIn(object lhs, Query other) => new Compare(lhs, "NOT IN", other);


        /// <summary>
        /// Negates an expression
        /// </summary>
        /// <param name="expr">The expression to negate</param>
        /// <returns>A query element</returns>
        public static QueryElement Not(object expr) => new UnaryOperator("not", expr);

        /// <summary>
        /// Accepts a anonymous object where the properties are column names, 
        /// and the values are the values to compare the properties to.
        /// Produces an and query for all items
        /// </summary>
        /// <param name="arg">The anonymous object to inspect</param>
        /// <returns>A query element</returns>
        public static QueryElement Equal(object arg)
        {
            var props = arg.GetType().GetProperties();
            if (props.Length == 1)
                return Compare(
                    Property(props.First().Name),
                    "=",
                    props.First().GetValue(arg)
                );

            return MultipleAnd(arg);
        }

        /// <summary>
        /// Accepts a anonymous object where the properties are column names, 
        /// and the values are the values to compare the properties to.
        /// Produces an and query for all items
        /// </summary>
        /// <param name="arg">The anonymous object to inspect</param>
        /// <param name="@operator">The compare operator to use</param>
        /// <returns>A query element</returns>
        public static QueryElement MultipleAnd(object args, string @operator = "=")
        {
            return And(
                args
                    .GetType()
                    .GetProperties()
                    .Select(x => 
                        Compare(
                            Property(x.Name),
                            @operator,
                            x.GetValue(args)
                        )
                    )
            );
        }

        /// <summary>
        /// Accepts a anonymous object where the properties are column names, 
        /// and the values are the values to compare the properties to.
        /// Produces an or query for all items
        /// </summary>
        /// <param name="arg">The anonymous object to inspect</param>
        /// <param name="@operator">The compare operator to use</param>
        /// <returns>A query element</returns>
        public static QueryElement MultipleOr(object args, string @operator = "=")
        {
            return Or(
                args
                    .GetType()
                    .GetProperties()
                    .Select(x =>
                        Compare(
                            Property(x.Name),
                            @operator,
                            x.GetValue(args)
                        )
                    )
            );
        } 

        /// <summary>
        /// Parses a lambda expression and returns a query
        /// </summary>
        /// <param name="expr">The expression to parse</param>
        /// <typeparam name="T">The target type</typeparam>
        /// <returns>A query element</returns>
        public static QueryElement FromLambda<T>(Expression<Func<T, bool>> expr)
        {
            return FromLambda(expr.Body, expr.Parameters.First());
        }

        /// <summary>
        /// Handles parsing a lambda fragment with an enum compare, and unwraps the type casts
        /// </summary>
        /// <param name="bx"></param>
        /// <param name="@operator"></param>
        /// <param name="methodtarget"></param>
        /// <returns></returns>
        private static QueryElement UnwrapCompare(BinaryExpression bx, string @operator, ParameterExpression methodtarget)
        {
            var lhs = bx.Left;
            var rhs = bx.Right;

            if (lhs is UnaryExpression luex && lhs.NodeType == ExpressionType.Convert)
                lhs = luex.Operand;

            if (rhs is UnaryExpression ruex && rhs.NodeType == ExpressionType.Convert)
                rhs = ruex.Operand;

            var lhsmtype = GetMemberType(lhs);
            var rhsmtype = GetMemberType(rhs);

            var lqp = FromLambda(lhs, methodtarget);
            var rqp = FromLambda(rhs, methodtarget);

            // If we unwrapped the left-hand side due to enum conversion, fix the right-hand side
            if (lhs != bx.Left && lhsmtype != null && lhsmtype.IsEnum && rqp is Value rqpv && rqpv.Item != null)
            {
                if (rqpv.Item is string vs)
                    rqp = new Value(Enum.Parse(lhsmtype, vs));
                else
                    rqp = new Value(Enum.ToObject(lhsmtype, rqpv.Item));
            }

            // If we unwrapped the right-hand side due to enum conversion, fix the left-hand side
            if (rhs != bx.Left && rhsmtype != null && rhsmtype.IsEnum && lqp is Value lqpv && lqpv.Item != null)
            {
                if (lqpv.Item is string vs)
                    lqp = new Value(Enum.Parse(rhsmtype, vs));
                else
                    lqp = new Value(Enum.ToObject(rhsmtype, lqpv.Item));
            }

            return Compare(lqp, @operator, rqp);
        }

        private static Type GetMemberType(Expression e)
        {
            if (e is MemberExpression me)
            {
                if (me.Member is System.Reflection.PropertyInfo pi)
                    return pi.PropertyType;
                else if (me.Member is System.Reflection.FieldInfo fi)
                    return fi.FieldType;
            }

            return null;
        }

        /// <summary>
        /// Parses an expression as a query element
        /// </summary>
        /// <param name="expr">The expression to parse</param>
        /// <param name="methodtarget">The parameter used as input for the lambda</param>
        /// <returns>A query element</returns>
        private static QueryElement FromLambda(Expression expr, ParameterExpression methodtarget)
        {
            if (expr is BinaryExpression bexpr)
            {
                switch (bexpr.NodeType)
                {
                    case ExpressionType.Equal:
                        return UnwrapCompare(bexpr, "=", methodtarget);
                    case ExpressionType.NotEqual:
                        return UnwrapCompare(bexpr, "!=", methodtarget);
                    case ExpressionType.GreaterThan:
                        return UnwrapCompare(bexpr, ">", methodtarget);
                    case ExpressionType.LessThan:
                        return UnwrapCompare(bexpr, "<", methodtarget);
                    case ExpressionType.LessThanOrEqual:
                        return UnwrapCompare(bexpr, "<=", methodtarget);
                    case ExpressionType.GreaterThanOrEqual:
                        return UnwrapCompare(bexpr, ">=", methodtarget);
                    case ExpressionType.AndAlso:
                    case ExpressionType.And:
                        return And(FromLambda(bexpr.Left, methodtarget), FromLambda(bexpr.Right, methodtarget));
                    case ExpressionType.OrElse:
                    case ExpressionType.Or:
                        return Or(FromLambda(bexpr.Left, methodtarget), FromLambda(bexpr.Right, methodtarget));
                    case ExpressionType.Add:
                        return Add(FromLambda(bexpr.Left, methodtarget), FromLambda(bexpr.Right, methodtarget));
                    case ExpressionType.Subtract:
                        return Subtract(FromLambda(bexpr.Left, methodtarget), FromLambda(bexpr.Right, methodtarget));
                    case ExpressionType.Multiply:
                        return Multiply(FromLambda(bexpr.Left, methodtarget), FromLambda(bexpr.Right, methodtarget));
                    case ExpressionType.Divide:
                        return Divide(FromLambda(bexpr.Left, methodtarget), FromLambda(bexpr.Right, methodtarget));
                    case ExpressionType.Modulo:
                        return Modulo(FromLambda(bexpr.Left, methodtarget), FromLambda(bexpr.Right, methodtarget));
                }
            }
            else if (expr is ConstantExpression cexpr)
            {
                return new Value(cexpr.Value);
            }
            else if (expr is MemberExpression mexpr)
            {
                if (mexpr.Expression == methodtarget)
                    return Property(mexpr.Member.Name);
                return new Value(GetValue(mexpr));
            }
            else if (expr is MethodCallExpression callexpr)
            {
                if (callexpr.Method.DeclaringType == typeof(string) && callexpr.Method.Name == nameof(string.Equals))
                {
                    var useLike = false;
                    if (callexpr.Method.GetParameters().Length == 3)
                        useLike = new StringComparison[] {
                            StringComparison.CurrentCultureIgnoreCase,
                            StringComparison.InvariantCultureIgnoreCase,
                            StringComparison.OrdinalIgnoreCase
                        }.Contains((StringComparison)GetValue(callexpr.Arguments.Last())); 

                    if (useLike)
                        return Like(FromLambda(callexpr.Arguments.First(), methodtarget), FromLambda(callexpr.Arguments.Skip(1).First(), methodtarget));
                    else
                        return Equal(FromLambda(callexpr.Arguments.First(), methodtarget), FromLambda(callexpr.Arguments.Last(), methodtarget));
                }
                else if (callexpr.Method.DeclaringType == typeof(TimeSpan) && callexpr.Method.IsStatic && callexpr.Method.GetParameters().Length == 1)
                {
                    var arg = FromLambda(callexpr.Arguments.First(), methodtarget);
                    if (arg is Value argv)
                    {
                        if (callexpr.Method.Name == nameof(TimeSpan.FromTicks))
                            return new Value(TimeSpan.FromTicks((long)Convert.ChangeType(argv.Item, typeof(long))));
                        if (callexpr.Method.Name == nameof(TimeSpan.FromMilliseconds))
                            return new Value(TimeSpan.FromMilliseconds((double)Convert.ChangeType(argv.Item, typeof(double))));
                        if (callexpr.Method.Name == nameof(TimeSpan.FromSeconds))
                            return new Value(TimeSpan.FromSeconds((double)Convert.ChangeType(argv.Item, typeof(double))));
                        if (callexpr.Method.Name == nameof(TimeSpan.FromMinutes))
                            return new Value(TimeSpan.FromMinutes((double)Convert.ChangeType(argv.Item, typeof(double))));
                        if (callexpr.Method.Name == nameof(TimeSpan.FromHours))
                            return new Value(TimeSpan.FromHours((double)Convert.ChangeType(argv.Item, typeof(double))));
                        if (callexpr.Method.Name == nameof(TimeSpan.FromDays))
                            return new Value(TimeSpan.FromDays((double)Convert.ChangeType(argv.Item, typeof(double))));
                    }
                }
                else if (callexpr.Method.DeclaringType.IsGenericType && callexpr.Method.DeclaringType.GetGenericTypeDefinition() == typeof(Dictionary<,>) && callexpr.Method.Name == nameof(Dictionary<int,int>.ContainsKey) && callexpr.Arguments.Count == 1)
                {
                    var collection = GetValue(callexpr.Object);
                    if (collection is IEnumerable cenm && !(collection is string))
                    {
                        var seqex = 
                            callexpr.Method.DeclaringType
                            .GetProperty(nameof(Dictionary<int,int>.Keys))
                            .GetValue(collection, null) as IEnumerable;

                        var arg = FromLambda(callexpr.Arguments.First(), methodtarget);
                        return In(arg, seqex.Cast<object>());
                    }

                }
                else if (callexpr.Method.IsStatic && callexpr.Method.DeclaringType == typeof(System.Linq.Enumerable) && callexpr.Method.Name == nameof(System.Linq.Enumerable.Contains))
                {
                    var collection = GetValue(callexpr.Arguments.First());
                    if (collection is IEnumerable cenm && !(collection is string))
                    {
                        var arg = FromLambda(callexpr.Arguments.Last(), methodtarget);
                        return In(arg, cenm.Cast<object>());
                    }

                }

                throw new Exception($"Method is not supported: {callexpr.Method}");
            }
            else if (expr is UnaryExpression uexp)
            {
                if (uexp.NodeType == ExpressionType.Not)
                    return Not(FromLambda(uexp.Operand, methodtarget));
            }

            throw new Exception($"Expression is not supported: {expr.NodeType}");
        }

        /// <summary>
        /// Extracts the value from an expression
        /// </summary>
        /// <param name="expr">The expression to get the value for</param>
        /// <returns>The value</returns>
        private static object GetValue(Expression expr)
        {
            if (expr is ConstantExpression cexpr)
                return cexpr.Value;
            else if (expr is MemberExpression mexpr)
                return GetValue(mexpr);
            else
                throw new Exception($"Expression is not supported: {expr.NodeType}");
        }

        /// <summary>
        /// Extracts the value from a member
        /// </summary>
        /// <param name="member">The member to get the value for</param>
        /// <returns>The value</returns>
        private static object GetValue(MemberExpression member)
        {
            return 
                Expression.Lambda<Func<object>>(
                    Expression.Convert(member, typeof(object))
                )
                .Compile()
                .Invoke();
        }
    }

    /// <summary>
    /// The query types supported
    /// </summary>
    public enum QueryType
    {
        /// <summary>Undetermined statement type, defaults to SELECT</summary>
        Default,
        /// <summary>A SELECT statement</summary>
        Select,
        /// <summary>An UPDATE statement</summary>
        Update,
        /// <summary>A DELETE statement</summary>
        Delete,
        /// <summary>An INSERT statement</summary>
        Insert

    }

    /// <summary>
    /// Represents a full SQL query statement
    /// </summary>
    public class ParsedQuery
    {
        /// <summary>
        /// The query type
        /// </summary>
        private QueryType m_type;
        /// <summary>
        /// The columns to return
        /// </summary>
        private List<string> m_columns;
        /// <summary>
        /// The where clause to use
        /// </summary>
        private QueryElement m_where;
        /// <summary>
        /// The limit to use
        /// </summary>
        private Tuple<long, long> m_limit;
        /// <summary>
        /// The order to use
        /// </summary>
        private List<QueryOrder> m_orders;
        /// <summary>
        /// The values used for an update
        /// </summary>
        private Dictionary<string, object> m_updatevalues;
        /// <summary>
        /// Flag indicating if the instance is finalized
        /// </summary>
        private bool m_isCompleted = false;
        /// <summary>
        /// The type this instance is for
        /// </summary>
        public Type DataType { get => m_map.Type; }
        /// <summary>
        /// The table mapping for the data type
        /// </summary>
        private readonly TableMapping m_map;
        /// <summary>
        /// A flag indicating if insert issues are ignored
        /// </summary>
        private bool m_ignoreInsert;
        /// <summary>
        /// The object being inserted, used for back-setting generated values
        /// </summary>
        private object m_insertItem;

        /// <summary>
        /// Default constructor
        /// </summary>
        public ParsedQuery(TableMapping map)
        {
            m_map = map ?? throw new ArgumentNullException(nameof(map));
        }

        /// <summary>
        /// Gets the query type this instance represents
        /// </summary>
        public QueryType Type => m_type == QueryType.Default ? QueryType.Select : m_type;

        /// <summary>
        /// The columns to select, null means all
        /// </summary>
        public IEnumerable<string> SelectColumns { get => m_columns?.Distinct(); }

        /// <summary>
        /// The where clause
        /// </summary>
        /// <returns></returns>
        public QueryElement WhereQuery { get => m_where ?? new Empty(); }

        /// <summary>
        /// The limit to apply, or null for unlimited
        /// </summary>
        public Tuple<long, long> LimitParams { get => m_limit; }

        /// <summary>
        /// Gets the update values
        /// </summary>
        public Dictionary<string, object> UpdateValues => m_updatevalues;

        /// <summary>
        /// Gets a value indicating if inserts are ignored
        /// </summary>
        public bool IgnoresInsert => m_ignoreInsert;

        /// <summary>
        /// The item being inserted
        /// </summary>
        public object InsertItem => m_insertItem;

        /// <summary>
        /// The order clause
        /// </summary>
        /// <value></value>
        public QueryOrder OrderClause
        {
            get
            {
                QueryOrder prev = null;
                if (m_orders != null)
                    for (int i = m_orders.Count - 1; i >= 0 ; i--)
                        prev = new QueryOrder(m_orders[i], prev);
                return prev;
            }
        }

        /// <summary>
        /// Marks the query as a select opration and optionally restricts the select part of the query
        /// </summary>
        /// <param name="columns">The columns to return</param>
        /// <returns>The query instance</returns>
        public ParsedQuery Select(params string[] columns)
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");

            if (m_type == QueryType.Default || m_type == QueryType.Select)
                m_type = QueryType.Select;
            else
                throw new ArgumentException($"Cannot change the query type from {m_type} to SELECT");

            if (columns != null && columns.Length != 0)
            {
                if (m_columns == null)
                    m_columns = new List<string>();

                foreach (var c in columns)
                {
                    if (!m_map.AllColumnsByMemberName.ContainsKey(c))
                        throw new ArgumentException($"The type {DataType} has no member named {c}");
                    m_columns.AddRange(columns);
                }
            }
            return this;
        }

        /// <summary>
        /// Marks the query as a delete operation
        /// </summary>
        /// <returns>The query instance</returns>
        public ParsedQuery Delete()
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");

            if (m_type == QueryType.Default || m_type == QueryType.Delete)
                m_type = QueryType.Delete;
            else
                throw new ArgumentException($"Cannot change the query type from {m_type} to DELETE");

            return this;
        }

        /// <summary>
        /// Marks the query as an insert and sets the values
        /// </summary>
        /// <param name="item">The item to insert</param>
        /// <returns>The query instance</returns>
        public ParsedQuery Insert(object item)
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (item.GetType() != DataType)
                throw new ArgumentException("The type to insert must be the same as the query is for");
            if (m_insertItem != null)
                throw new InvalidOperationException("Cannot call insert twice");

            if (m_type == QueryType.Default || m_type == QueryType.Insert)
                m_type = QueryType.Insert;
            else
                throw new ArgumentException($"Cannot change the query type from {m_type} to INSERT");

            m_insertItem = item;
            if (m_updatevalues == null)
                m_updatevalues = new Dictionary<string, object>();

            foreach (var col in m_map.InsertColumns)
                m_updatevalues.Add(col.MemberName, col.GetValueForDb(item));

            return this;
        }

        /// <summary>
        /// Marks the query as an update and sets the values to update
        /// </summary>
        /// <param name="values">An anonymous object with parameters to update</param>
        /// <returns>The query instance</returns>
        public ParsedQuery Update(object values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            Dictionary<string, object> props;
            if (values.GetType() == DataType)
            {
                props = m_map
                    .UpdateColumns
                    .ToDictionary(
                        x => x.MemberName,
                        x => x.GetValueForDb(values)
                    );
            }
            else
            {
                props = values
                    .GetType()
                    .GetProperties()
                    .ToDictionary(
                        x => x.Name,
                        x => {                            
                            var v = x.GetValue(values);
                            if (x.PropertyType.IsEnum)
                                v = (v ?? string.Empty).ToString();
                            return v;
                        }
                    );
            }

            return Update(props);
        }

        /// <summary>
        /// Marks the query as an update and sets the values to update
        /// </summary>
        /// <param name="values">The values to update</param>
        /// <returns>The query instance</returns>
        public ParsedQuery Update(Dictionary<string, object> values)
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");

            if (m_type == QueryType.Default || m_type == QueryType.Update)
                m_type = QueryType.Update;
            else
                throw new ArgumentException($"Cannot change the query type from {m_type} to UPDATE");

            if (m_updatevalues == null)
                m_updatevalues = new Dictionary<string, object>();

            foreach (var item in values)
            {
                if (!m_map.UpdateColumns.Any(x => x.MemberName == item.Key))
                {
                    if (m_map.AllColumnsByMemberName.ContainsKey(item.Key))
                        throw new ArgumentException($"The type {DataType} cannot update {item.Key}");
                    else
                        throw new ArgumentException($"The type {DataType} has no member named {item.Key}");
                }
                m_updatevalues.Add(item.Key, item.Value);
            }

            return this;
        }

        /// <summary>
        /// Computes the where filter from a string
        /// </summary>
        /// <param name="filter">The filter string</param>
        /// <returns>The query instance</returns>
        public ParsedQuery Where(string filter)
        {
            return Where(FilterParser.ParseFilter(m_map, filter));
        }

        /// <summary>
        /// Adds a additional where clause to the query
        /// </summary>
        /// <param name="query">The query to add</param>
        /// <returns>The query instance</returns>
        public ParsedQuery Where(QueryElement query)
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");
            if (m_type == QueryType.Default)
                throw new InvalidOperationException($"Cannot use {nameof(Where)} before the query type has been set");
            if (m_type == QueryType.Insert)
                throw new InvalidOperationException($"Cannot have a where statement on an INSERT");

            if (query != null)
            {
                if (m_where == null)
                    m_where = query;
                else
                    m_where = new And(m_where, query);
            }

            return this;
        }

        /// <summary>
        /// Prepends a match for the primary key to the where clause
        /// </summary>
        /// <param name="item">The item with the primary key values</param>
        /// <returns>The query instance</returns>
        public ParsedQuery MatchPrimaryKeys(object item)
        {
            return MatchPrimaryKeys(m_map.PrimaryKeys
                .Select(x => { 
                    var prop = item.GetType().GetProperty(x.MemberName);
                    if (prop != null)
                        return prop.GetValue(item);
                    
                    var field = item.GetType().GetField(x.MemberName);
                    if (field != null)
                        return field.GetValue(item);
                    throw new ArgumentException($"The data item does not have the primary key property {x.MemberName}");
                })
                .ToArray()
            );
        }

        /// <summary>
        /// Prepends a match for the primary key to the where clause
        /// </summary>
        /// <param name="values">The primary key values</param>
        /// <returns>The query instance</returns>
        public ParsedQuery MatchPrimaryKeys(object[] values)
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");
            if (m_type == QueryType.Default)
                throw new InvalidOperationException($"Cannot use {nameof(MatchPrimaryKeys)} before the query type has been set");
            if (m_type == QueryType.Insert)
                throw new InvalidOperationException($"Cannot have a where statement on an INSERT");

            if (m_map.PrimaryKeys.Length == 0)
                throw new ArgumentException($"The type {DataType} does not have a primary key");
            if (values == null || values.Length != m_map.PrimaryKeys.Length)
                throw new ArgumentException($"Expected {m_map.PrimaryKeys.Length} keys but got {values?.Length}");

            var els = values
                .Select((x, i) => new Compare(
                    new Property(m_map.PrimaryKeys[i].MemberName),
                    "=",
                    new Value(x)
                ))
                .ToArray();

            var q = els.Length == 1 ? (QueryElement)els[0] : new And(els);
            if (m_where == null || m_where is Empty)
                m_where = q;
            else
                m_where = new And(q, m_where);

            return this;
        }

        /// <summary>
        /// Adds order clauses to the query
        /// </summary>
        /// <param name="orders">The order to use</param>
        /// <returns>The query instance</returns>
        public ParsedQuery OrderBy(params QueryOrder[] orders)
        {
            return OrderBy(orders.AsEnumerable());
        }

        /// <summary>
        /// Adds order clauses to the query
        /// </summary>
        /// <param name="orders">The order to use</param>
        /// <returns>The query instance</returns>
        public ParsedQuery OrderBy(IEnumerable<QueryOrder> orders)
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");
            if (m_type == QueryType.Default)
                throw new InvalidOperationException($"Cannot use {nameof(OrderBy)} before the query type has been set");
            if (m_type == QueryType.Insert)
                throw new InvalidOperationException($"Cannot have an order-by statement on an INSERT");

            if (m_orders == null)
                m_orders = new List<QueryOrder>();

            m_orders.AddRange(orders);

            return this;
        }

        /// <summary>
        /// Adds order clauses to the query
        /// </summary>
        /// <param name="order">The order to use</param>
        /// <returns>The query instance</returns>
        public ParsedQuery OrderBy(string order)
        {
            OrderBy(FilterParser.ParseOrderList(m_map, order));
            return this;
        }

        /// <summary>
        /// Sets a limit on the number of results
        /// </summary>
        /// <param name="limit">The limit to use</param>
        /// <returns>The query instance</returns>
        public ParsedQuery Limit(long limit)
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");
            if (m_type == QueryType.Default)
                throw new InvalidOperationException($"Cannot use {nameof(Limit)} before the query type has been set");
            if (m_type == QueryType.Insert)
                throw new InvalidOperationException($"Cannot use {nameof(Limit)} on an INSERT statement");

            if (m_limit == null)
                m_limit = new Tuple<long, long>(limit, -1);
            else
            {
                if (m_limit.Item1 != -1)
                    throw new ArgumentException("Cannot set the limit more than once");
                m_limit = new Tuple<long, long>(limit, m_limit.Item2);
            }

            return this;
        }
        /// <summary>
        /// Sets an offset for the results
        /// </summary>
        /// <param name="offset">The limit to use</param>
        /// <returns>The query instance</returns>
        public ParsedQuery Offset(long offset)
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");
            if (m_type == QueryType.Default)
                throw new InvalidOperationException($"Cannot use {nameof(Offset)} before the query type has been set");
            if (m_type != QueryType.Select)
                throw new InvalidOperationException($"Can only use {nameof(Offset)} on SELECT");

            if (m_limit == null)
                m_limit = new Tuple<long, long>(-1, offset);
            else
            {
                if (m_limit.Item2 != -1)
                    throw new InvalidOperationException("Cannot set the offset more than once");
                m_limit = new Tuple<long, long>(m_limit.Item1, offset);
            }

            return this;
        }

        /// <summary>
        /// Completes the query by adding auto-update items
        /// </summary>
        public ParsedQuery Complete()
        {
            if (!m_isCompleted)
            {
                m_isCompleted = true;
                if (m_type == QueryType.Default)
                    m_type = QueryType.Select;

                if (m_type == QueryType.Insert)
                {
                    foreach (var el in m_map.ClientGenerated)
                    {
                        switch (el.AutoGenerateAction)
                        {
                            case AutoGenerateAction.ClientGenerateGuid:
                                {
                                    m_updatevalues.TryGetValue(el.MemberName, out var s);
                                    if (string.IsNullOrWhiteSpace(s as string))
                                        m_updatevalues[el.MemberName] = Guid.NewGuid().ToString();
                                    break;
                                }
                            case AutoGenerateAction.ClientChangeTimestamp:
                            case AutoGenerateAction.ClientCreateTimestamp:
                                m_updatevalues[el.MemberName] = DateTime.Now;
                                break;
                        }
                    }
                }
                else if (m_type == QueryType.Update)
                {
                    foreach (var el in m_map.ClientGenerated)
                    {
                        switch (el.AutoGenerateAction)
                        {
                            case AutoGenerateAction.ClientChangeTimestamp:
                                m_updatevalues[el.MemberName] = DateTime.Now;
                                break;
                        }
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Toggles a flag that makes a failed insert operation return false instead of throwing an exception
        /// </summary>
        /// <returns>The query instance</returns>
        internal void IgnoreInsertFailure()
        {
            if (m_isCompleted)
                throw new InvalidOperationException("Cannot change the query after it is finalized");
            if (m_type != QueryType.Insert)
                throw new InvalidOperationException($"Can only use {nameof(IgnoreInsertFailure)} when the query is INSERT");
            m_ignoreInsert = true;
        }
    }

    /// <summary>
    /// An untyped version of the query
    /// </summary>
    public abstract class Query
    {
        /// <summary>
        /// The parsed query instance
        /// </summary>
        public ParsedQuery Parsed { get => m_query; }

        /// <summary>
        /// The parsed query instance
        /// </summary>
        protected readonly ParsedQuery m_query;

        /// <summary>
        /// Creates a new instance of the query
        /// </summary>
        /// <param name="map">The table mapping to use</param>
        protected Query(TableMapping map)
        {
            m_query = new ParsedQuery(map);
        }
    }

    /// <summary>
    /// Represents a full SQL query statement bound to a specific type
    /// </summary>
    public class Query<T> : Query
    {
        /// <summary>
        /// Creates a new instance of a bound query
        /// </summary>
        public Query(IDatabaseDialect dialect) 
            : base(dialect.GetTypeMap<T>())
        {
        }

        /// <summary>
        /// Marks the query as a select opration and optionally restricts the select part of the query
        /// </summary>
        /// <param name="columns">The columns to return</param>
        /// <returns>The query instance</returns>
        public Query<T> Select(params string[] columns)
        {
            m_query.Select(columns);
            return this;
        }

        /// <summary>
        /// Marks the query as a delete operation
        /// </summary>
        /// <returns>The query instance</returns>
        public Query<T> Delete()
        {
            m_query.Delete();
            return this;
        }

        /// <summary>
        /// Marks the query as an insert and sets the values
        /// </summary>
        /// <param name="item">The item to insert</param>
        /// <returns>The query instance</returns>
        public Query<T> Insert(T item)
        {
            m_query.Insert(item);
            return this;
        }

        /// <summary>
        /// Toggles a flag that makes a failed insert operation return false instead of throwing an exception
        /// </summary>
        /// <returns>The query instance</returns>
        public Query<T> IgnoreInsertFailure()
        {
            m_query.IgnoreInsertFailure();
            return this;
        }

        /// <summary>
        /// Marks the query as an update and sets the values to update
        /// </summary>
        /// <param name="values">An anonymous object with parameters to update</param>
        /// <returns>The query instance</returns>
        public Query<T> Update(T values)
        {
            m_query.Update(values);
            return this;
        }
        /// <summary>
        /// Marks the query as an update and sets the values to update
        /// </summary>
        /// <param name="values">An anonymous object with parameters to update</param>
        /// <returns>The query instance</returns>
        public Query<T> Update(object values)
        {
            m_query.Update(values);
            return this;
        }

        /// <summary>
        /// Marks the query as an update and sets the values to update
        /// </summary>
        /// <param name="values">The values to update</param>
        /// <returns>The query instance</returns>
        public Query<T> Update(Dictionary<string, object> values)
        {
            m_query.Update(values);
            return this;
        }

        /// <summary>
        /// Computes the where filter from a string
        /// </summary>
        /// <param name="filter">The filter string</param>
        /// <returns>The query instance</returns>
        public Query<T> Where(string filter)
        {
            m_query.Where(filter);
            return this;
        }

        /// <summary>
        /// Adds a additional where clause to the query
        /// </summary>
        /// <param name="query">The query to add</param>
        /// <returns>The query instance</returns>
        public Query<T> Where(QueryElement query)
        {
            m_query.Where(query);
            return this;
        }

        /// <summary>
        /// Adds a additional where clause to the query
        /// </summary>
        /// <param name="exp">The query to add</param>
        /// <returns>The query instance</returns>
        public Query<T> Where(Expression<Func<T, bool>> exp)
        {
            return Where(QueryUtil.FromLambda(exp));
        }

        /// <summary>
        /// Adds an additional where clause to the query, using a sub-query to filter
        /// </summary>
        /// <param name="column">The column to match</param>
        /// <param name="subquery">The subquery to use</param>
        /// <returns>The query instance</returns>
        public Query<T> WhereIn(string column, Query subquery)
        {
            return Where(
                QueryUtil.In(
                    new Property(column),
                    subquery
                )
            );
        }

        /// <summary>
        /// Adds an additional where clause to the query, using a sub-query to filter
        /// </summary>
        /// <param name="column">The column to match</param>
        /// <param name="subquery">The subquery to use</param>
        /// <returns>The query instance</returns>
        public Query<T> WhereNotIn(string column, Query subquery)
        {
            return Where(
                QueryUtil.NotIn(
                    new Property(column),
                    subquery
                )
            );
        }

        /// <summary>
        /// Prepends a match for the primary key to the where clause
        /// </summary>
        /// <param name="item">The item with the primary key values</param>
        /// <returns>The query instance</returns>
        public Query<T> MatchPrimaryKeys(object item)
        {
            m_query.MatchPrimaryKeys(item);
            return this;
        }

        /// <summary>
        /// Prepends a match for the primary key to the where clause
        /// </summary>
        /// <param name="item">The item with the primary key values</param>
        /// <returns>The query instance</returns>
        public Query<T> MatchPrimaryKeys(object[] values)
        {
            m_query.MatchPrimaryKeys(values);
            return this;
        }

        /// <summary>
        /// Adds order clauses to the query
        /// </summary>
        /// <param name="orders">The order to use</param>
        /// <returns>The query instance</returns>
        public Query<T> OrderBy(params QueryOrder[] orders)
        {
            m_query.OrderBy(orders);
            return this;
        }

        /// <summary>
        /// Adds order clauses to the query
        /// </summary>
        /// <param name="order">The order to use</param>
        /// <returns>The query instance</returns>
        public Query<T> OrderBy(string order)
        {
            m_query.OrderBy(order);
            return this;
        }

        /// <summary>
        /// Extracts the property or field name from a lambda expression
        /// </summary>
        /// <param name="lambda">The expression to examine</param>
        /// <typeparam name="Tx">The return type</typeparam>
        /// <returns>The name of the member</returns>
        private string GetMemberNameFromLambda<Tx>(Expression<Func<T, Tx>> lambda)
        {
            if (!(lambda.Body is MemberExpression member))
                throw new ArgumentException($"Expression \"{lambda}\" is not a field or property");

            if (member.Member is System.Reflection.PropertyInfo property)
            {
                if (!property.ReflectedType.IsAssignableFrom(typeof(T)))
                    throw new ArgumentException($"Expression \"{lambda}\" is declared on {property.ReflectedType} but should be on {typeof(T)}");
                return property.Name;
            }
            if (member.Member is System.Reflection.FieldInfo field)
            {
                if (!field.ReflectedType.IsAssignableFrom(typeof(T)))
                    throw new ArgumentException($"Expression \"{lambda}\" is declared on {field.ReflectedType} but should be on {typeof(T)}");
                return field.Name;
            }
            else
                throw new ArgumentException($"Expression \"{lambda}\" is not a property");
        }

        /// <summary>
        /// Adds an order clause to the query (ascending)
        /// </summary>
        /// <param name="lambda">The field to use</param>
        /// <returns>The query instance</returns>
        public Query<T> OrderBy<Tx>(Expression<Func<T, Tx>> lambda)
        {
            m_query.OrderBy(new QueryOrder(GetMemberNameFromLambda(lambda), false));
            return this;
        }

        /// <summary>
        /// Adds an order clause to the query (descending)
        /// </summary>
        /// <param name="lambda">The field to use</param>
        /// <returns>The query instance</returns>
        public Query<T> OrderByDesc<Tx>(Expression<Func<T, Tx>> lambda)
        {
            m_query.OrderBy(new QueryOrder(GetMemberNameFromLambda(lambda), true));
            return this;
        }

        /// <summary>
        /// Sets a limit on the number of results
        /// </summary>
        /// <param name="limit">The limit to use</param>
        /// <returns>The query instance</returns>
        public Query<T> Limit(long limit)
        {
            m_query.Limit(limit);
            return this;
        }
        /// <summary>
        /// Sets an offset for the results
        /// </summary>
        /// <param name="offset">The limit to use</param>
        /// <returns>The query instance</returns>
        public Query<T> Offset(long offset)
        {
            m_query.Offset(offset);
            return this;
        }

        /// <summary>
        /// Completes the query by adding any client generated values
        /// </summary>
        /// <returns>The query instance</returns>
        public Query<T> Complete()
        {
            m_query.Complete();
            return this;
        }
    }

    /// <summary>
    /// A collection class for a query order
    /// </summary>
    public class QueryOrder
    {
        /// <summary>
        /// The property to order by
        /// </summary>
        public readonly string Property;

        /// <summary>
        /// A flag indicating if the sort order is descending
        /// </summary>
        public readonly bool Descending;

        /// <summary>
        /// The next order element
        /// </summary>
        public readonly QueryOrder Next;

        /// <summary>
        /// Creates a new query order
        /// </summary>
        /// <param name="property">The property to order after</param>
        /// <param name="descending">The order to sort</param>
        /// <param name="next">The next sort property</param>
        public QueryOrder(string property, bool descending = false, QueryOrder next = null)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            Descending = descending;
            Next = next;
        }

        /// <summary>
        /// Constructs a new query order based on an existing one
        /// </summary>
        /// <param name="item">The item to base the order on</param>
        /// <param name="next">The next order</param>
        public QueryOrder(QueryOrder item, QueryOrder next)
        {
            if (item.Next != null)
                throw new ArgumentException("Cannot re-order an order clause with sub-orders");

            Property = item.Property;
            Descending = item.Descending;
            Next = next;
        }

    }


    /// <summary>
    /// Base class for all query items
    /// </summary>
    public abstract class QueryElement
    {
    }

    /// <summary>
    /// Represents a custom SQL where fragment
    /// </summary>
    public class CustomQuery : QueryElement
    {
        /// <summary>
        /// The SQL string fragment
        /// </summary>
        public readonly string Value;
        /// <summary>
        /// The arguments to use for the fragment
        /// </summary>
        public readonly object[] Arguments;

        /// <summary>
        /// Creates a new custom query fragment
        /// </summary>
        /// <param name="value">The SQL string fragment</param>
        /// <param name="arguments">The arguments for the fragment</param>
        public CustomQuery(string value, object[] arguments)
        {
            Value = value;
            Arguments = arguments;
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => Value;

    }

    /// <summary>
    /// Wrapper class for a value
    /// </summary>
    public class Value : QueryElement
    {
        /// <summary>
        /// The value being wrapped
        /// </summary>
        public object Item;

        /// <summary>
        /// Creates a new value wrapper
        /// </summary>
        /// <param name="item">The item to wrap</param>
        public Value(object item)
        {
            Item = item;
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => Item?.ToString();
    }

    /// <summary>
    /// Placeholder class for an empty query element
    /// </summary>
    public class Empty : QueryElement
    {
    }

    /// <summary>
    /// Represents a property
    /// </summary>
    public class Property : QueryElement
    {
        /// <summary>
        /// The property name
        /// </summary>
        public readonly string PropertyName;

        /// <summary>
        /// Creates a new property name
        /// </summary>
        /// <param name="propertyname">The name of the property</param>
        public Property(string propertyname)
        {
            PropertyName = propertyname ?? throw new ArgumentNullException(nameof(propertyname));
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => PropertyName;
    }

    /// <summary>
    /// Compares two items
    /// </summary>
    public class Compare : QueryElement
    {
        /// <summary>
        /// The property being compared
        /// </summary>
        public readonly object LeftHandSide;
        /// <summary>
        /// The compare operator
        /// </summary>
        public readonly string Operator;
        /// <summary>
        /// The value compared to
        /// </summary>
        public readonly object RightHandSide;

        /// <summary>
        /// Creates a new compare query element
        /// </summary>
        /// <param name="lefthandside">The left-hand-side to compare</param>
        /// <param name="@operator">The operator to use</param>
        /// <param name="righthandside">The right-hand-side to compare to</param>
        public Compare(object lefthandside, string @operator, object righthandside)
        {
            LeftHandSide = lefthandside;
            Operator = @operator;
            RightHandSide = righthandside;
            if (RightHandSide is Query rhq)
            {
                if (rhq.Parsed.Type != QueryType.Select)
                    throw new ArgumentException("The query must be a select statement for exactly one column", nameof(righthandside));
                if (rhq.Parsed.SelectColumns.Count() != 1)
                    throw new ArgumentException("The query must be a select statement for exactly one column", nameof(righthandside));
            }
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => $"{LeftHandSide} {Operator} {RightHandSide}";

    }

    /// <summary>
    /// Performs arithmetic on two operands
    /// </summary>
    public class Arithmetic : QueryElement
    {
        /// <summary>
        /// The left-hand side operand
        /// </summary>
        public readonly object LeftHandSide;
        /// <summary>
        /// The arithmetic operator
        /// </summary>
        public readonly string Operator;
        /// <summary>
        /// The right-hand side operand
        /// </summary>
        public readonly object RightHandSide;

        /// <summary>
        /// Creates a new arithmetic query element
        /// </summary>
        /// <param name="lefthandside">The left-hand-side operand</param>
        /// <param name="@operator">The operator to use</param>
        /// <param name="righthandside">The right-hand-side operand</param>
        public Arithmetic(object lefthandside, string @operator, object righthandside)
        {
            LeftHandSide = lefthandside;
            Operator = @operator;
            RightHandSide = righthandside;
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => $"{LeftHandSide} {Operator} {RightHandSide}";

    }

    /// <summary>
    /// Represents a logical and
    /// </summary>
    public class And : QueryElement
    {
        /// <summary>
        /// The items in the and list
        /// </summary>
        public readonly QueryElement[] Items;

        /// <summary>
        /// Constructs a new and element
        /// </summary>
        /// <param name="items">The items to and</param>
        public And(params QueryElement[] items)
            : this((items ?? new QueryElement[0]).AsEnumerable())
        {
        }

        /// <summary>
        /// Constructs a new and element
        /// </summary>
        /// <param name="items">The items to and</param>
        public And(IEnumerable<QueryElement> items)
        {
            Items = items.ToArray();
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => string.Join(" AND ", Items.Select(x => "(" + x.ToString() + ")") );
    }

    /// <summary>
    /// Represents a logical or
    /// </summary>
    public class Or : QueryElement
    {
        /// <summary>
        /// The items in the list
        /// </summary>
        public readonly QueryElement[] Items;

        /// <summary>
        /// Constructs a new or element
        /// </summary>
        /// <param name="items">The items to or</param>
        public Or(params QueryElement[] items)
            : this((items ?? new QueryElement[0]).AsEnumerable())
        {
        }

        /// <summary>
        /// Constructs a new or element
        /// </summary>
        /// <param name="items">The items to or</param>
        public Or(IEnumerable<QueryElement> items)
        {
            Items = items.ToArray();
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => string.Join(" OR ", Items.Select(x => "(" + x.ToString() + ")"));
    }

    /// <summary>
    /// Represents a parenthesized expression
    /// </summary>
    public class ParenthesisExpression : QueryElement
    {
        /// <summary>
        /// The expression to operate on
        /// </summary>
        public readonly object Expression;

        /// <summary>
        /// Constructs a new parenthesis element
        /// </summary>
        /// <param name="expression">The expression to operate on</param>
        public ParenthesisExpression(object expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => $"({Expression})";

    }

    /// <summary>
    /// Represents a unary operator
    /// </summary>
    public class UnaryOperator : QueryElement
    {
        /// <summary>
        /// The operator to use
        /// </summary>
        public readonly string Operator;

        /// <summary>
        /// The expression to operate on
        /// </summary>
        public readonly object Expression;

        /// <summary>
        /// Constructs a new unary operator element
        /// </summary>
        /// <param name="operator">The operation to use</param>
        /// <param name="expression">The expression to operate on</param>
        public UnaryOperator(string @operator, object expression)
        {
            Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => $"{Operator} ({Expression})";
    }
}
