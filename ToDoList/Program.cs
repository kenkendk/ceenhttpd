using System;
using System.IO;
using System.Linq;
using Ceen.Httpd.Cli;

namespace ToDoList
{
	class MainClass
	{
		//private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public static int Main(string[] args)
		{
			log4net.Config.XmlConfigurator.Configure();
			Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));

			// Check if the webroot is already defined
			if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBROOT")))
			{
				// Use the webroot in the target folder, if any
				if (Directory.Exists("webroot"))
					Environment.SetEnvironmentVariable("WEBROOT", Path.GetFullPath("webroot"));
#if DEBUG
				// In debug mode, assume we are in ./bin/Debug, 
				// so move back and find webroot in the project folder
				else
					Environment.SetEnvironmentVariable("WEBROOT", Path.GetFullPath("../../webroot"));
#endif
			}

			// Set a default listen port
			if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LISTEN_PORT")))
				Environment.SetEnvironmentVariable("LISTEN_PORT", "8080");

			if (args == null || args.Length == 0)
				args = new string[] { "config.txt" };

			// Try to parse the config file so we can report the settings
			var config = ConfigParser.ParseTextFile(args[0]);
			Console.WriteLine("Listening to port {0}, serving {1}", config.HttpPort, config.Routes.Where(x => x.Assembly == "Ceen.Httpd" && x.Classname == "Ceen.Httpd.Handler.FileHandler").Select(x => ConfigParser.ExpandEnvironmentVariables(x.ConstructorArguments.FirstOrDefault())).FirstOrDefault());

			return Ceen.Httpd.Cli.MainClass.Main(args);
		}
	}
}
