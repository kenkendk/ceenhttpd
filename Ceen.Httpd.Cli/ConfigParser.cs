using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace Ceen.Httpd.Cli
{
	/// <summary>
	/// Implementation of methods relating to parsing the config file
	/// </summary>
	public static class ConfigParser
	{
		/// <summary>
		/// The types the parser supports + enums
		/// </summary>
		private static readonly Type[] SUPPORTED_RETURN_TYPES = new Type[] { typeof(string), typeof(int), typeof(bool) };

		/// <summary>
		/// Helper method to get all properties in a case-insensitive dictionary
		/// </summary>
		/// <returns>The property map.</returns>
		/// <typeparam name="T">The type to build the map for.</typeparam>
		private static Dictionary<string, PropertyInfo> BuildPropertyMap<T>()
		{
			return BuildPropertyMap(typeof(T));
		}

		/// <summary>
		/// Helper method to get all properties in a case-insensitive dictionary
		/// </summary>
		/// <returns>The property map.</returns>
		/// <param name="item">The type to build the map for.</param>
		private static Dictionary<string, PropertyInfo> BuildPropertyMap(Type item)
		{
			return item
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(x => SUPPORTED_RETURN_TYPES.Contains(x.PropertyType) || x.PropertyType.IsEnum)
				.ToDictionary(x => x.Name.ToLowerInvariant(), x => x, StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Sets a property on the object, using the name and a string representation of the value
		/// </summary>
		/// <param name="instance">The object to set the property on.</param>
		/// <param name="propertyname">The name of the property.</param>
		/// <param name="value">The value to set.</param>
		public static void SetProperty(object instance, string propertyname, string value)
		{
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));

			PropertyInfo prop;
			BuildPropertyMap(instance.GetType()).TryGetValue(propertyname, out prop);
			if (prop == null)
				throw new Exception($"Did not find a property with the name {propertyname} in type {instance.GetType().FullName}");
			SetProperty(instance, prop, value);
		}

		/// <summary>
		/// Sets a property on the object, using the name and a string representation of the value
		/// </summary>
		/// <param name="instance">The object to set the property on.</param>
		/// <param name="prop">The property to set.</param>
		/// <param name="value">The value to set.</param>
		public static void SetProperty(object instance, PropertyInfo prop, string value)
		{
			prop.SetValue(instance, ArgumentFromString(value, prop.PropertyType));
		}


		/// <summary>
		/// Creates an argument of a specific type by parsing the string
		/// </summary>
		/// <returns>The parsed object.</returns>
		/// <param name="value">The string to parse.</param>
		/// <typeparam name="T">The type to parse to.</typeparam>
		public static T ArgumentFromString<T>(string value)
		{
			return (T)ArgumentFromString(value, typeof(T));
		}

		/// <summary>
		/// Creates an argument of a specific type by parsing the string
		/// </summary>
		/// <returns>The parsed object.</returns>
		/// <param name="value">The string to parse.</param>
		/// <param name="targettype">The type to parse to.</param>
		public static object ArgumentFromString(string value, Type targettype)
		{
			if (targettype.IsEnum)
			{
				var entries = ExpandEnvironmentVariables(value ?? "")
				                         .Split('|')
				                         .Select(x => Enum.Parse(targettype, x, true))
				                         .ToArray();
				if (entries.Length == 1)
					return entries.First();
				else
					return Enum.ToObject(
						targettype,
						entries.Select(x => (int)Convert.ChangeType(x, typeof(int))).Sum()
					);
					
			}
				
			return Convert.ChangeType(ExpandEnvironmentVariables(value), targettype);
		}

		/// <summary>
		/// Create an instance of an object, using constructor arguments and properties
		/// </summary>
		/// <returns>The created instance.</returns>
		/// <param name="itemtype">The type to create.</param>
		/// <param name="constructorargs">Arguments to pass to the constructor.</param>
		/// <param name="targettype">The type the instance should be assignable to.</param>
		private static object CreateInstance(Type itemtype, List<string> constructorargs, Type targettype)
		{
			constructorargs = constructorargs ?? new List<string>();

			if (!targettype.IsAssignableFrom(itemtype))
				throw new Exception($"Found class {itemtype.FullName} in assembly {itemtype.Assembly.FullName}, but it does not implement ${typeof(ILogger).FullName}");

			var cons = itemtype.GetConstructors().Where(x => x.GetParameters().Length == constructorargs.Count).ToArray();
			if (cons.Length == 0)
				throw new Exception($"Failed to load the class named {itemtype.FullName} from assembly {itemtype.Assembly.FullName} as there were no matching constructors");
			if (cons.Length != 1)
				throw new Exception($"Failed to load the class named {itemtype.FullName} from assembly {itemtype.Assembly.FullName} as there were {cons.Length} matching constructors");

			var args = cons.First().GetParameters().Zip(constructorargs, (arg1, arg2) => ArgumentFromString(arg2, arg1.ParameterType)).ToArray();
			return Activator.CreateInstance(itemtype, args);		
		}

		/// <summary>
		/// Create an instance of an object, using constructor arguments and properties
		/// </summary>
		/// <returns>The created instance.</returns>
		/// <param name="assemblyname">The name of the assembly where the type is in.</param>
		/// <param name="classname">The name of the class to create.</param>
		/// <param name="constructorargs">Arguments to pass to the constructor.</param>
		/// <param name="targettype">The type the instance should be assignable to.</param>
		private static object CreateInstance(string assemblyname, string classname, List<string> constructorargs, Type targettype)
		{
			return CreateInstance(ResolveType(assemblyname, classname), constructorargs, targettype);
		}

		/// <summary>
		/// Resolves the type given the class and assembly names.
		/// </summary>
		/// <returns>The resolved type.</returns>
		/// <param name="assemblyname">The name of the assembly where the type is in.</param>
		/// <param name="classname">The name of the class to create.</param>
		private static Type ResolveType(string assemblyname, string classname)
		{
			var itemtype = Type.GetType($"{classname}, {assemblyname}", false);
			if (itemtype == null)
				throw new Exception($"Failed to find the class named {classname} in assembly {assemblyname}");
			return itemtype;
		}

		/// <summary>
		/// Sets all properties on an instance
		/// </summary>
		/// <param name="target">The instance to set properties on.</param>
		/// <param name="options">The list of properties to set.</param>
		public static void SetProperties(object target, Dictionary<string, string> options)
		{
			var lookup = BuildPropertyMap(target.GetType());

			foreach (var kv in options)
			{
				if (!lookup.ContainsKey(kv.Key))
					throw new Exception($"Cannot find property named {kv.Key} in type {target.GetType().FullName}");

				SetProperty(target, lookup[kv.Key], kv.Value);
			}
		}

		/// <summary>
		/// Parse a text file and build a configuration instance
		/// </summary>
		/// <returns>The parsed instance.</returns>
		/// <param name="path">The path to the file to read.</param>
		public static CLIServerConfiguration ParseTextFile(string path)
		{
			var cfg = new CLIServerConfiguration();
			var line = string.Empty;
			var re = new System.Text.RegularExpressions.Regex(@"(?<comment>\#.*)|(\""(?<value>[^\""]*)\"")|((?<value>[^ ]*))");

			var s1names = BuildPropertyMap<CLIServerConfiguration>();
			var s2names = BuildPropertyMap<ServerConfig>();

			var lineindex = 0;
			Dictionary<string, string> lastitemprops = null;
			using (var fs = File.OpenRead(path))
			using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8, true))
				while ((line = sr.ReadLine()) != null)
				{
					var args = re.Matches(line)
								 .Cast<System.Text.RegularExpressions.Match>()
								 .Where(x => x.Groups["value"].Success && !string.IsNullOrWhiteSpace(x.Value))
				                 .Select(x => x.Groups["value"].Value)
								 .ToArray();
					lineindex++;
					if (args.Length == 0)
						continue;

					var cmd = args.First();
					if (s1names.ContainsKey(cmd))
					{
						if (args.Length > 2)
							throw new Exception($"Too many arguments in line {lineindex}: {line}");
						SetProperty(cfg, s1names[cmd], args.Skip(1).FirstOrDefault());
					}
					else if (s2names.ContainsKey(cmd))
					{
						if (args.Length > 2)
							throw new Exception($"Too many arguments in line {lineindex}: {line}");

						cfg.ServerOptions[cmd] = args.Skip(1).FirstOrDefault();
					}
					else if (string.Equals(cmd, "route", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length == 2)
						{
							var route = new RouteDefinition() { Assembly = args.Skip(1).First() };
							cfg.Routes.Add(route);
							lastitemprops = route.RouteOptions;
						}
						else
						{
							if (args.Length < 3)
								throw new Exception($"Too few arguments in line {lineindex}: {line}");

							var route = new RouteDefinition()
							{
								Assembly = args.Skip(1).First(),
								Classname = args.Skip(2).First(),
								ConstructorArguments = args.Skip(3).ToList()
							};

							cfg.Routes.Add(route);
							lastitemprops = route.RouteOptions;
						}

					}
					else if (string.Equals(cmd, "handler", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 4)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");

						var route = new RouteDefinition()
						{
							RoutePrefix = args.Skip(1).First(),
							Assembly = args.Skip(2).First(),
							Classname = args.Skip(3).First(),
							ConstructorArguments = args.Skip(4).ToList()
						};

						cfg.Routes.Add(route);
						lastitemprops = route.RouteOptions;
					}
					else if (string.Equals(cmd, "logger", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 3)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");

						var logger = new LoggerDefinition()
						{
							Assembly = args.Skip(1).First(),
							Classname = args.Skip(2).First(),
							ConstructorArguments = args.Skip(3).ToList()
						};

						cfg.Loggers.Add(logger);
						lastitemprops = logger.LoggerOptions;
					}
					else if (string.Equals(cmd, "set", StringComparison.OrdinalIgnoreCase))
					{
						if (lastitemprops == null)
							throw new Exception($"There was no active entry to set properties for in line {lineindex}");

						if (args.Length > 3)
							throw new Exception($"Too many arguments in line {lineindex}: {line}");
						if (args.Length < 2)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");

						lastitemprops[args.Skip(1).First()] = args.Skip(2).FirstOrDefault();
					}
					else if (string.Equals(cmd, "serve", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 3)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");
						if (args.Length > 3)
							throw new Exception($"Too many arguments in line {lineindex}: {line}");

						var routearg = args.Skip(1).First();
						if (!routearg.StartsWith("/", StringComparison.Ordinal))
							throw new Exception($"The route must start with a forward slash in line {lineindex}: {line}");
						while (routearg.Length > 1 && routearg.EndsWith("/", StringComparison.Ordinal))
							routearg = routearg.Substring(0, routearg.Length - 1);
					
						var pathprefix = routearg;
						
						routearg = routearg == "/" ? "[/.*]" : $"[{routearg}(/.*)?]";

						var route = new RouteDefinition()
						{
							RoutePrefix = routearg,
							Assembly = typeof(Ceen.Httpd.Handler.FileHandler).Assembly.GetName().Name,
							Classname = typeof(Ceen.Httpd.Handler.FileHandler).FullName,
							ConstructorArguments = args.Skip(2).ToList()
						};

						cfg.Routes.Add(route);
						lastitemprops = route.RouteOptions;
						lastitemprops.Add(nameof(Ceen.Httpd.Handler.FileHandler.PathPrefix), pathprefix);
					}
					else if (string.Equals(cmd, "redirect", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 3)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");
						if (args.Length > 3)
							throw new Exception($"Too many arguments in line {lineindex}: {line}");

						var routearg = args.Skip(1).First();

						var route = new RouteDefinition()
						{
							RoutePrefix = routearg,
							Assembly = typeof(Ceen.Httpd.Handler.RedirectHandler).Assembly.GetName().Name,
							Classname = typeof(Ceen.Httpd.Handler.RedirectHandler).FullName,
							ConstructorArguments = args.Skip(2).ToList()
						};

						cfg.Routes.Add(route);
						lastitemprops = route.RouteOptions;
					}
					else if (string.Equals(cmd, "mime", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 3)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");
						if (args.Length > 3)
							throw new Exception($"Too many arguments in line {lineindex}: {line}");

						var key = args.Skip(1).First();
						if (!string.IsNullOrEmpty(key) && key != "*" && !key.StartsWith(".", StringComparison.Ordinal))
							key = "." + key;
					
						cfg.MimeTypes[key] = args.Skip(2).First();
					}
					else if (string.Equals(cmd, "header", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 3)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");
						if (args.Length > 3)
							throw new Exception($"Too many arguments in line {lineindex}: {line}");

						cfg.DefaultHeaders[args.Skip(1).First()] = args.Skip(2).First();
					}
					else if (string.Equals(cmd, "index", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 2)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");
						if (args.Length > 2)
							throw new Exception($"Too many arguments in line {lineindex}: {line}");

						cfg.IndexDocuments.Add(args.Skip(1).First());
					}
					else
					{
						throw new Exception($"Unable to parse the action \"{cmd}\" for line {lineindex}: \"{line}\"");
					}
				}


			cfg.Basepath = Path.GetFullPath(cfg.Basepath ?? ".");
			cfg.Assemblypath = string.Join(Path.PathSeparator.ToString(), (cfg.Assemblypath ?? "").Split(new char[] { Path.PathSeparator }).Select(x => Path.GetFullPath(Path.Combine(cfg.Basepath, ExpandEnvironmentVariables(x)))));

			return cfg;
		}

		/// <summary>
		/// Parse an IP Address, supporting &quot;any&quot;, &quot;*&quot;, &quot;local&quot;, and &quot;loopback&quot;
		/// </summary>
		/// <returns>The IP Address.</returns>
		/// <param name="address">The adress to parse.</param>
		public static IPAddress ParseIPAddress(string address)
		{
			var enabled = !string.IsNullOrWhiteSpace(address);
			var addr = IPAddress.Loopback;

			if (new string[] { "any", "*" }.Contains(address, StringComparer.OrdinalIgnoreCase))
				addr = IPAddress.Any;
			else if (new string[] { "local", "loopback" }.Contains(address, StringComparer.OrdinalIgnoreCase))
				addr = IPAddress.Loopback;
			else if (enabled)
				addr = IPAddress.Parse(address);

			return addr;
		}

		/// <summary>
		/// Creates the server config instance, but does not attempt to load assemblies
		/// </summary>
		/// <returns>The server config.</returns>
		/// <param name="config">The CLI configuration.</param>
		public static ServerConfig CreateServerConfig(CLIServerConfiguration config)
		{
			var enablehttp = !string.IsNullOrWhiteSpace(config.HttpAddress);
			var enablehttps = !string.IsNullOrWhiteSpace(config.HttpsAddress);

			if (!(enablehttp || enablehttps))
				throw new Exception("Either http or https must be enabled");
			if (enablehttp && (config.HttpPort < 0 || config.HttpPort > ushort.MaxValue))
				throw new Exception($"Invalid value for http port: {config.HttpPort}");
			if (enablehttps && (config.HttpsPort < 0 || config.HttpsPort > ushort.MaxValue))
				throw new Exception($"Invalid value for https port: {config.HttpsPort}");

			var cfg = new ServerConfig();

			if (enablehttps && !string.IsNullOrWhiteSpace(config.CertificatePath))
				cfg.LoadCertificate(config.CertificatePath, config.CertificatePassword);

			if (config.ServerOptions != null)
				SetProperties(cfg, config.ServerOptions);

			if (config.SupressDefaultHeaders)
			{
				cfg.AddDefaultResponseHeaders = resp =>
				{
					if (config.DefaultHeaders != null)
						foreach (var e in config.DefaultHeaders)
							if (!resp.Headers.ContainsKey(e.Key))
								resp.AddHeader(e.Key, e.Value);
				};

			}
			else if (config.DefaultHeaders != null && config.DefaultHeaders.Count != 0)
			{
				cfg.AddDefaultResponseHeaders = resp =>
				{
					foreach (var e in config.DefaultHeaders)
						if (!resp.Headers.ContainsKey(e.Key))
							resp.AddHeader(e.Key, e.Value);
				};
			}

			return cfg;
		}

		/// <summary>
		/// Creates a server configuration from a CLI config instance
		/// </summary>
		/// <returns>The server configuration.</returns>
		/// <param name="config">The CLI config instance.</param>
		public static ServerConfig ValidateConfig(CLIServerConfiguration config)
		{
			var cfg = CreateServerConfig(config);

			if (config.Loggers != null)
			{
				foreach (var logger in config.Loggers)
				{
					var inst = CreateInstance(logger.Assembly, logger.Classname, logger.ConstructorArguments, typeof(ILogger));
					if (logger.LoggerOptions != null)
						SetProperties(inst, logger.LoggerOptions);
					cfg.AddLogger((ILogger)inst);
				}
			}

			if (config.Routes != null)
			{
				foreach (var route in config.Routes)
				{
					// Check if this is a module
					if (route.RoutePrefix != null)
					{
						object handler;
						var moduletype = ResolveType(route.Assembly, route.Classname);
						if (typeof(Ceen.Httpd.Handler.FileHandler).IsAssignableFrom(moduletype))
						{
							Func<IHttpRequest, string, string> mimehandler = null;
							if (config.MimeTypes != null && config.MimeTypes.Count > 0)
							{
								string default_mimetype;
								config.MimeTypes.TryGetValue("*", out default_mimetype);
								if (string.IsNullOrWhiteSpace(default_mimetype))
									default_mimetype = null;

								if (config.IgnoreDefaultMimeTypes)
								{
									mimehandler = (req, mappedpath) =>
									{
										var ext = Path.GetExtension(mappedpath).ToLowerInvariant();											
										string mime;
										config.MimeTypes.TryGetValue(ext, out mime);
										if (default_mimetype != null && string.IsNullOrWhiteSpace(mime))
											return default_mimetype;
										
										return mime;
									};
								}
								else
								{
									mimehandler = (req, mappedpath) =>
									{
										var ext = Path.GetExtension(mappedpath).ToLowerInvariant();
										string mime;
										config.MimeTypes.TryGetValue(ext, out mime);
										if (!string.IsNullOrWhiteSpace(mime))
											return mime;
										else if (default_mimetype != null && string.IsNullOrWhiteSpace(mime))
											return default_mimetype;
										else
											return Ceen.Httpd.Handler.FileHandler.DefaultMimeTypes(mappedpath);
									};
								}
							}
							else if (config.IgnoreDefaultMimeTypes)
							{
								mimehandler = (req, path) => null;
							}

							if (config.IndexDocuments.Count == 0)
								handler = new Ceen.Httpd.Handler.FileHandler(
									ExpandEnvironmentVariables(route.ConstructorArguments.First()),
									mimehandler
								);
							else
								handler = new Ceen.Httpd.Handler.FileHandler(
									ExpandEnvironmentVariables(route.ConstructorArguments.First()),
									config.IndexDocuments.ToArray(),
									mimehandler
								);
						}
						else
						{
							handler = CreateInstance(moduletype, route.ConstructorArguments, typeof(IHttpModule));
						}

						if (route.RouteOptions != null)
							SetProperties(handler, route.RouteOptions);

						if (string.IsNullOrWhiteSpace(route.RoutePrefix))
							cfg.AddRoute((IHttpModule)handler);
						else
							cfg.AddRoute(route.RoutePrefix, (IHttpModule)handler);
					}
					else
					{
						Type defaulttype = null;
						if (!string.IsNullOrWhiteSpace(route.Classname))
						{
							defaulttype = Type.GetType($"{route.Classname}, {route.Assembly}", false);
							if (defaulttype == null)
								throw new Exception($"Failed to find class {route.Classname} in {route.Assembly}");
						}

						var rt = new Ceen.Mvc.ControllerRouterConfig(defaulttype);
						if (route.RouteOptions != null)
							SetProperties(rt, route.RouteOptions);

						var assembly = Assembly.Load(route.Assembly);

						cfg.AddRoute(new Ceen.Mvc.ControllerRouter(rt, assembly));
					}
				}
			}

			return cfg;
		}

		public static string ExpandEnvironmentVariables(string input)
		{
			// TODO: Support unix-style environment variables + default values
			return Environment.ExpandEnvironmentVariables(input);
		}
	}
}
