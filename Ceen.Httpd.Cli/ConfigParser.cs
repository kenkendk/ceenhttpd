using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

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
        private static readonly Type[] SUPPORTED_RETURN_TYPES = new Type[] { typeof(string), typeof(int), typeof(uint), typeof(bool), typeof(long), typeof(ulong), typeof(TimeSpan) };

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
                .Where(x => SUPPORTED_RETURN_TYPES.Contains(x.PropertyType) || x.PropertyType.IsEnum || (x.PropertyType.IsArray && SUPPORTED_RETURN_TYPES.Contains(x.PropertyType.GetElementType())))
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
			try
			{
				prop.SetValue(instance, ArgumentFromString(value, prop.PropertyType));
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"Failed to set the {prop.PropertyType.FullName} property {prop.Name} in {prop.DeclaringType.FullName} to value \"{value}\"", ex);
			}
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

            if (targettype.IsArray)
            {
                // TODO: Handle embedded comma values in strings?
				var args = 
                    (value ?? "")
                        .Split(',')
                        .Select(x => ArgumentFromString((x ?? string.Empty).Trim(), targettype.GetElementType()))
                        .ToArray();

				// Manually convert to the right type
				var res = Array.CreateInstance(targettype.GetElementType(), args.Length);
				Array.Copy(args, res, args.Length);
				return res;
            }

            if ((targettype.IsArray || targettype == typeof(string)) && string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                return null;

			if (targettype == typeof(TimeSpan))
				return ParseUtil.ParseDuration(ExpandEnvironmentVariables(value));
			if (targettype == typeof(int) || targettype == typeof(uint) || targettype == typeof(long) || targettype == typeof(ulong))
				return Convert.ChangeType(ParseUtil.ParseSize(ExpandEnvironmentVariables(value)), targettype);
            if (targettype == typeof(bool))
                return ParseUtil.ParseBool(ExpandEnvironmentVariables(value));

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
				throw new Exception($"Found class {itemtype.FullName} in assembly {itemtype.Assembly.FullName}, but it does not implement ${targettype.FullName}");

			var cons = itemtype.GetConstructors().Where(x => x.GetParameters().Length == constructorargs.Count).ToArray();
			if (cons.Length == 0)
				throw new Exception($"Failed to load the class named {itemtype.FullName} from assembly {itemtype.Assembly.FullName} as there were no matching constructors");
			if (cons.Length != 1)
				throw new Exception($"Failed to load the class named {itemtype.FullName} from assembly {itemtype.Assembly.FullName} as there were {cons.Length} matching constructors");

			var args = cons.First().GetParameters().Zip(constructorargs, (parameter, value) =>
			{
				try
				{
					return ArgumentFromString(value, parameter.ParameterType);
				}
				catch (Exception ex)
				{
					throw new ArgumentException($"Failed to set the {parameter.ParameterType.FullName} property {parameter.Name} in {parameter.Member.DeclaringType.FullName}.{parameter.Member.Name} to value \"{value}\"", ex);
				}
			}).ToArray();

			try
			{
				return Activator.CreateInstance(itemtype, args);
			}
			catch(Exception ex)
			{
				throw new ArgumentException($"Failed to create an instance of type {itemtype.FullName} with arguments: {string.Join(", ", args.Select(x => x == null ? string.Empty : x.ToString()))}{Environment.NewLine}Error: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Create an instance of an object, using constructor arguments and properties
		/// </summary>
		/// <returns>The created instance.</returns>
		/// <param name="classname">The name of the class to create.</param>
		/// <param name="constructorargs">Arguments to pass to the constructor.</param>
		/// <param name="targettype">The type the instance should be assignable to.</param>
		private static object CreateInstance(string classname, List<string> constructorargs, Type targettype)
		{
			return CreateInstance(ResolveType(classname), constructorargs, targettype);
		}

		/// <summary>
		/// Resolves the type given the class and assembly names.
		/// </summary>
		/// <returns>The resolved type, or <c>null</c>.</returns>
		/// <param name="classname">The name of the class to create.</param>
		private static Type ResolveType(string classname)
		{
			var itemtype =
				Type.GetType(classname, false)
					??
				AppDomain.CurrentDomain.GetAssemblies()
					.Select(x => x.GetType(classname))
					.FirstOrDefault(x => x != null)
					??
				ResolveBuiltInTypes(classname);

			if (itemtype == null)
				throw new Exception($"Failed to find the class named {classname}");
			return itemtype;
		}

		/// <summary>
		/// Helper class to support parsing typenames for built-in types without requiring their full name
		/// </summary>
		/// <param name="classname">The name of the type to find</param>
		/// <returns>The resolved type, or <c>null</c>.</returns>
		private static Type ResolveBuiltInTypes(string classname)
		{
			if (string.IsNullOrWhiteSpace(classname))
				return null;
			classname = classname.Trim().ToLowerInvariant();
			if (classname.StartsWith("system."))
				classname = classname.Substring("system.".Length);

			switch(classname)
			{
				case "int":
				case "integer":
				case "int32":
					return typeof(int);
				case "uint":
				case "uint32":
					return typeof(uint);
				case "long":
				case "int64":
					return typeof(int);
				case "ulong":
				case "uint64":
					return typeof(int);
				case "str":
				case "string":
					return typeof(string);
				case "float":
				case "float32":
				case "single":
					return typeof(float);
				case "float64":
				case "double":
					return typeof(float);
				case "bool":
				case "boolean":
					return typeof(bool);
				case "date":
				case "datetime":
					return typeof(DateTime);
				case "time":
				case "timespan":
					return typeof(TimeSpan);
			}

			return null;				
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
							lastitemprops = route.Options;
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
							lastitemprops = route.Options;
						}
					}
					else if (string.Equals(cmd, "wireroute", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length != 2)
							throw new Exception($"Wrong number of arguments in line {lineindex}: {line}");
						var route = new WiredRouteDefinition()
						{
							Classname = args.Skip(1).First()
						};

						cfg.Routes.Add(route);
						lastitemprops = route.Options;
					}
					else if (string.Equals(cmd, "wirecontroller", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length != 3)
							throw new Exception($"Wrong number of arguments in line {lineindex}: {line}");

						var route = new WiredRouteDefinition()
						{
							RoutePrefix = args.Skip(1).First(),
							Classname = args.Skip(2).First(),
						};

						cfg.Routes.Add(route);

						// We cannot set props on the wired instance
						lastitemprops = null;
					}
					else if (string.Equals(cmd, "wire", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 4)
							throw new Exception($"Wrong number of arguments in line {lineindex}: {line}");

						var route = new WiredRouteDefinition()
						{
							Verbs = args.Skip(1).First(),
							RoutePrefix = args.Skip(2).First(),
							Classname = args.Skip(3).First(),
							ConstructorArguments = args.Skip(4).ToList()
						};

						if (route.Verbs.Length == 0)
							throw new Exception($"Need at least one verb for the route in line {lineindex}: {line}");

						cfg.Routes.Add(route);

						// We cannot set props on the wired instance
						lastitemprops = null;
					}
					else if (string.Equals(cmd, "handler", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 3)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");

						var route = new RouteDefinition()
						{
							RoutePrefix = args.Skip(1).First(),
							Classname = args.Skip(2).First(),
							ConstructorArguments = args.Skip(3).ToList()
						};

						cfg.Routes.Add(route);
						lastitemprops = route.Options;
					}
					else if (string.Equals(cmd, "module", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 2)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");

						var module = new ModuleDefinition()
						{
							Classname = args.Skip(1).First(),
							ConstructorArguments = args.Skip(2).ToList()
						};

						cfg.Modules.Add(module);
						lastitemprops = module.Options;
					}
                    else if (string.Equals(cmd, "postprocessor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2)
                            throw new Exception($"Too few arguments in line {lineindex}: {line}");

                        var postprocessor = new PostProcessorDefinition()
                        {
                            Classname = args.Skip(1).First(),
                            ConstructorArguments = args.Skip(2).ToList()
                        };

                        cfg.PostProcessors.Add(postprocessor);
                        lastitemprops = postprocessor.Options;
                    }

                    else if (string.Equals(cmd, "logger", StringComparison.OrdinalIgnoreCase))
					{
						if (args.Length < 2)
							throw new Exception($"Too few arguments in line {lineindex}: {line}");

						var logger = new LoggerDefinition()
						{
							Classname = args.Skip(1).First(),
							ConstructorArguments = args.Skip(2).ToList()
						};

						cfg.Loggers.Add(logger);
						lastitemprops = logger.Options;
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
						if (routearg == string.Empty)
							routearg = "/";
					
						if (!routearg.StartsWith("/", StringComparison.Ordinal))
							throw new Exception($"The route must start with a forward slash in line {lineindex}: {line}");
						while (routearg.Length > 1 && routearg.EndsWith("/", StringComparison.Ordinal))
							routearg = routearg.Substring(0, routearg.Length - 1);
					
						var pathprefix = routearg;
						
						routearg = routearg == "/" ? "[/.*]" : $"[{routearg}(/.*)?]";

						var route = new RouteDefinition()
						{
							RoutePrefix = routearg,
							Classname = typeof(Ceen.Httpd.Handler.FileHandler).AssemblyQualifiedName,
							ConstructorArguments = args.Skip(2).ToList()
						};

						cfg.Routes.Add(route);
						lastitemprops = route.Options;
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
							Classname = typeof(Ceen.Httpd.Handler.RedirectHandler).AssemblyQualifiedName,
							ConstructorArguments = args.Skip(2).ToList()
						};

						cfg.Routes.Add(route);
						lastitemprops = route.Options;
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
			
			// Set up the module context
			var ctx = LoaderContext.StartContext();
			cfg.LoaderContext = ctx;

            // Expose our configuration to allow modules to call back, 
			// beware that these values should never be reported to the client
			// as there might be proxies in front which will make them useless
			// to clients not running on the local machine
			if (config.ListenHttp)
			{
				var parsedaddr = ParseUtil.ParseEndPoint(config.HttpAddress, config.HttpPort);
				if (parsedaddr is IPEndPoint ipe)
				{
					var ip = ipe.Address == IPAddress.Any ? "127.0.0.1" : ipe.Address.ToString();
            		Environment.SetEnvironmentVariable("CEEN_SELF_HTTP_URL", "http://" + ip + ":" + ipe.Port);
				}
				// else
				// {
            	// 	Environment.SetEnvironmentVariable("CEEN_SELF_HTTP_URL", config.HttpAddress);
				// }
			}
            if (config.ListenHttps)
			{
				var parsedaddr = ParseUtil.ParseEndPoint(config.HttpsAddress, config.HttpsPort);
				if (parsedaddr is IPEndPoint ipe)
				{
					var ip = ipe.Address == IPAddress.Any ? "127.0.0.1" : ipe.Address.ToString();
            		Environment.SetEnvironmentVariable("CEEN_SELF_HTTPS_URL", "http://" + ip + ":" + ipe.Port);
				}
				// else
				// {
            	// 	Environment.SetEnvironmentVariable("CEEN_SELF_HTTPS_URL", config.HttpsAddress);					
				// }
			}

            if (config.AutoLoadAssemblies)
			{
				// Workaround for unittests not providing EntryAssembly
				var callingasm = Assembly.GetEntryAssembly();
				var callingdir = callingasm == null ? null : Path.GetDirectoryName(callingasm.Location);
				                                       
				var paths = (config.Assemblypath ?? string.Empty)
					.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
					.Union(new[] { config.Basepath, callingdir })
					.Where(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x))
					.Distinct();

				foreach (var p in paths)
					foreach (var file in Directory.GetFiles(p, "*.*", SearchOption.TopDirectoryOnly))
						if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
							try { Assembly.LoadFrom(file); }
							catch { }

				var ceenpath = Path.GetDirectoryName(typeof(CLIServerConfiguration).Assembly.Location);
				if ( Directory.Exists(ceenpath))
				{
					foreach (var dll in Directory.GetFiles(ceenpath, "Ceen.*.dll", SearchOption.TopDirectoryOnly))
						try { Assembly.LoadFrom(dll); }
						catch { }
				}
			}

			if (config.Loggers != null)
			{
				foreach (var logger in config.Loggers)
				{
					var inst = CreateInstance(logger.Classname, logger.ConstructorArguments, typeof(ILogger));
					if (logger.Options != null)
						SetProperties(inst, logger.Options);

                    if (inst is IWithSetup mse)
                        mse.AfterConfigure();

                    cfg.AddLogger((ILogger)inst);
				}
			}

			if (config.Modules != null)
			{
				foreach (var module in config.Modules)
				{
					object handler;
					var moduletype = ResolveType(module.Classname);
					handler = CreateInstance(moduletype, module.ConstructorArguments, typeof(IModule));

					if (module.Options != null)
						SetProperties(handler, module.Options);

                    if (handler is IWithSetup mse)
                        mse.AfterConfigure();


                    cfg.AddModule((IModule)handler);
				}
			}

            if (config.PostProcessors != null)
            {
                foreach (var postProcessor in config.PostProcessors)
                {
                    var inst = CreateInstance(postProcessor.Classname, postProcessor.ConstructorArguments, typeof(IPostProcessor));
                    if (postProcessor.Options != null)
                        SetProperties(inst, postProcessor.Options);

                    if (inst is IWithSetup mse)
                        mse.AfterConfigure();

                    cfg.AddPostProcessor((IPostProcessor)inst);
                }
            }

            if (config.Routes != null)
			{
				Ceen.Mvc.ManualRoutingController wirecontroller = null;
				Ceen.Mvc.ControllerRouterConfig wirecontrollerconfig = null;

				foreach (var route in config.Routes)
				{
					// If we have a wired controller, and a non-wire item
					// add it now to ensure the order is maintained
					if (wirecontroller != null && !(route is WiredRouteDefinition))
					{
						cfg.AddRoute(wirecontroller.ToRoute(wirecontrollerconfig));
						wirecontroller = null;
					}

					// Check for wireups
					if (route is WiredRouteDefinition wired)
					{
						// Make a new manual router, if none is present or if we add one
						if (wirecontroller == null || (wired.RoutePrefix == null ))
						{
							// Add any previous wireup to the routes
							if (wirecontroller != null)
								cfg.AddRoute(wirecontroller.ToRoute(wirecontrollerconfig));

							wirecontrollerconfig = new Ceen.Mvc.ControllerRouterConfig();
							if (route.Options != null)
								SetProperties(wirecontrollerconfig, route.Options);
							wirecontroller = new Ceen.Mvc.ManualRoutingController();
						}

						if (wired.Verbs == null)
						{
							var itemtype = ResolveType(wired.Classname);
							wirecontroller.WireController(itemtype, wired.RoutePrefix, wirecontrollerconfig);
						}
						else
						{						
							var ix = wired.Classname.LastIndexOf('.');
							var classname = wired.Classname.Substring(0, ix);
							var methodname = wired.Classname.Substring(ix + 1);

							var itemtype = ResolveType(classname);
							var argumenttypes = wired.ConstructorArguments.Select(x => ResolveType(x)).ToArray();
							if (argumenttypes.Any(x => x == null))
								throw new Exception($"Cannot resolve type argument {argumenttypes.Select((a, b) => a == null ? wired.ConstructorArguments[b] : null).Where(x => x != null).First()}");

							wirecontroller.Wire(itemtype, wired.Verbs + " " + wired.RoutePrefix, methodname, argumenttypes);
						}
					}
					// Check if this is a module
					else if (route.RoutePrefix != null)
					{
						object handler;
						var moduletype = ResolveType(route.Classname);
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

                            handler = Activator.CreateInstance(
                                moduletype,
                                ExpandEnvironmentVariables(route.ConstructorArguments.First()),
                                mimehandler
                            );

                            if (config.IndexDocuments.Count != 0)
                            {
                                if (route.Options.ContainsKey(nameof(Handler.FileHandler.IndexDocuments)))
                                    throw new Exception($"Cannot use both the `Index` option and {nameof(Handler.FileHandler.IndexDocuments)} in the configuration for {moduletype.FullName}");
                                route.Options[nameof(Handler.FileHandler.IndexDocuments)] = string.Join(", ", config.IndexDocuments.Select(x => $"\"{x}\""));
                            }
						}
						else
						{
							handler = CreateInstance(moduletype, route.ConstructorArguments, typeof(IHttpModule));
						}

						if (route.Options != null)
							SetProperties(handler, route.Options);

                        if (handler is IWithSetup mse)
                            mse.AfterConfigure();

						if (string.IsNullOrWhiteSpace(route.RoutePrefix))
							cfg.AddRoute((IHttpModule)handler);
						else
							cfg.AddRoute(route.RoutePrefix, (IHttpModule)handler);
					}
					// Otherwise it is automatic routing of an assembly
					else
					{
						var assembly = Assembly.Load(route.Assembly);
						if (assembly == null)
							throw new Exception($"Failed to load assembly {route.Assembly}");

						Type defaulttype = null;
						if (!string.IsNullOrWhiteSpace(route.Classname))
						{
							defaulttype = assembly.GetType(route.Classname, false);
							if (defaulttype == null)
								throw new Exception($"Failed to find class {route.Classname} in {route.Assembly}");
						}

						var rt = new Ceen.Mvc.ControllerRouterConfig(defaulttype);
						if (route.Options != null)
							SetProperties(rt, route.Options);

                        cfg.AddRoute(new Ceen.Mvc.ControllerRouter(rt, assembly));
					}
				}				
				
				// If we have a wired controller, add it
				if (wirecontroller != null)
					cfg.AddRoute(wirecontroller.ToRoute(wirecontrollerconfig));
			}

			ctx.Freeze();
			return cfg;
		}

		public static string ExpandEnvironmentVariables(string input)
		{
			// TODO: Support unix-style environment variables + default values
			return Environment.ExpandEnvironmentVariables(input);
		}
	}
}
