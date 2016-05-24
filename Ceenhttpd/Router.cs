using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

namespace Ceenhttpd
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
					return new Tuple<Regex, IHttpModule>(new Regex(Regex.Escape(x.Item1) + ".*"), x.Item2);
			}).ToArray();
		}

		/// <summary>
		/// Process the specified request.
		/// </summary>
		/// <param name="request">Request.</param>
		/// <param name="response">Response.</param>
		/// <returns><c>True</c> if the processing was handled, false otherwise</returns>
		public async Task<bool> Process(HttpRequest request, HttpResponse response)
		{
			foreach (var rule in m_rules)
			{
				var m = rule.Item1.Match(request.Path);
				if (m.Success && m.Length == request.Path.Length)
				{
					if (rule.Item2 is IRestHandler)
					{
						if ("GET".Equals(request.Method))
						{
							if (!(rule.Item2 is IRestGetHandler))
								continue;

							if (await (rule.Item2 as IRestGetHandler).HandleGetAsync(request, response))
								return true;
						}
						else if ("PUT".Equals(request.Method))
						{
							if (!(rule.Item2 is IRestPutHandler))
								continue;
							
							if (await (rule.Item2 as IRestPutHandler).HandlePutAsync(request, response))
								return true;
						}
						else if ("POST".Equals(request.Method))
						{
							if (!(rule.Item2 is IRestPostHandler))
								continue;

							if (await (rule.Item2 as IRestPostHandler).HandlePostAsync(request, response))
								return true;
						}
						else if ("PATCH".Equals(request.Method))
						{
							if (!(rule.Item2 is IRestPatchHandler))
								continue;

							if (await (rule.Item2 as IRestPatchHandler).HandlePatchAsync(request, response))
								return true;
						}
						else if ("DELETE".Equals(request.Method))
						{
							if (!(rule.Item2 is IRestDeleteHandler))
								continue;

							if (await (rule.Item2 as IRestDeleteHandler).HandleDeleteAsync(request, response))
								return true;
						}
						else
							continue;

					}
					if (await rule.Item2.HandleAsync(request, response))
						return true;
				}
			}

			return false;
		}
	}
}

