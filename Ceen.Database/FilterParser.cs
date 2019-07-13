using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ceen.Database
{    
    /// <summary>
    /// Helper class that can parse a limited subset of an SQL where statement
    /// </summary>
    public static class FilterParser
    {
        /// <summary>
        /// Exception for invalid filter strings
        /// </summary>
        public class ParserException : Exception
        {
            /// <summary>
            /// Creats a new parser exception
            /// </summary>
            /// <param name="message">The error message</param>
            public ParserException(string message)
                : base(message)
            {
            }
        }

        /// <summary>
        /// The UNIX timestamp epoch value
        /// </summary>
        private static readonly DateTime EPOCH = new DateTime(1970, 1, 1, 0, 0, 0);

        /// <summary>
        /// The regular expression to use for parsing an orderBy string
        /// </summary>
        private static readonly Regex _orderByTokenizer =
            new Regex(
                @"\s*(?<sortorder1>\+|\-)?((?<nonquoted>\w+)|((?<quoted>""[^""]*"")))(\s+(?<sortorder2>\w+))?\s*(?<comma>,?)"
            );

        /// <summary>
        /// A regular expression to tokenize a filter string, 
        /// looking for quoted and unquoted identifiers 
        /// and supporting a small number of arithmetic and compare operators
        /// </summary>
        private static readonly Regex _filterTokenizer =
            new Regex(
                @"(?<number>(\d+(\.\d*))|(\.\d+))|(?<nonquoted>\w+)|((?<quoted>""[^""]*""))|(?<special>\<=|\>=|==|!=|<>|\(|\)|<|=|>|\+|-|\*|/|\%|,)|(?<whitespace>\s+)"
            );

        /// <summary>
        /// Operator preceedence table, based on:
        /// https://www.sqlite.org/lang_expr.html
        /// 
        /// The operators are applied bottom-up, meaning we split on the lowest
        /// priority and then recursively evaluate the parts, leaving us with
        /// the correct &quot;higher-value-first&quot; bindings.
        /// 
        /// But we need to make sure the parenthesis is always binding hardest
        /// as it changes precedence, thus it has the lowest priority value
        /// </summary>
        private static readonly Dictionary<string, int> _priorityTable 
            = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) 
            {
                { ")",   50 }, { "(",   1 }, { ",",     2 },

                { "*",   40 }, { "%",   40 }, { "/",   40 },
                { "+",   35 }, { "-",   35 }, { "not", 35 },
                { "<",   30 }, { "<=",  30 }, { ">",   30 }, { ">=", 30 },
                { "=",   25 }, { "==",  25 }, { "!=",  25 }, { "<>", 25 }, { "in", 25 }, { "like", 25 },
                { "and", 20 }, 
                { "or",  15 }
            };

        /// <summary>
        /// Map of all supported arithmetic operators
        /// </summary>
        private static readonly HashSet<string> _arithmeticOps
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "*", "%", "/", "+", "-"
            };

        /// <summary>
        /// Map of all supported compare operators
        /// </summary>
        private static readonly HashSet<string> _compareOps
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "<", ">", "<=", ">=", "=", "==",
                "!=", "<>", "in", "like"
            };

        /// <summary>
        /// Map of all supported binary operators
        /// </summary>
        private static readonly HashSet<string> _binOps 
                = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                {
                    "*", "%", "/", "+", "-", "<", ">", 
                    "<=", ">=", "=", "==", "!=", "<>", 
                    "in", "like", "and", "or"
                };

        /// <summary>
        /// Map of all supported unary operators
        /// </summary>
        private static readonly HashSet<string> _unOps
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                    "+", "-", "not"
            };

        /// <summary>
        /// Structure for keeping track of semi-parsed tokens
        /// </summary>
        private class SemiParsed
        {
            /// <summary>
            /// The original token string
            /// </summary>
            public readonly string Token;
            /// <summary>
            /// The offset into the original string
            /// </summary>
            public readonly int Offset;
            /// <summary>
            /// The potentially parsed item
            /// </summary>
            public QueryElement Parsed;
            /// <summary>
            /// The priority of the item
            /// </summary>
            public readonly int Priority;

            /// <summary>
            /// Constructs a new semi-parsed instance
            /// </summary>
            /// <param name="map">The table mapping</param>
            /// <param name="value">The token being parsed</param>
            /// <param name="offset">The string offset</param>
            public SemiParsed(TableMapping map, string value, int offset)
            {
                Token = value;
                Offset = offset;
                if (!_priorityTable.TryGetValue(value, out Priority))
                {
                    if (Token.StartsWith("\"") && Token.EndsWith("\""))
                    {
                        Parsed = new Value(Token.Substring(1, Token.Length - 2));
                    }
                    else
                    {
                        var prop = map.AllColumns.FirstOrDefault(x => string.Equals(x.MemberName, Token, StringComparison.OrdinalIgnoreCase));
                        if (prop != null)
                            Parsed = new Property(prop.MemberName);
                        else
                            Parsed = new Value(Token);
                    }
                }
            }
        }

        /// <summary>
        /// Parses an order string
        /// </summary>
        /// <param name="map">The table map to yse</param>
        /// <param name="order">The order string</param>
        /// <returns>The parsed order</returns>
        public static QueryOrder ParseOrder(TableMapping map, string order)
        {
            QueryOrder res = null;
            foreach(var n in ParseOrderList(map, order).Reverse())
                res = new QueryOrder(n, res);

            return res;
        }


        /// <summary>
        /// Parses an order string
        /// </summary>
        /// <param name="map">The table map to yse</param>
        /// <param name="order">The order string</param>
        /// <returns>The parsed order elements</returns>
        public static IEnumerable<QueryOrder> ParseOrderList(TableMapping map, string order)
        {
            if (string.IsNullOrWhiteSpace(order))
                yield break;

            // Make a case-insensitive lookup table for the column names
            var propmap = map.AllColumns
                .ToDictionary(
                    x => x.MemberName, 
                    StringComparer.OrdinalIgnoreCase
                );

            var prevcomma = true;
            var pos = 0;
            foreach (var m in _orderByTokenizer.Matches(order).Cast<Match>())
            {
                if (!prevcomma)
                    throw new ParserException($"Missing comma before: {m.Value} at {m.Index}");
                if (!m.Success)
                    throw new ParserException($"No match at {m.Index}");
                if (pos != m.Index)
                    throw new ParserException($"Failed to parse {order.Substring(pos, m.Index - pos)} at offset {pos}");
                pos += m.Length;

                var dir = string.Empty;
                if (m.Groups["sortorder1"].Success)
                    dir = m.Groups["sortorder1"].Value;
                if (m.Groups["sortorder2"].Success)
                {
                    if (!string.IsNullOrWhiteSpace(dir))
                        throw new ParserException($"Cannot use both pre- and post-fix direction specifiers: {m.Value} at offset {m.Index}");
                    dir = m.Groups["sortorder2"].Value;
                }

                if (string.IsNullOrWhiteSpace(dir) || string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase))
                    dir = "+";
                if (string.Equals(dir, "DESC", StringComparison.OrdinalIgnoreCase))
                    dir = "-";

                if (dir != "-" && dir != "+")
                    throw new ParserException($"Unsupported direction specifier: {dir}");

                var column = m.Groups["quoted"].Success
                    ? m.Groups["quoted"].Value
                    : m.Groups["nonquoted"].Value;

                if (!propmap.ContainsKey(column))
                    throw new ParserException($"The property {column} does not exist on the type");

                yield return new QueryOrder(propmap[column].MemberName, dir == "-");
                prevcomma = m.Groups["comma"].Length > 0;
            }

            if (pos != order.Length)
                throw new ParserException($"Failed to parse {order.Substring(pos)} at offset {pos}");

            if (prevcomma)
                throw new ParserException($"Invalid trailing comma: {order}");
        }

        /// <summary>
        /// Parsese a filter string and returns a query element for it
        /// </summary>
        /// <param name="map">The table map to yse</param>
        /// <param name="filter">The filter to parse</param>
        /// <returns>The parsed query</returns>
        public static QueryElement ParseFilter(TableMapping map, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return new Empty();

            var lst = Parse(map, Tokenize(map, filter).ToList());
            if (lst.Count() == 0)
                return new Empty();
            else if (lst.Count() > 1)
                throw new ParserException($"Found multiple expressions: {string.Join(", ", lst.Select(x => x.Offset))}");

            return CorrectValues(map, lst.First().Parsed ?? throw new ParserException("Unexpected null value"));
        }

        /// <summary>
        /// Visits the query elements and converts any string values to match the operands
        /// </summary>
        /// <param name="map">The table mapping</param>
        /// <param name="top">The element to explore</param>
        /// <returns>The top element</returns>
        private static QueryElement CorrectValues(TableMapping map, QueryElement top)
        {
            // Keep a reference to the element we return
            var entry = top;

            // Remove any parenthesis
            while (top is ParenthesisExpression pe)
                top = (QueryElement)pe.Expression;

            if (top is Compare cp)
                CorrectValues(map, null, (QueryElement)cp.LeftHandSide, (QueryElement)cp.RightHandSide);
            if (top is Arithmetic am)
                CorrectValues(map, null, (QueryElement)am.LeftHandSide, (QueryElement)am.RightHandSide);

            if (top is And andExp)
                foreach (var e in andExp.Items)
                    CorrectValues(map, typeof(bool), (QueryElement)e);
            
            if (top is Or orExp)
                foreach (var e in orExp.Items)
                    CorrectValues(map, typeof(bool), (QueryElement)e);

            if (top is UnaryOperator ue)
            {
                if (string.Equals(ue.Operator, "not", StringComparison.OrdinalIgnoreCase))
                    CorrectValues(map, typeof(bool), (QueryElement)ue.Expression);
                else
                    CorrectValues(map, (QueryElement)ue.Expression);
            }

            return entry;
        }

        /// <summary>
        /// Changes the string value of any <see name="Value" /> instances to match the property types
        /// </summary>
        /// <param name="map">The table mapping</param>
        /// <param name="targettype">The type to change the item to</param>
        /// <param name="left">The left element</param>
        /// <param name="right">The right element</param>
        private static void CorrectValues(TableMapping map, Type targettype, QueryElement left, QueryElement right)
        {
            if (left is Property lpr)
                CorrectValues(map, map.AllColumnsByMemberName[lpr.PropertyName].MemberType, right);
            else if (right is Property rpr)
                CorrectValues(map, map.AllColumnsByMemberName[rpr.PropertyName].MemberType, left);
            else if (left is Compare lcp)
                CorrectValues(map, typeof(bool), right);
            else if (left is Compare rcp)
                CorrectValues(map, typeof(bool), left);
            else
            {
                CorrectValues(map, targettype, left);
                CorrectValues(map, targettype, right);
            }
        }

        /// <summary>
        /// Changes the string value of any <see name="Value" /> instances to match the property types
        /// </summary>
        /// <param name="map">The table mapping</param>
        /// <param name="targettype">The type to change the item to</param>
        /// <param name="el">The element to visit</param>
        private static void CorrectValues(TableMapping map, Type targettype, QueryElement el)
        {
            while(el is ParenthesisExpression p)
                el = (QueryElement)p.Expression;

            if (el is Arithmetic a)
                CorrectValues(map, targettype, (QueryElement)a.LeftHandSide, (QueryElement)a.RightHandSide);
            if (el is Compare c)
                CorrectValues(map, targettype, (QueryElement)c.LeftHandSide, (QueryElement)c.RightHandSide);
            if (el is And andExp)
                foreach (var n in andExp.Items)
                    CorrectValues(map, targettype, (QueryElement)n);
            if (el is Or orExp)
                foreach (var n in orExp.Items)
                    CorrectValues(map, targettype, (QueryElement)n);

            // Change the type if we get here
            if (el is Value v && targettype != null && v.Item is string vs)
            {
                v.Item = ConvertEl(vs, targettype);
            }
            else if (el is Value vx && targettype != null && vx.Item is Array en)
            {
                for(var i = 0; i < en.Length; i++)
                    if (en.GetValue(i) is string vsa)
                        en.SetValue(ConvertEl(vsa, targettype), i);
            }

        }

        /// <summary>
        /// Converts a string value to the target type
        /// </summary>
        /// <param name="vs">The input string</param>
        /// <param name="targettype">The desired type</param>
        /// <returns>The converted object</returns>
        private static object ConvertEl(string vs, Type targettype)
        {
            if (targettype.IsEnum)
            {
                object e = null;

                try { e = Enum.Parse(targettype, vs, true); }
                catch { }

                if (e == null)
                    throw new ParserException($"Cannot parse {vs} as a {targettype.Name}");

                // We return as a string, because enums are stored as strings
                return e.ToString();
            }
            else if (targettype == typeof(bool))
            {
                if (string.Equals("true", vs, StringComparison.OrdinalIgnoreCase))
                    return true;
                else if (string.Equals("false", vs, StringComparison.OrdinalIgnoreCase))
                    return false;
                else
                    throw new ParserException($"Cannot parse {vs} as a boolean");
            }
            else if (targettype.IsPrimitive)
            {
                try { return Convert.ChangeType(vs, targettype); }
                catch
                {
                    throw new ParserException($"Cannot parse {vs} as a {targettype.Name}");
                }
            }
            else if (targettype == typeof(TimeSpan))
            {
                if (!double.TryParse(vs, NumberStyles.Any, CultureInfo.InvariantCulture, out var lval))
                    throw new ParserException($"Cannot parse {vs} as a {targettype.Name}");
                return TimeSpan.FromSeconds(lval);
            }
            else if (targettype == typeof(DateTime))
            {
                if (!double.TryParse(vs, NumberStyles.Any, CultureInfo.InvariantCulture, out var lval))
                    throw new ParserException($"Cannot parse {vs} as a {targettype.Name}");
                return EPOCH.AddSeconds(lval);
            }

            return vs;
        }

        /// <summary>
        /// Parses a sequence of tokens into a list of parsed elements
        /// </summary>
        /// <param name="map">The table map</param>
        /// <param name="semiparsed">The list of semi-parsed items</param>
        /// <returns>An updated condensed list of parsed items</returns>
        private static List<SemiParsed> Parse(TableMapping map, List<SemiParsed> semiparsed)
        {
            // Keep on parsing untill everything is parsed
            while(semiparsed.Any(x => x.Parsed == null))
            {
                var best_index = -1;

                for (int i = 0; i < semiparsed.Count; i++)
                {
                    var sp = semiparsed[i];
                    if (sp.Parsed == null && (best_index < 0 || sp.Priority < semiparsed[best_index].Priority))
                        best_index = i;
                }

                if (best_index < 0)
                    throw new ParserException("Unable to parse filter");

                var best = semiparsed[best_index];
                if (best.Token == "(")
                {
                    // Find the next matching brace
                    var count = 1;
                    var p = best_index + 1;
                    for (; p < semiparsed.Count; p++)
                    {
                        var t = semiparsed[p];
                        if (t.Parsed != null)
                            continue;

                        if (t.Token == "(")
                            count++;
                        else if (t.Token == ")")
                            count--;

                        if (count == 0)
                            break;
                    }

                    if (count != 0 || p >= semiparsed.Count)
                        throw new ParserException($"Unbalanced parenthesis starting at {best.Offset}");

                    var subseq = semiparsed.GetRange(best_index + 1, p - best_index - 1);
                    semiparsed.RemoveRange(best_index + 1, p - best_index);
                    var parsed = Parse(map, subseq);
                    if (parsed.Count != 1)
                        throw new ParserException($"Unable to parse sub expression starting at {best.Offset}");

                    best.Parsed = new ParenthesisExpression(
                        parsed.First().Parsed 
                            ?? throw new ParserException($"Failed to parse {parsed.First().Token} at {parsed.First().Offset}")
                    );
                }
                else if (_binOps.Contains(best.Token) || _unOps.Contains(best.Token))
                {  
                    if (best_index == semiparsed.Count - 1)
                        throw new ParserException($"No right-hand operand for {best.Token} at {best.Offset}");

                    var right = Parse(map, semiparsed.GetRange(best_index + 1, semiparsed.Count - best_index - 1));
                    var right_hand = right.First();

                    // Handle unary operators
                    if (best_index == 0 || !_binOps.Contains(best.Token))
                    {
                        if (_unOps.Contains(best.Token))
                        {
                            right[0] = best;
                            best.Parsed = new UnaryOperator(best.Token, right_hand);
                            semiparsed = right;
                        }
                        else
                        {
                            throw new ParserException($"No left-hand operand for {best.Token} at {best.Offset}");
                        }
                    }
                    else
                    {
                        var left = Parse(map, semiparsed.GetRange(0, best_index));
                        var left_hand = left.Last();
                        var t = best.Token;

                        if (_arithmeticOps.Contains(t))
                            best.Parsed = new Arithmetic(left_hand.Parsed, t, right_hand.Parsed);
                        else if (_compareOps.Contains(t))
                        {
                            if (t == "==")
                                t = "=";
                            else if (t == "<>")
                                t = "!=";
                            best.Parsed = new Compare(left_hand.Parsed, t, right_hand.Parsed);
                        }
                        else if (string.Equals(t, "and"))
                            best.Parsed = new And(left_hand.Parsed, right_hand.Parsed);
                        else if (string.Equals(t, "or"))
                            best.Parsed = new Or(left_hand.Parsed, right_hand.Parsed);
                        else
                            throw new ParserException($"Failed to classify operator {best.Token} at {best.Offset}");

                        // Remove the left-hand symbol
                        left.RemoveAt(left.Count - 1);

                        // Replace the right-hand symbol
                        right[0] = best;

                        // Build the new list of unifished tasks
                        semiparsed = left.Concat(right).ToList();
                    }
                }
                else
                {
                    if (best.Token == ")")
                        throw new ParserException($"Dangling parenthesis at {best.Offset}");
                    else if (best.Token == ",")
                    {
                        // Most likely this is a sequence of elements
                        if (semiparsed.Count > 1 && (semiparsed.Count % 2) == 1)
                        {
                            var good = true;
                            
                            for (int i = 0; i < semiparsed.Count; i++)
                            {
                                if (i % 2 == 0)
                                    good &= semiparsed[i].Parsed is Value;
                                else
                                    good &= semiparsed[i].Token == ",";
                            }

                            if (good)
                            {
                                semiparsed[0].Parsed = new Value(
                                    semiparsed
                                        .Where(x => x != null)
                                        .Select(x => x.Parsed)
                                        .OfType<Value>()                                        
                                        .Select(x => x.Item)
                                        .ToArray()
                                );
                                semiparsed.RemoveRange(1, semiparsed.Count - 1);
                                continue;
                            }
                        }

                        throw new ParserException($"Mismatched comma at {best.Offset}");
                    }
                    else
                        throw new ParserException($"Unable to process token {best.Token} at {best.Offset}");
                }
            }

            return semiparsed;
        }

        /// <summary>
        /// Divides a filter string into tokens
        /// </summary>
        /// <param name="filter">The filter string</param>
        /// <returns>A sequence of tokens</returns>
        private static IEnumerable<SemiParsed> Tokenize(TableMapping map, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                yield break;

            var pos = 0;
            foreach (var item in _filterTokenizer.Matches(filter).Cast<Match>())
            {
                if (!item.Success)
                    throw new ParserException($"Failed to parse {item.Value} at offset {item.Index}");
                if (pos != item.Index)
                    throw new ParserException($"Failed to parse {filter.Substring(pos, item.Index - pos)} at offset {pos}");

                // Ignore whitespace tokens
                if (!string.IsNullOrWhiteSpace(item.Value))
                    yield return new SemiParsed(map, item.Value, item.Index);

                pos += item.Length;
            }

            if (pos != filter.Length)
                throw new ParserException($"Failed to parse {filter.Substring(pos)} at offset {pos}");
        }    
    }
}
