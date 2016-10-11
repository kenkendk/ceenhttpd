using System;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ceen
{
	/// <summary>
	/// Extension methods for the Mvc module
	/// </summary>
	public static class MvcExtensionMethods
	{
		/// <summary>
		/// Creates a route instance from an assembly
		/// </summary>
		/// <returns>The route.</returns>
		/// <param name="assembly">The assembly to use.</param>
		/// <param name="config">An optional config.</param>
		public static Ceen.Mvc.ControllerRouter ToRoute(this Assembly assembly, Ceen.Mvc.ControllerRouterConfig config = null)
		{
			return new Mvc.ControllerRouter(config ?? new Mvc.ControllerRouterConfig(), assembly);
		}

		/// <summary>
		/// Creates a route instance from a list of assemblies
		/// </summary>
		/// <returns>The route.</returns>
		/// <param name="assemblies">The assemblies to use.</param>
		/// <param name="config">An optional config.</param>
		public static Ceen.Mvc.ControllerRouter ToRoute(this IEnumerable<Assembly> assemblies, Ceen.Mvc.ControllerRouterConfig config = null)
		{
			return new Mvc.ControllerRouter(config ?? new Mvc.ControllerRouterConfig(), assemblies);
		}

		/// <summary>
		/// Creates a route instance from a list of types
		/// </summary>
		/// <returns>The route.</returns>
		/// <param name="types">The types to use.</param>
		/// <param name="config">An optional config.</param>
		public static Ceen.Mvc.ControllerRouter ToRoute(this IEnumerable<Type> types, Ceen.Mvc.ControllerRouterConfig config = null)
		{
			return new Mvc.ControllerRouter(config ?? new Mvc.ControllerRouterConfig(), types);
		}
		
	}
}

namespace Ceen.Mvc
{
	/// <summary>
	/// Some common Linq methods
	/// </summary>
	public static class LinqHelpers
	{
		/// <summary>
		/// Builds a dictionary where the identical key values overwrite instead of throwing exceptions
		/// </summary>
		/// <returns>The dictionary.</returns>
		/// <param name="self">The list to build from.</param>
		/// <param name="keyselector">The function to extract the key.</param>
		/// <param name="target">An optional target dictionary</param>
		/// <typeparam name="TKey">The key type parameter.</typeparam>
		/// <typeparam name="TItem">The value type parameter.</typeparam>
		public static Dictionary<TKey, TItem> ToSafeDictionary<TKey, TItem>(this IEnumerable<TItem> self, Func<TItem, TKey> keyselector, Dictionary<TKey, TItem> target = null)
		{
			var res = target ?? new Dictionary<TKey, TItem>();
			foreach (var x in self)
				res[keyselector(x)] = x;
			return res;
		}

		/// <summary>
		/// Builds a dictionary where the identical key values overwrite instead of throwing exceptions
		/// </summary>
		/// <returns>The dictionary.</returns>
		/// <param name="self">The list to build from.</param>
		/// <param name="keyselector">The function to extract the key.</param>
		/// <param name="valueselector">The function to extract the value.</param>
		/// <param name="target">An optional target dictionary</param>
		/// <typeparam name="TKey">The key type parameter.</typeparam>
		/// <typeparam name="TValue">The value type parameter.</typeparam>
		/// <typeparam name="TItem">The source type parameter.</typeparam>
		public static Dictionary<TKey, TValue> ToSafeDictionary<TKey, TValue, TItem>(this IEnumerable<TItem> self, Func<TItem, TKey> keyselector, Func<TItem, TValue> valueselector, Dictionary<TKey, TValue> target)
		{
			var res = target ?? new Dictionary<TKey, TValue>();
			foreach (var x in self)
				res[keyselector(x)] = valueselector(x);
			return res;
		}

		/// <summary>
		/// Returns distinct items, by sorting the list and removing duplicate values
		/// </summary>
		/// <param name="self">The list to filter.</param>
		/// <param name="comparevalue">The function to extract the compare parameter.</param>
		/// <param name="comparer">An optional key comparer.</param>
		/// <typeparam name="TItem">The value type parameter.</typeparam>
		/// <typeparam name="TKey">The distinct value type parameter.</typeparam>
		public static IEnumerable<TItem> Distinct<TItem, TKey>(this IEnumerable<TItem> self, Func<TItem, TKey> comparevalue, IComparer<TKey> comparer = null)
		{
			var prev = default(TKey);
			var first = true;
			var cmp = comparer ?? Comparer<TKey>.Default;

			foreach (var item in self.OrderBy(x => comparevalue(x)))
			{
				var k = comparevalue(item);
				if (first || cmp.Compare(prev, k) != 0)
				{
					first = false;
					prev = k;
					yield return item;
				}
			}
		}

		/// <summary>
		/// Gets the covering parent interfaces for a given type
		/// </summary>
		/// <returns>The parent interfaces.</returns>
		/// <param name="self">The type to examine.</param>
		/// <typeparam name="TFilter">An filter to limit the results to a specific type</typeparam>
		public static IEnumerable<Type> GetParentInterfaces<TFilter>(this Type self)
		{
			return GetParentInterfaces(self, typeof(TFilter));
		}

		/// <summary>
		/// Gets the covering parent interfaces for a given type
		/// </summary>
		/// <returns>The parent interfaces.</returns>
		/// <param name="self">The type to examine.</param>
		/// <param name="basetypefilter">An optional filter to limit the results to a specific type.</param>
		public static IEnumerable<Type> GetParentInterfaces(this Type self, Type basetypefilter = null)
		{
			var all = self.GetInterfaces().AsEnumerable();
			if (basetypefilter != null)
				all = all.Where(x => typeof(IControllerPrefix).IsAssignableFrom(x));
			
			return all.Except(all.SelectMany(t => t.GetInterfaces()));
		}

		/// <summary>
		/// Gets a list of parent interfaces
		/// </summary>
		/// <returns>The sequence of parent interfaces.</returns>
		/// <param name="self">The type to start with.</param>
		/// <typeparam name="TFilter">An filter to limit the results to a specific type</typeparam>
		public static IEnumerable<Type> GetParentInterfaceSequence<TFilter>(this Type self)
		{
			return GetParentInterfaceSequence(self, typeof(TFilter));
		}

		/// <summary>
		/// Gets a list of parent interfaces
		/// </summary>
		/// <returns>The sequence of parent interfaces.</returns>
		/// <param name="self">The type to start with.</param>
		/// <param name="basetypefilter">An optional filter to limit the results to a specific type.</param>
		public static IEnumerable<Type> GetParentInterfaceSequence(this Type self, Type basetypefilter = null)
		{
			var cur = self;
			while (cur != null)
			{
				yield return cur;

				var parents = cur.GetParentInterfaces(basetypefilter);

				if (parents.Count() > 1)
					throw new Exception($"Error building prefix map, the type {cur.FullName} has multiple parents");
				cur = parents.FirstOrDefault();
			}
		}

	}

	/// <summary>
	/// Router that can route to a set of controllers
	/// </summary>
	public class ControllerRouter : IRouter, IHttpModule
	{
		/// <summary>
		/// The list of possible controllers
		/// </summary>
		private readonly Controller[] m_controllers;

		/// <summary>
		/// The template used to locate controllers
		/// </summary>
		private readonly ControllerRouterConfig m_config;

		/// <summary>
		/// The fully combined regular expression
		/// </summary>
		private readonly Regex m_fullre;

		/// <summary>
		/// The regular expression used to match prefixes
		/// </summary>
		private readonly Regex m_prefixre;

		/// <summary>
		/// The regular expressions to use after the prefix
		/// </summary>
		private readonly Dictionary<string, Regex> m_subprefixre;

		/// <summary>
		/// The default controller
		/// </summary>
		private string m_defaultcontroller;

		/// <summary>
		/// The default action
		/// </summary>
		private string m_defaultaction;

		/// <summary>
		/// Lookup table for finding target methods,
		/// outer key is &quot;http-verb&quot;,
		/// inner key is &quot;prefix&quot;,
		/// inner key is the &quot;controller&quot;,
		/// inner key is the &quot;action&quot;
		/// </summary>
		private readonly Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<RouteEntry>>>>> m_targets;


		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.ControllerRouter"/> class.
		/// </summary>
		/// <param name="config">The configuration to use</param>
		/// <param name="assembly">The assembly to scan for controllers.</param>
		public ControllerRouter(ControllerRouterConfig config, Assembly assembly)
			: this(config, assembly == null ? new Assembly[0] : new [] { assembly })
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.ControllerRouter"/> class.
		/// </summary>
		/// <param name="config">The configuration to use</param>
		/// <param name="assemblies">The assemblies to scan for controllers.</param>
		public ControllerRouter(ControllerRouterConfig config, IEnumerable<Assembly> assemblies)
			: this(config, assemblies.SelectMany(x => x.GetTypes()).Where(x => x != null && typeof(Controller).IsAssignableFrom(x)))
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.ControllerRouter"/> class.
		/// </summary>
		/// <param name="config">The configuration to use</param>
		/// <param name="types">The types to use, must all derive from <see cref="T:Ceen.Mvc.Controller"/>.</param>
		public ControllerRouter(ControllerRouterConfig config, params Type[] types)
			: this(config, (types ?? new Type[0]).AsEnumerable())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.ControllerRouter"/> class.
		/// </summary>
		/// <param name="config">The configuration to use</param>
		/// <param name="types">The types to use, must all derive from <see cref="T:Ceen.Mvc.Controller"/>.</param>
		public ControllerRouter(ControllerRouterConfig config, IEnumerable<Type> types)
		{
			if (types == null)
				throw new ArgumentNullException($"{types}");
			if (config == null)
				throw new ArgumentNullException($"{config}");

			// Make sure the caller cannot edit the config afterwards
			m_config = config.Clone();

			var prefixes =
				types
					.SelectMany(x => x.GetParentInterfaces<IControllerPrefix>())
					.Select(x => new
					{
						Type = x,
					    Prefix = "/" + string.Join(
							 "/",
							 x.GetParentInterfaceSequence()
							 .Where(y => y != typeof(IControllerPrefix))
							 .Select(y => GetRouteName(y, m_config))
							 .Reverse()
						)
					})
					.Distinct()
					.ToDictionary(x => x.Type, x => x.Prefix);

			if (types.Any(x => !x.GetParentInterfaces<IControllerPrefix>().Any()) && !prefixes.Values.Contains(string.Empty))
				prefixes[typeof(Controller)] = string.Empty;

			var basetemplate = RouteParser.Append(new RouteParser("{" + m_config.PrefixGroupName + "}"), new RouteParser(m_config.Template));

				//RouteParser.PrependRegex(new RouteParser(m_config.Template), m_config.PrefixGroupName, string.Join("|", prefixes.Values.Select(x => string.IsNullOrWhiteSpace(x) ? "()" : Regex.Escape(x))), string.Empty);
			var re = new Regex(basetemplate.RegularExpression);

			if (re.GetGroupNames().Count(x => x == m_config.ControllerGroupName) != 1)
				throw new ArgumentException($"The template must contain exactly 1 named group called {m_config.ControllerGroupName}");
			if (re.GetGroupNames().Count(x => x == m_config.ActionGroupName) != 1)
				throw new ArgumentException($"The template must contain exactly 1 named group called {m_config.ActionGroupName}");

			m_defaultcontroller = basetemplate.GetDefaultValue(m_config.ControllerGroupName);
			m_defaultaction = basetemplate.GetDefaultValue(m_config.ActionGroupName);

			types = types.Where(x => x != null).Distinct().ToArray();
			if (types.Count() == 0)
				throw new ArgumentException($"No controller entries to load from \"{types}\"");

			var wrong = types.Where(x => !typeof(Controller).IsAssignableFrom(x)).FirstOrDefault();
			if (wrong != null)
				throw new ArgumentException($"The type \"{wrong.FullName}\" does not derive from {typeof(Controller).FullName}");

			m_controllers = types.Select(x => (Controller)Activator.CreateInstance(x)).ToArray();

			var targetmethods = m_controllers
				.SelectMany(x =>
							x.GetType()
							.GetMethods(BindingFlags.Public | BindingFlags.Instance)
				   			.Where(y => y.ReturnType == typeof(void) || typeof(IResult).IsAssignableFrom(y.ReturnType) || typeof(Task).IsAssignableFrom(y.ReturnType))
							.Select(y =>
							{
								// Extract target method name
								string name;
								var nameattr = y.GetCustomAttributes(typeof(NameAttribute), false).Cast<NameAttribute>().FirstOrDefault();
								if (nameattr != null)
									name = nameattr.Name;
								else
									name = m_config.LowerCaseNames ? y.Name.ToLowerInvariant() : y.Name;

								return new { Name = name, Item = x, Method = y };
							})
			);

			var targetmethodroutes =
				targetmethods.SelectMany(x =>
				{
					var routes = x.Method.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>().Select(y => y.Route);
					// Add default route, if there are no route attributes
					if (routes.Count() == 0)
						routes = new[] { string.Empty };

					return routes.Distinct().Select(y => new
					{
						Route = new RouteParser(y),
						Name = x.Name,
						Item = x.Item,
						Method = x.Method
					});
				});

			var fulllist = targetmethodroutes.SelectMany(x =>
			{
				var routes = x.Item.GetType().GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>();
				if (routes.Count() == 0)
					routes = new[] { new RouteAttribute(string.Empty) };

				string name;
				var nameattr = x.Item.GetType().GetCustomAttributes(typeof(NameAttribute), false).Cast<NameAttribute>().FirstOrDefault();
				// Extract controller name
				if (nameattr != null)
					name = nameattr.Name;
				else
				{
					name = x.Item.GetType().Name;
					if (m_config.ControllerSuffixRemovals != null)
						foreach (var rm in m_config.ControllerSuffixRemovals)
							while (!string.IsNullOrWhiteSpace(rm) && name.EndsWith(rm, StringComparison.InvariantCultureIgnoreCase))
								name = name.Substring(0, name.Length - rm.Length);

					if (m_config.LowerCaseNames)
						name = name.ToLowerInvariant();
				}

				return routes.Distinct().Select(y => new
				{
					Route = RouteParser.Append(new RouteParser(y.Route), x.Route),
					ControllerName = name,
					Controller = x.Item,
					ActionName = x.Name,
					ActionMethod = x.Method
				});
			});

			var allwithverbs = fulllist.SelectMany(x =>
			{
				var methodverbs = x.ActionMethod.GetCustomAttributes(typeof(HttpVerbFilterAttribute), false).Cast<HttpVerbFilterAttribute>().Select(b => b.Verb.ToUpperInvariant());
				if (methodverbs.Count() == 0)
					methodverbs = new[] { string.Empty };

				return methodverbs.Distinct().Select(verb =>
					 new RouteEntry(
						x.Route,
						x.Controller,
						x.ControllerName,
						x.ActionMethod,
						x.ActionName,
						verb,
						null
					)
				);
			});

			var allwithprefix = prefixes
				.SelectMany(
					x => allwithverbs
					.Where(y => 
					       !y.Controller.GetType().GetParentInterfaces<IControllerPrefix>().Any() 
					       || 
					       y.Controller.GetType().GetInterfaces().Contains(x.Key)
			        )
			        .Select(z => new RouteEntry(
						z.Route,
						z.Controller,
						z.ControllerName,
						z.Action.Method,
						z.ActionName,
						z.Verb,
						x.Value
					)
		       	))
	            .OrderBy(x => x.ControllerName)
	            .OrderByDescending(x => x.ActionName)
	            .OrderByDescending(x => x.Route.Value.Length)
	            .ToArray();

			m_prefixre = new Regex(string.Join("|", allwithprefix.Select(x => x.Prefix).Distinct().Select(x => string.IsNullOrWhiteSpace(x) ? "()" : Regex.Escape(x))));

			m_subprefixre =
				allwithprefix
					 .GroupBy(x => x.Prefix)
					 .ToDictionary(
						x => x.Key,
						x => 
							new Regex(basetemplate
							.Bind(m_config.PrefixGroupName, x.Key.StartsWith("/") ? x.Key.Substring(1) : x.Key, false, false)
				            .Bind(m_config.ControllerGroupName, 
						          string.Join(
							          "|", 
							          x
							          .Where(z => !m_config.HideDefaultController || z.ControllerName != m_defaultcontroller).Distinct()
							          .Select(z => $"({Regex.Escape(z.ControllerName)})").Distinct()), true, !string.IsNullOrWhiteSpace(m_defaultcontroller)
						         )
							.Bind(m_config.ActionGroupName, string.Join(
							              "|", 
							              x
							              .Where(z => !m_config.HideDefaultAction || z.ActionName != m_defaultaction).Distinct()
							              .Select(z => $"({Regex.Escape(z.ActionName)})").Distinct()), true, !string.IsNullOrWhiteSpace(m_defaultaction)
						         )
			               .RegularExpression
						)
					);
				             

			var controllernames = allwithverbs.Select(x => x.ControllerName).Where(x => !m_config.HideDefaultController || x != m_defaultcontroller).Distinct().ToList();
			var actionnames = allwithverbs.Select(x => x.ActionName).Where(x => !m_config.HideDefaultAction || x != m_defaultaction).Distinct().ToList();

			if (!string.IsNullOrWhiteSpace(m_defaultcontroller))
				controllernames.Add(string.Empty);
			if (!string.IsNullOrWhiteSpace(m_defaultaction))
				actionnames.Add(string.Empty);

			// Build expression with all controller/action names
			var bound = basetemplate
				.Bind(m_config.ControllerGroupName, string.Join("|", controllernames.Select(x => $"({Regex.Escape(x)})")), true, !string.IsNullOrWhiteSpace(m_defaultcontroller))
				.Bind(m_config.ActionGroupName, string.Join("|", actionnames.Select(x => $"({Regex.Escape(x)})")), true, !string.IsNullOrWhiteSpace(m_defaultaction));

			// Build regex with all controller/action names
			m_fullre = new Regex(bound.RegularExpression);

			// Build lookup table with verb, controller, action
			m_targets = allwithprefix
				.GroupBy(verb => verb.Verb)
				.ToDictionary(
					verb => verb.Key,
					verb => verb
						.GroupBy(prefix => prefix.Prefix)
						.ToDictionary(
							prefix => prefix.Key,
							prefix => prefix
								.GroupBy(controller => controller.ControllerName)
								.ToDictionary(
									controller => controller.Key,
									controller => controller
											  .GroupBy(action => action.ActionName)
											  .ToDictionary(
												  action => action.Key,
												  action => action.ToList()
											   )
								)
						)
				);
		}

		/// <summary>
		/// Helper method to extract the name from a routing component
		/// </summary>
		/// <returns>The route name.</returns>
		/// <param name="type">The type to examine.</param>
		/// <param name="config">The server config to use.</param>
		/// <param name="remove_prefixes"></param>
		private static string GetRouteName(Type type, ControllerRouterConfig config)
		{
			string name;
			var nameattr = type.GetCustomAttributes(typeof(NameAttribute), false).Cast<NameAttribute>().FirstOrDefault();
			if (nameattr != null)
				name = nameattr.Name;
			else
			{
				name = type.Name;
				if (config.ControllerSuffixRemovals != null)
					foreach (var rm in config.ControllerSuffixRemovals)
						while (!string.IsNullOrWhiteSpace(rm) && name.EndsWith(rm, StringComparison.InvariantCultureIgnoreCase))
							name = name.Substring(0, name.Length - rm.Length);

				if (config.LowerCaseNames)
					name = name.ToLowerInvariant();
			}

			return name;
		}

		/// <summary>
		/// Handles a request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The exexcution context.</param>
		public Task<bool> HandleAsync(IHttpContext context)
		{
			return Process(context);
		}

		/// <summary>
		/// Attempts to route the request to a controller instance.
		/// </summary>
		/// <param name="context">The exexcution context.</param>
		public async Task<bool> Process(IHttpContext context)
		{
			var pre = m_prefixre.Match(context.Request.Path);
			if (!pre.Success || pre.Index != 0)
				return false;

			var prefix = pre.Value;
			Regex targetre;
			if (!m_subprefixre.TryGetValue(prefix, out targetre))
				return false;

			var m = targetre.Match(context.Request.Path);
			if (!m.Success || m.Index != 0)
				return false;

			var m2 = m_fullre.Match(context.Request.Path);

			//if (!(m.Groups[m_config.ControllerGroupName].Success)) // && m.Groups[m_config.ActionGroupName].Success && m.Groups[m_config.PrefixGroupName].Success))
			//	return false;

			var controller = m.Groups[m_config.ControllerGroupName].Value;
			if (string.IsNullOrWhiteSpace(controller))
			    controller = m_defaultcontroller ?? string.Empty;

			var action = m.Groups[m_config.ActionGroupName].Value;
			if (string.IsNullOrWhiteSpace(action))
				action = m_defaultaction ?? string.Empty;

			//var prefix = m.Groups[m_config.PrefixGroupName].Value;

			var candidates = GetTargets(context.Request.Method, prefix, controller, action);
			if (candidates == null || candidates.Count == 0)
				candidates = GetTargets(string.Empty, prefix, controller, action);

			if (candidates == null || candidates.Count == 0)
				return false;

			var subpath = context.Request.Path.Substring(m.Length);
			if (!subpath.StartsWith("/"))
				subpath = "/" + subpath;

			var best = candidates.Select(x => BuildPathDictionary(x, subpath, m))
	                .Where(x => x.Key != null)
                    .OrderByDescending(x => x.Value == null ? 0 : x.Value.Count)
					.FirstOrDefault();

			if (best.Key == null)
				return false;

			await HandleWithMethod(context, best.Key.Action, best.Key.Controller, best.Value);
			return true;
		}

		/// <summary>
		/// Builds a dictionary of arguments that extracted from the URL
		/// </summary>
		/// <returns>The argument lookup dictionary.</returns>
		/// <param name="route">The route to build the dictionary for.</param>
		/// <param name="subpath">The part of the path that was not parsed by the parent.</param>
		/// <param name="parentmatch">The match from the parent matching.</param>
		private KeyValuePair<RouteEntry, Dictionary<string, string>> BuildPathDictionary(RouteEntry route, string subpath, Match parentmatch)
		{			
			var match = route.RegularExpression.Match(subpath);
			if (subpath != "/" && match.Length != subpath.Length)
				return default(KeyValuePair<RouteEntry, Dictionary<string, string>>);
		
			if (route.Action.ArgumentCount == 0)
				return new KeyValuePair<RouteEntry, Dictionary<string, string>>(route, null);

			var items = new Dictionary<string, string>();
			foreach (var p in route.Action.Parameters)
			{
				if (p.IsContextParameter || !p.Source.HasFlag(ParameterSource.Url))
					continue;

				var m1 = parentmatch.Groups[p.Name];
				if (m1.Success)
					items[p.Name] = m1.Value;
				var m2 = match.Groups[p.Name];
				if (m2.Success)
					items[p.Name] = m2.Value;

				var defval = m1.Success || m2.Success ? null : route.Route.GetDefaultValue(p.Name);
				if (!string.IsNullOrWhiteSpace(defval))
					items[p.Name] = defval;

				// Skip it if it does not match
				if (p.Required && ((p.Source & (~ParameterSource.Url)) == 0) && !m1.Success && !m2.Success && string.IsNullOrWhiteSpace(defval))
					return default(KeyValuePair<RouteEntry, Dictionary<string, string>>);
			}

			return
				new KeyValuePair<RouteEntry, Dictionary<string, string>>(route, items);
		}

		/// <summary>
		/// Finds the method target for a given action
		/// </summary>
		/// <returns>The target methods.</returns>
		/// <param name="verb">The HTTP verb to match.</param>
		/// <param name="verb">The path prefix to match.</param>
		/// <param name="controller">The controller to use.</param>
		/// <param name="action">The action to use.</param>
		private List<RouteEntry> GetTargets(string verb, string prefix, string controller, string action)
		{
			Dictionary<string, Dictionary<string, Dictionary<string, List<RouteEntry>>>> prefixlist;
			if (!m_targets.TryGetValue(verb, out prefixlist))
				return null;

			Dictionary<string, Dictionary<string, List<RouteEntry>>> controllerlist;
			if (!prefixlist.TryGetValue(prefix, out controllerlist))
				return null;

			Dictionary<string, List<RouteEntry>> actionlist;
			if (!controllerlist.TryGetValue(controller, out actionlist))
				return null;

			List<RouteEntry> methodlist;
			if (!actionlist.TryGetValue(action, out methodlist))
				return null;

			return methodlist;
		}

		/// <summary>
		/// Handles the actual method invocation, once a method has been selected
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The execution context.</param>
		/// <param name="method">The method to invoke.</param>
		/// <param name="controller">The controller instance to use.</param>
		/// <param name="urlmatch">The parent url match</param>
		private async Task HandleWithMethod(IHttpContext context, MethodEntry method, Controller controller, Dictionary<string, string> urlmatch)
		{
			// Apply each argument in turn
			var values = new object[method.ArgumentCount];

			for (var ix = 0; ix < values.Length; ix++)
			{
				var e = method.Parameters[ix];
				string val;

				if (typeof(IHttpContext).IsAssignableFrom(e.Parameter.ParameterType))
					values[ix] = context;
				else if (typeof(IHttpRequest).IsAssignableFrom(e.Parameter.ParameterType))
					values[ix] = context.Request;
				else if (typeof(IHttpResponse).IsAssignableFrom(e.Parameter.ParameterType))
					values[ix] = context.Response;
				else if (e.Source.HasFlag(ParameterSource.Url) && urlmatch != null && urlmatch.TryGetValue(e.Name, out val))
					ApplyArgument(method.Method, e, val, values);
				else if (e.Source.HasFlag(ParameterSource.Header) && context.Request.Headers.TryGetValue(e.Name, out val))
					ApplyArgument(method.Method, e, val, values);
				else if (e.Source.HasFlag(ParameterSource.Form) && context.Request.Form.TryGetValue(e.Name, out val))
					ApplyArgument(method.Method, e, val, values);
				else if (e.Source.HasFlag(ParameterSource.Query) && context.Request.QueryString.TryGetValue(e.Name, out val))
					ApplyArgument(method.Method, e, val, values);
				else if (e.Required)
					throw new HttpException(HttpStatusCode.BadRequest, $"Missing mandatory parameter {e.Name}");
				else if (e.Parameter.HasDefaultValue)
					values[e.ArgumentIndex] = e.Parameter.DefaultValue;
				else
					values[e.ArgumentIndex] = e.Parameter.ParameterType.IsValueType ? Activator.CreateInstance(e.Parameter.ParameterType) : null;
			}

			var res = method.Method.Invoke(controller, values);
			if (res == null)
				return;

			if (res is IResult)
				await((IResult)res).Execute(context);
			else if (res is Task<IResult>)
			{
				res = await(Task<IResult>)res;
				if (res as IResult != null)
					await((IResult)res).Execute(context);
			}
			else if (res is Task)
				await(Task)res;
		}

		/// <summary>
		/// Applies the argument to the value list.
		/// </summary>
		/// <param name="entry">The argument entry.</param>
		/// <param name="name">The name of the argument.</param>
		/// <param name="value">The argument value.</param>
		/// <param name="values">The list of values to process.</param>
		private static void ApplyArgument(MethodInfo method, ParameterEntry entry, string value, object[] values)
		{
			var argtype = method.GetParameters()[entry.ArgumentIndex].ParameterType;
			try
			{
				values[entry.ArgumentIndex] = Convert.ChangeType(value, argtype);
			}
			catch (Exception)
			{
				throw new HttpException(HttpStatusCode.BadRequest, $"The value \"{value}\" for {entry.Name} is not a valid {argtype.Name.ToLower()}");
			}
		}
	}
}
