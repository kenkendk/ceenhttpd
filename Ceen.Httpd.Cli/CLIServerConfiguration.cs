using System;
using System.Collections.Generic;

namespace Ceen.Httpd.Cli
{
	/// <summary>
	/// A definition of a CLI defined route
	/// </summary>
	[Serializable]
	public class RouteDefinition
	{
		/// <summary>
		/// The name of the assembly to load
		/// </summary>
		public string Assembly { get; set; }
		/// <summary>
		/// The name of the class to use in the assembly,
		/// using the entire assembly if this is  empty
		/// </summary>
		/// <value>The assembly.</value>
		public string Classname { get; set; }
		/// <summary>
		/// Using a route prefix on the class
		/// </summary>
		public string RoutePrefix { get; set; }
		/// <summary>
		/// A list of route options to apply
		/// </summary>
		public Dictionary<string, string> RouteOptions { get; set; } = new Dictionary<string, string>();
		/// <summary>
		/// Gets or sets the constructor arguments.
		/// </summary>
		public List<string> ConstructorArguments { get; set; } = new List<string>();
	}

	/// <summary>
	/// A logger of a logger to attach
	/// </summary>
	[Serializable]
	public class LoggerDefinition
	{
		/// <summary>
		/// The name of the assembly to load
		/// </summary>
		public string Assembly { get; set; }
		/// <summary>
		/// The name of the class to use in the assembly,
		/// using the entire assembly if this is  empty
		/// </summary>
		/// <value>The assembly.</value>
		public string Classname { get; set; }
		/// <summary>
		/// Gets or sets the logger options.
		/// </summary>
		public Dictionary<string, string> LoggerOptions { get; set; } = new Dictionary<string, string>();
		/// <summary>
		/// Gets or sets the constructor arguments.
		/// </summary>
		public List<string> ConstructorArguments { get; set; } = new List<string>();
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
		/// Gets or sets the maximum number of seconds to wait for the old domain to unload
		/// </summary>
		public int MaxUnloadWaitSeconds { get; set; } = 30;

		/// <summary>
		/// Gets or sets a value indicating if app domains are created to handle reloads
		/// </summary>
		public bool IsolatedAppDomain { get; set; } = true;

		/// <summary>
		/// Gets or sets the server options.
		/// </summary>
		public Dictionary<string, string> ServerOptions { get; set; } = new Dictionary<string, string>();

		/// <summary>
		/// Gets the defined routes
		/// </summary>
		public List<RouteDefinition> Routes { get; set; } = new List<RouteDefinition>();

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
