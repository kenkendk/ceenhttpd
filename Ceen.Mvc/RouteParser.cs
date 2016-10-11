using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace Ceen.Mvc
{
	/// <summary>
	/// Class for handling parsing of route specifications
	/// </summary>
	public class RouteParser
	{
		/// <summary>
		/// Common interface for fragments
		/// </summary>
		private interface IFragment
		{
			/// <summary>
			/// Gets the literal value
			/// </summary>
			string Value { get; }

			/// <summary>
			/// Gets the fragment as a regular expression.
			/// </summary>
			string RegularExpression { get; }
		}

		/// <summary>
		/// Class representing a literal fragment of a route
		/// </summary>
		private class Literal : IFragment
		{
			/// <summary>
			/// Gets the literal value
			/// </summary>
			/// <value>The value.</value>
			public string Value { get; private set; }

			/// <summary>
			/// Gets a value indicating if this fragment delimits paths
			/// </summary>
			/// <value><c>true</c> if is path delimiter; otherwise, <c>false</c>.</value>
			public bool IsPathDelimiter { get; private set; }

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Mvc.RouteParser.Literal"/> class.
			/// </summary>
			/// <param name="value">The literal value.</param>
			public Literal(string value)
			{
				Value = value;
			}

			/// <summary>
			/// Gets the fragment as a regular expression.
			/// </summary>
			public string RegularExpression
			{
				get
				{
					return Regex.Escape(Value);
				}
			}
		}

		/// <summary>
		/// Class representing a variable entry
		/// </summary>
		private class Variable : IFragment
		{
			/// <summary>
			/// Gets the full string from the route
			/// </summary>
			public string Value { get; private set; }

			/// <summary>
			/// Gets the name of the variable
			/// </summary>
			public string Name { get; private set; }

			/// <summary>
			/// Gets a value indicating if the argument is optional
			/// </summary>
			public bool Optional { get; private set; }

			/// <summary>
			/// Gets a value indicating if this item is slurping the rest of the line
			/// </summary>
			public bool Slurp { get; private set; }

			/// <summary>
			/// Gets the constraint of this variable
			/// </summary>
			public string Constraint { get; private set; }

			/// <summary>
			/// Gets the default value for this variable
			/// </summary>
			/// <value>The default value.</value>
			public string DefaultValue { get; private set; }

			/// <summary>
			/// Gets or sets the delimiter.
			/// </summary>
			/// <value>The delimiter.</value>
			public string Delimiter { get; set; } = "/";

			/// <summary>
			/// The regular expression for matching a variable's components
			/// </summary>
			private static readonly Regex VARIABLE_MATCH = new Regex(@"(?<slurp>\*)?(?<name>\w+)(?<optional>\?)?(:(?<constraint>[^\=\?]+))?(?<optional>\?)?(\=(?<default>.*))?");

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Mvc.RouteParser.Variable"/> class.
			/// </summary>
			/// <param name="value">The value to parse.</param>
			public Variable(string value)
			{
				Value = string.Format("{{{0}}}", value);

				var m = VARIABLE_MATCH.Match(value);
				if (!m.Success)
					throw new ArgumentException($"The supplied string is not a valid variable specification: {{{value}}}", nameof(value));

				Slurp = m.Groups["slurp"].Success;
				Name = m.Groups["name"].Value;
				Optional = m.Groups["optional"].Success;
				Constraint = m.Groups["constraint"].Value;
				DefaultValue = m.Groups["default"].Value;
			}

			/// <summary>
			/// Gets the fragment as a regular expression.
			/// </summary>
			public string RegularExpression
			{
				get
				{
					return string.Format("({3}(?<{0}>{2})){1}", Regex.Escape(Name), Optional || DefaultValue != null ? "?" : "", Slurp ? ".*" : "[^" + Regex.Escape(Delimiter) + "]+", Regex.Escape(Delimiter));
				}
			}
		}

		/// <summary>
		/// Gets all variables and a boolean indicating if they are optional
		/// </summary>
		public IEnumerable<KeyValuePair<string, bool>> Variables
		{
			get
			{
				foreach(var x in m_fragments)
				{
					if (x is Variable)
						yield return new KeyValuePair<string, bool>((x as Variable).Name, (x as Variable).Optional);
					else if (!(x is Variable || x is Literal || x is NamedCapture))
						throw new Exception($"Unable to bind route with elments that are not {typeof(Variable).Name}, {typeof(Literal).Name}, or {typeof(NamedCapture).Name}");
				}
			}
		}

		/// <summary>
		/// Represents a choice between multiple paths
		/// </summary>
		private class Choice : IFragment
		{
			/// <summary>
			/// Gets the full string from the route
			/// </summary>
			public string Value
			{
				get { return string.Join("|", Items.Select(x => string.Join("", x.Select(y => y.Value)))); }
			}

			/// <summary>
			/// One value
			/// </summary>
			public IFragment[][] Items { get; private set; }

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Mvc.RouteParser.Choice"/> class.
			/// </summary>
			/// <param name="first">One value.</param>
			/// <param name="second">Another value.</param>
			public Choice(IFragment[] first, IFragment[] second)
			{
				Items = new[] { first, second };
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Mvc.RouteParser.Choice"/> class.
			/// </summary>
			/// <param name="first">One value.</param>
			/// <param name="second">Another value.</param>
			public Choice(IEnumerable<IFragment[]> items)
			{
				Items = items.ToArray();
			}

			/// <summary>
			/// Gets the fragment as a regular expression.
			/// </summary>
			public string RegularExpression
			{
				get
				{
					return
						string.Join("|",
									Items.Select(x => "(" + string.Join("", x.Select(y => y.RegularExpression)) + ")")
  						);
				}
			}
		}

		/// <summary>
		/// Represents a named capture group
		/// </summary>
		private class NamedCapture : IFragment
		{
			/// <summary>
			/// The name of the group to capture
			/// </summary>
			public string GroupName { get; set; }
			/// <summary>
			/// The match sequence to capture
			/// </summary>
			/// <value>The match.</value>
			public string Match { get; set; }
			/// <summary>
			/// The delimiter value
			/// </summary>
			/// <value>The delimiter.</value>
			public string Delimiter { get; set; }

			/// <summary>
			/// Gets or sets a value indicating whether this <see cref="T:Ceen.Mvc.RouteParser.NamedCapture"/> is escaped.
			/// </summary>
			public bool Escaped { get; set; }

			/// <summary>
			/// Gets or sets a flag indicating if the literal is optional
			/// </summary>
			/// <value><c>true</c> if optional; otherwise, <c>false</c>.</value>
			public bool Optional { get; set; }

			/// <summary>
			/// Gets the value that would represent this capture group
			/// </summary>
			/// <value>The value.</value>
			public string Value { get { return $"{{{GroupName}={Match}}}"; } }

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Mvc.RouteParser.NamedCapture"/> class.
			/// </summary>
			/// <param name="groupname">The groupname to use.</param>
			/// <param name="match">The match expression.</param>
			/// <param name="delimiter">The delimiter expression.</param>
			/// <param name="escaped">A flag indicating if the literal value is already escaped</param>
			/// <param name="optional">A flag indicating if the literal is optional</param>
			public NamedCapture(string groupname, string match, string delimiter, bool escaped = false, bool optional = false)
			{
				GroupName = groupname;
				Match = match;
				Delimiter = string.IsNullOrWhiteSpace(Match) ? string.Empty : delimiter;
				Escaped = escaped;
				Optional = optional;
			}

			/// <summary>
			/// Gets the fragment as a regular expression.
			/// </summary>
			public string RegularExpression
			{
				get
				{
					
					return string.Format($"({Regex.Escape(Delimiter)}(?<{GroupName}>{(Escaped ? Match : Regex.Escape(Match))})){(Optional ? "?" : "")}");
				}
			}
		}

		/// <summary>
		/// The regluar expression that matches variables
		/// </summary>
		private static readonly Regex CURLY_MATCH = new Regex(@"((?<!\{)\{(?!{))(?<name>[^\}]+)\}(?!\})");

		/// <summary>
		/// The list of fragments in this route
		/// </summary>
		private readonly List<IFragment> m_fragments;

		/// <summary>
		/// Gets the value this instance is built from
		/// </summary>
		/// <value>The value.</value>
		public string Value { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.RouteParser"/> class.
		/// </summary>
		/// <param name="fragments">The fragments to base this instance on.</param>
		private RouteParser(List<IFragment> fragments)
		{
			m_fragments = fragments;
			Value = string.Join("", m_fragments.Select(x => x.Value));
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.RouteParser"/> class.
		/// </summary>
		/// <param name="route">The route specification to parse.</param>
		public RouteParser(string route)
		{
			m_fragments = new List<IFragment>();
			Value = route = route ?? string.Empty;

			var ix = 0;
			var slurp = false;
			foreach (Match m in CURLY_MATCH.Matches(route))
			{
				if (slurp)
					throw new Exception($"Cannot have trailing data after slurp: {m.Value}");

				if (ix != m.Index)
					m_fragments.Add(new Literal(route.Substring(ix, m.Index - ix)));

				var v = new Variable(m.Groups["name"].Value);

				if (m_fragments.Count > 0 && m_fragments.Last() is Literal)
				{
					var prev = (Literal)m_fragments.Last();
					v.Delimiter = prev.Value.Substring(prev.Value.Length - 1);
					if (prev.Value.Length == 1)
						m_fragments.RemoveAt(m_fragments.Count - 1);
					else
						m_fragments[m_fragments.Count - 1] = new Literal(prev.Value.Substring(0, prev.Value.Length - 1));

				}
				else if (m_fragments.Count > 0)
					throw new Exception(string.Format("Must have literal spacer between {0} and {1}", m_fragments[m_fragments.Count - 2].Value, v.Value));

				m_fragments.Add(v);

				ix = m.Index + m.Length;
			}

			if (ix != route.Length)
				m_fragments.Add(new Literal(route.Substring(ix, route.Length - ix)));
		}

		/// <summary>
		/// Gets the route as a regular expression.
		/// </summary>
		public string RegularExpression
		{
			get
			{
				return string.Join("", m_fragments.Select(x => x.RegularExpression));
			}
		}

		/// <summary>
		/// Appends a route to another route
		/// </summary>
		/// <param name="first">First.</param>
		/// <param name="second">Second.</param>
		public static RouteParser Append(RouteParser first, RouteParser second)
		{
			if (second.m_fragments.Count == 0)
				return new RouteParser(new List<IFragment>(first.m_fragments));
			if (first.m_fragments.Count == 0)
				return new RouteParser(new List<IFragment>(second.m_fragments));

			var second_lead = second.m_fragments.First() as Literal;
			if (second_lead != null && second_lead.Value.StartsWith("/"))
				return new RouteParser(new List<IFragment>(second.m_fragments));

			var slurper = first.m_fragments.Where(x => x is Variable && (x as Variable).Slurp).FirstOrDefault();
			if (slurper != null && second.m_fragments.Any(x => !(x is Literal)))
				throw new Exception($"Cannot append \"{second.Value}\" to \"{first.Value}\" due to the slurping variable \"{slurper.Value}\"");

			var traillit = first.m_fragments.Last() as Literal;
			var leadlit = second.m_fragments.First() as Literal;

			var trailpath = traillit != null && traillit.Value.EndsWith("/");
			var leadpath = leadlit != null && leadlit.Value.StartsWith("/");

			var list = new List<IFragment>();
			if (trailpath && leadpath)
			{
				list.AddRange(first.m_fragments);
				//list.Add(new Literal(leadlit.Value.Substring(1)));
				list.AddRange(second.m_fragments.Skip(1));
			}
			else
			{
				list.AddRange(first.m_fragments);
				//if (!(trailpath || leadpath))
				//	list.Add(new Literal("/"));
				list.AddRange(second.m_fragments);
			}

			return new RouteParser(list);
		}

		/// <summary>
		/// Appends a route to another route
		/// </summary>
		/// <param name="first">First.</param>
		/// <param name="second">Second.</param>
		public static RouteParser PrependRegex(RouteParser first, string name, string match, string delimiter)
		{
			var list = new List<IFragment>(first.m_fragments);
			list.Insert(0, new NamedCapture(name, match, delimiter, true, false));
			return new RouteParser(list);
		}

		/// <summary>
		/// Combines two routes into one new route
		/// </summary>
		/// <param name="first">First.</param>
		/// <param name="second">Second.</param>
		public static RouteParser Join(RouteParser first, RouteParser second)
		{
			if (first.Value == second.Value)
				return new RouteParser(new List<IFragment>(first.m_fragments));

			var list = new List<IFragment>();
			var max = Math.Min(first.m_fragments.Count, second.m_fragments.Count);

			var i = 0;
			for (; i < max - 1; i++)
			{
				if (first.m_fragments[i].Value == second.m_fragments[i].Value)
					list.Add(first.m_fragments[i]);
				else
					break;
			}

			list.Add(new Choice(
				first.m_fragments.Skip(i).ToArray(),
				second.m_fragments.Skip(i).ToArray()
			));

			return new RouteParser(list);
		}

		/// <summary>
		/// Returns a new RouteParser where some variable is bound to a literal value
		/// </summary>
		/// <param name="varname">The variable to find.</param>
		/// <param name="value">The literal value to use.</param>
		/// <param name="escaped">A flag indicating if the literal value is already escaped</param>
		/// <param name="optional">A flag indicating if the literal is optional</param>
		public RouteParser Bind(string varname, string value, bool escaped = false, bool optional = false)
		{
			return new RouteParser(m_fragments.Select(x => {
				if (x is Variable && (x as Variable).Name == varname)
					return new NamedCapture(varname, value, (x as Variable).Delimiter, escaped, optional);
				else if (x is Variable || x is Literal || x is NamedCapture)
					return x;
				else
					throw new Exception($"Unable to bind route with elments that are not {typeof(Variable).Name}, {typeof(Literal).Name}, or {typeof(NamedCapture).Name}");
			}).ToList());
		}

		/// <summary>
		/// Returns the default value of a named variable
		/// </summary>
		/// <param name="varname">The variable to find.</param>
		public string GetDefaultValue(string varname)
		{
			return m_fragments.Where(x => x is Variable && (x as Variable).Name == varname).Cast<Variable>().Select(x => x.DefaultValue).FirstOrDefault();
		}
	}
}
