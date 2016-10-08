using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using Ceen.Common;

namespace Ceen.Httpd
{
	/// <summary>
	/// Implementation of a simple regexp based router
	/// </summary>
	public class Router : IRouter
	{
		/// <summary>
		/// List of rules
		/// </summary>
		private readonly Tuple<Regex, IHttpModule>[] m_rules;

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.Router"/> class.
		/// </summary>
		/// <param name="rules">The routing rules.</param>
		public Router(IEnumerable<Tuple<string, IHttpModule>> rules)
		{
			m_rules = rules.Select(x => {
				if (x.Item1.StartsWith("[") && x.Item1.EndsWith("]"))
					return new Tuple<Regex, IHttpModule>(new Regex(x.Item1.Substring(1, x.Item1.Length - 2)), x.Item2);
				else
					return new Tuple<Regex, IHttpModule>(new Regex(Regex.Escape(x.Item1)), x.Item2);
			}).ToArray();
		}

		/// <summary>
		/// Process the specified request.
		/// </summary>
		/// <param name="request">Request.</param>
		/// <param name="response">Response.</param>
		/// <returns><c>True</c> if the processing was handled, false otherwise</returns>
		public async Task<bool> Process(IHttpContext context)
		{
			foreach (var rule in m_rules)
			{
				var m = rule.Item1.Match(context.Request.Path);
				if (m.Success && m.Length == context.Request.Path.Length)
				{
					if (await rule.Item2.HandleAsync(context))
						return true;
				}
			}

			return false;
		}
	}
}

