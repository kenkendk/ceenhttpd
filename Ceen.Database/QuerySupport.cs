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
        /// Negates an expression
        /// </summary>
        /// <param name="expr">The expression to negate</param>
        /// <returns>A query element</returns>
        public static QueryElement Not(object expr) => new Not(expr);

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
                        return UnwrapCompare(bexpr, "<=", methodtarget);
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
    /// Base class for all query items
    /// </summary>
    public abstract class QueryElement
    {
    }

    /// <summary>
    /// Wrapper class for a value
    /// </summary>
    public class Value : QueryElement
    {
        /// <summary>
        /// The value being wrapped
        /// </summary>
        public readonly object Item;

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
        }

        /// <summary>
        /// A string representation
        /// </summary>
        /// <returns>The string representation</returns>
        public override string ToString() => $"{LeftHandSide} {Operator} {RightHandSide}";

    }

    /// <summary>
    /// Compares two items
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
    /// Represents a logical negated expression
    /// </summary>
    public class Not : QueryElement
    {
        /// <summary>
        /// The expression to negate
        /// </summary>
        public readonly object Expression;

        /// <summary>
        /// Constructs a new not element
        /// </summary>
        /// <param name="expression">The expression to negate</param>
        public Not(object expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
    }

}
