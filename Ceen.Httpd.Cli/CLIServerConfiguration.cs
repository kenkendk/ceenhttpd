using System;
using System.Collections.Generic;

namespace Ceen.Httpd.Cli
{

	/// <summary>
	/// A definition of a CLI defined module
	/// </summary>
	[Serializable]
	public class ModuleDefinition
	{
		/// <summary>
		/// The name of the class to use, can be partially qualified
		/// </summary>
		public string Classname { get; set; }
		/// <summary>
		/// A list of route options to apply
		/// </summary>
		public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
		/// <summary>
		/// Gets or sets the constructor arguments.
		/// </summary>
		public List<string> ConstructorArguments { get; set; } = new List<string>();
	}

	/// <summary>
	/// A definition of a CLI defined route
	/// </summary>
	[Serializable]
	public class RouteDefinition : ModuleDefinition
	{
		/// <summary>
		/// The name of the assembly to load
		/// </summary>
		public string Assembly { get; set; }
		/// <summary>
		/// Using a route prefix on the class
		/// </summary>
		public string RoutePrefix { get; set; }
	}

	/// <summary>
	/// A logger of a logger to attach
	/// </summary>
	[Serializable]
	public class LoggerDefinition : ModuleDefinition
	{
	}

	/// <summary>
	/// Configuration holder for CLI-style server configuration
	/// </summary>
	[Serializable]
	public class CLIServerConfiguration
	{
		/// <summary>
		/// Gets or sets the working directory.
		/// </summary>
		public string Basepath { get; set; }
		/// <summary>
		/// Path where assemblies are loaded from
		/// </summary>
		public string Assemblypath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if assemblies are loaded automatically from
        /// <see cref="Assemblypath"/> and <see cref="Basepath"/>, as well as the Ceen libraries.
        /// </summary>
        public bool AutoLoadAssemblies { get; set; } = true;

		/// <summary>
		/// Gets a value indicating if the config file is monitored for changes,
		/// and reloaded
		/// </summary>
		public bool WatchConfigFile { get; set; } = false;

		/// <summary>
		/// Gets or sets the certificate path.
		/// </summary>
		public string CertificatePath { get; set; }
		/// <summary>
		/// Gets or sets the certificate password.
		/// </summary>
		public string CertificatePassword { get; set; }

		/// <summary>
		/// Gets or sets the http port.
		/// </summary>
		public int HttpPort { get; set; } = 80;
		/// <summary>
		/// Gets or sets the HTTPS port.
		/// </summary>
		public int HttpsPort { get; set; } = 443;

		/// <summary>
		/// The address the http port is bound to
		/// </summary>
		public string HttpAddress { get; set; } = "loopback";

		/// <summary>
		/// The address the http port is bound to
		/// </summary>
		public string HttpsAddress { get; set; }

		/// <summary>
		/// Value indicating if configuration allows the server to listen for http requests
		/// </summary>
		public bool ListenHttp { get { return !string.IsNullOrWhiteSpace(this.HttpAddress); } }

		/// <summary>
		/// Value indicating if configuration allows the server to listen for https requests
		/// </summary>
		public bool ListenHttps { get { return !string.IsNullOrWhiteSpace(this.HttpsAddress) && !string.IsNullOrWhiteSpace(this.CertificatePath); } }		

		/// <summary>
		/// Gets or sets the maximum number of seconds to wait for the old domain to unload
		/// </summary>
		public int MaxUnloadWaitSeconds { get; set; } = 30;

		/// <summary>
        /// Gets or sets a value indicating if isolated AppDomains are used to handle reloads
		/// </summary>
		public bool IsolatedAppDomain { get; set; } = false;

		/// <summary>
        /// Gets or sets a value indicating if isolated processes are used to handle reloads
		/// </summary>
		public bool IsolatedProcesses { get; set; } = false;

		/// <summary>
		/// Gets or sets the maximum life time of a spawned runner.
		/// A zero or negative value will make the runner live until a configuration change causes a reload.
		/// </summary>
		public TimeSpan MaxRunnerLifeSeconds { get; set; } = new TimeSpan(0);

		/// <summary>
		/// Gets or sets the number of seconds between each storage expiration check.
		/// </summary>
		public TimeSpan StorageExpirationCheckIntervalSeconds { get; set; } = TimeSpan.FromMinutes(10);

		/// <summary>
		/// Gets or sets the server options.
		/// </summary>
		public Dictionary<string, string> ServerOptions { get; set; } = new Dictionary<string, string>();

		/// <summary>
		/// Gets the defined routes
		/// </summary>
		public List<RouteDefinition> Routes { get; set; } = new List<RouteDefinition>();

		/// <summary>
		/// Gets the defined routes
		/// </summary>
		public List<ModuleDefinition> Modules { get; set; } = new List<ModuleDefinition>();

		/// <summary>
		/// Gets the list of loggers to attach
		/// </summary>
		public List<LoggerDefinition> Loggers { get; set; } = new List<LoggerDefinition>();

		/// <summary>
		/// Gets or sets a value indicating if the default mime types are ignored
		/// </summary>
		public bool IgnoreDefaultMimeTypes { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating if the default headers are suppressed
		/// </summary>
		public bool SupressDefaultHeaders { get; set; } = false;

		/// <summary>
		/// Gets or sets the MIME types, where key is the extension and value is the mime type.
		/// </summary>
		public Dictionary<string, string> MimeTypes { get; set; } = new Dictionary<string, string>();

		/// <summary>
		/// Gets or sets the default headers, where key is the header name and value is the header value.
		/// </summary>
		public Dictionary<string, string> DefaultHeaders { get; set; } = new Dictionary<string, string>();

		/// <summary>
		/// Gets or sets the allowed index documents.
		/// </summary>
		public List<string> IndexDocuments { get; set; } = new List<string>();
	}
}
