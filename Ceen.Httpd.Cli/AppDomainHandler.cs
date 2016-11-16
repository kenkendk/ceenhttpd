using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Linq;
using System.Net;

namespace Ceen.Httpd.Cli
{
	/// <summary>
	/// Handler class for running a server split across appdomains
	/// </summary>
	internal class AppDomainHandler : IDisposable
	{
		/// <summary>
		/// Bridge to call methods across an App Domain
		/// </summary>
		private class AppDomainBridge : Ceen.Httpd.HttpServer.AppDomainBridge
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Httpd.HttpServer.AppDomainBridge"/> class.
			/// </summary>
			public AppDomainBridge() : base() { }

			/// <summary>
			/// Setup this instance
			/// </summary>
			/// <param name="usessl">If set to <c>true</c> usessl.</param>
			/// <param name="path">Path to the configuration file.</param>
			/// <param name="storage">The storage instance or null.</param>
			public void SetupFromFile(bool usessl, string path, IStorageCreator storage)
			{
				var config = ConfigParser.ValidateConfig(ConfigParser.ParseTextFile(path));
				if (storage != null)
				{
					// Inject a wrapper to support async via callbacks
					if (AppDomain.CurrentDomain.IsDefaultAppDomain())
						config.Storage = storage;
					else
						config.Storage = new StorageCreatorAccessor(storage);
				}
				else
					config.Storage = new MemoryStorageCreator();
				
				base.Setup(usessl, config);
			}
		}

		/// <summary>
		/// Wrapper helper to invoke methods across the AppDomain boundary
		/// </summary>
		private class AppDomainWrapper
		{
			/// <summary>
			/// The remote instance of the AppDomainBridge
			/// </summary>
			private readonly object m_wrapped;
			/// <summary>
			/// The Setup method
			/// </summary>
			private readonly System.Reflection.MethodInfo m_setupFromFile;
			/// <summary>
			/// The HandleRequest method
			/// </summary>
			private readonly System.Reflection.MethodInfo m_handleRequest;
			/// <summary>
			/// The Stop method
			/// </summary>
			private readonly System.Reflection.MethodInfo m_stop;
			/// <summary>
			/// The WaitForStop method
			/// </summary>
			private readonly System.Reflection.MethodInfo m_waitforstop;
			/// <summary>
			/// The ActiveClients property
			/// </summary>
			private readonly System.Reflection.PropertyInfo m_activeclients;

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.AppDomainHandler.AppDomainWrapper"/> class.
			/// </summary>
			/// <param name="domain">The domain to wrap the instance in.</param>
			public AppDomainWrapper(AppDomain domain)
			{
				m_wrapped = domain.CreateInstanceAndUnwrap(typeof(AppDomainHandler.AppDomainBridge).Assembly.FullName, typeof(AppDomainHandler.AppDomainBridge).FullName);
				m_setupFromFile = m_wrapped.GetType().GetMethod(nameof(AppDomainHandler.AppDomainBridge.SetupFromFile));
				m_handleRequest = m_wrapped.GetType().GetMethod(nameof(AppDomainHandler.AppDomainBridge.HandleRequest));
				m_stop = m_wrapped.GetType().GetMethod(nameof(AppDomainHandler.AppDomainBridge.Stop));
				m_waitforstop = m_wrapped.GetType().GetMethod(nameof(AppDomainHandler.AppDomainBridge.WaitForStop));
				m_activeclients = m_wrapped.GetType().GetProperty(nameof(AppDomainHandler.AppDomainBridge.ActiveClients));

				if (new[] { m_wrapped, m_setupFromFile, m_handleRequest, m_stop, m_waitforstop }.Any(x => x == null) || m_activeclients == null)
					throw new Exception($"Something changed in {typeof(AppDomainBridge)}");
			}

			/// <summary>
			/// Setup this instance
			/// </summary>
			/// <param name="usessl">If set to <c>true</c> usessl.</param>
			/// <param name="configfile">Path to the configuration file</param>
			/// <param name="storage">The storage instance or null</param>
			public void SetupFromFile(bool usessl, string configfile, IStorageCreator storage)
			{
				m_setupFromFile.Invoke(m_wrapped, new object[] { usessl, configfile, storage });
			}

			/// <summary>
			/// Handles a request
			/// </summary>
			/// <param name="socket">The socket handle.</param>
			/// <param name="remoteEndPoint">The remote endpoint.</param>
			/// <param name="logtaskid">The task ID to use.</param>
			public void HandleRequest(SocketInformation socket, EndPoint remoteEndPoint, string logtaskid)
			{
				m_handleRequest.Invoke(m_wrapped, new object[] { socket, remoteEndPoint, logtaskid });
			}

			/// <summary>
			/// Requests that this instance stops serving requests
			/// </summary>
			public void Stop()
			{
				m_stop.Invoke(m_wrapped, null);
			}

			/// <summary>
			/// Waits for all clients to finish processing
			/// </summary>
			/// <returns><c>true</c>, if for stop succeeded, <c>false</c> otherwise.</returns>
			/// <param name="waitdelay">The maximum time to wait for the clients to stop.</param>
			public bool WaitForStop(TimeSpan waitdelay)
			{
				return (bool)m_waitforstop.Invoke(m_wrapped, new object[] { waitdelay });
			}

			/// <summary>
			/// Gets the number of active clients.
			/// </summary>
			public int ActiveClients { get { return (int)m_activeclients.GetValue(m_wrapped, null); } }
		}

		/// <summary>
		/// Class for keeping the state of a listener
		/// </summary>
		private class RunnerInstance
		{
			/// <summary>
			/// The cancellation token used to signal the listener to stop
			/// </summary>
			private CancellationTokenSource m_token = new CancellationTokenSource();

			/// <summary>
			/// Gets the port this instance listens on
			/// </summary>
			public int Port { get; private set; }
			/// <summary>
			/// Gets the configuration used for this instance
			/// </summary>
			public ServerConfig Config { get; private set; }

			/// <summary>
			/// Gets the IP address this instance listens on
			/// </summary>
			public string Address { get; private set; }

			/// <summary>
			/// The wrapper that is used to handle requests
			/// </summary>
			public AppDomainWrapper Wrapper { get; set; }

			/// <summary>
			/// The awaitable task for the current runner
			/// </summary>
			public Task RunnerTask { get; private set; }

			/// <summary>
			/// Gets a value indicating whether this <see cref="T:Ceen.Httpd.Cli.AppDomainHandler.RunnerInstance"/> should stop.
			/// </summary>
			public bool ShouldStop { get; private set; }

			/// <summary>
			/// Gets a value indicating whether this <see cref="T:Ceen.Httpd.Cli.AppDomainHandler.RunnerInstance"/> uses ssl.
			/// </summary>
			/// <value><c>true</c> if using ssl; otherwise, <c>false</c>.</value>
			public bool UseSSL { get; private set; }

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.AppDomainHandler.RunnerInstance"/> class.
			/// </summary>
			/// <param name="wrapper">The wrapper used to process requests.</param>
			/// <param name="address">The address to listen on</param>
			/// <param name="port">The port to listen on.</param>
			/// <param name="usessl">If set to <c>true</c> use ssl.</param>
			/// <param name="config">The server configuration.</param>
			public RunnerInstance(AppDomainWrapper wrapper, string address, int port, bool usessl, ServerConfig config)
			{
				RestartAsync(wrapper, address, port, usessl, config);
			}

			/// <summary>
			/// Stops the listener
			/// </summary>
			/// <returns>The awaitable task.</returns>
			public Task StopAsync()
			{
				ShouldStop = true;
				m_token.Cancel();
				return RunnerTask;
			}

			/// <summary>
			/// Restarts this instance
			/// </summary>
			/// <param name="address">The address to listen on</param>
			/// <param name="port">The port to listen on.</param>
			/// <param name="usessl">If set to <c>true</c> use ssl.</param>
			/// <param name="config">The server configuration.</param>
			public async Task RestartAsync(AppDomainWrapper wrapper, string address, int port, bool usessl, ServerConfig config)
			{
				if (RunnerTask != null)
					await StopAsync();

				m_token = new CancellationTokenSource();
				Port = port;
				Config = config;
				UseSSL = usessl;
				Wrapper = wrapper;
				Address = address;
				ShouldStop = false;

				var pid = System.Diagnostics.Process.GetCurrentProcess().Id;

				RunnerTask = HttpServer.ListenToSocketAsync(
					new IPEndPoint(ConfigParser.ParseIPAddress(address), port),
					usessl,
					m_token.Token,
					config,
					(socket, addr, id) => Wrapper.HandleRequest(socket.Client.DuplicateAndClose(pid), addr, id)
				);
			}
		}

		/// <summary>
		/// The current active AppDomain
		/// </summary>
		private AppDomain m_appDomain;

		/// <summary>
		/// Map of active handlers
		/// </summary>
		private List<RunnerInstance> m_handlers = new List<RunnerInstance>();
		/// <summary>
		/// Path to the configuration file
		/// </summary>
		private readonly string m_path;
		/// <summary>
		/// The task signalling stopped
		/// </summary>
		private readonly TaskCompletionSource<bool> m_stopped = new TaskCompletionSource<bool>();
		/// <summary>
		/// Gets a task that signals completion
		/// </summary>
		public Task StoppedAsync { get { return m_stopped.Task; } }

		/// <summary>
		/// An event that is raised if the listener crashes
		/// </summary>
		public event Action<string, bool, Exception> InstanceCrashed;

		/// <summary>
		/// The storage creator
		/// </summary>
		private readonly IStorageCreator m_storage = new MemoryStorageCreator();

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.AppDomainHandler"/> class.
		/// </summary>
		/// <param name="configfile">The path to the configuration file.</param>
		public AppDomainHandler(string configfile)
		{
			m_path = configfile;
		}

		/// <summary>
		/// Reload this instance.
		/// </summary>
		public async Task ReloadAsync(bool http, bool https)
		{
			var cfg = ConfigParser.ParseTextFile(m_path);
			var config = ConfigParser.CreateServerConfig(cfg);
			config.Storage = m_storage;

			var domain = AppDomain.CreateDomain(
				"CeenRunner-" + Guid.NewGuid().ToString(),
				null,
				cfg.Basepath,
				cfg.Assemblypath,
				true
			);

			// For debugging, use the same domain
			//domain = AppDomain.CurrentDomain;

			var prevdomains = m_handlers.Select(x => x.Wrapper).ToList();

			try
			{
				await Task.WhenAll(new[] {
					http ? StartRunnerAsync(domain, config, false, cfg.HttpAddress, cfg.HttpPort) : null,
					https ? StartRunnerAsync(domain, config, true, cfg.HttpsAddress, cfg.HttpsPort) : null					
				}.Where(x => x != null));
			}
			catch
			{
				try { AppDomain.Unload(domain); }
				catch { }

				throw;
			}

			var prevdomain = m_appDomain;

			m_appDomain = domain;

			await Task.Run(async () =>
			{
				// Give old domain time to terminate
				var maxtries = cfg.MaxUnloadWaitSeconds;
				while (maxtries-- > 0)
				{
					if (prevdomains.Select(x => x.ActiveClients).Sum() == 0)
						break;
					await Task.Delay(1000);
				}

				if (prevdomain != null && prevdomain != AppDomain.CurrentDomain)
					try { AppDomain.Unload(prevdomain); }
					catch { }
			});
		}

		/// <summary>
		/// Starts a runner instance
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="domain">The AppDomain to use.</param>
		/// <param name="config">The server configuration.</param>
		/// <param name="usessl">If set to <c>true</c> use ssl.</param>
		/// <param name="port">The port to listen on.</param>
		private async Task StartRunnerAsync(AppDomain domain, ServerConfig config, bool usessl, string address, int port)
		{
			var enabled = !string.IsNullOrWhiteSpace(address);
			// Ensure it parses
			ConfigParser.ParseIPAddress(address);


			var prev = m_handlers.Where(x => x.UseSSL == usessl).FirstOrDefault();
			if (enabled)
			{
				var addcrashhandler = true;
				var wrapper = new AppDomainWrapper(domain);
				wrapper.SetupFromFile(usessl, m_path, config.Storage);
				if (prev == null)
				{
					prev = new RunnerInstance(wrapper, address, port, usessl, config);
					m_handlers.Add(prev);
				}
				else
				{
					if (prev.RunnerTask.IsFaulted || prev.RunnerTask.IsCanceled || prev.RunnerTask.IsCompleted)
					{
						var cur = new RunnerInstance(wrapper, address, port, usessl, config);
						m_handlers.Remove(prev);
						m_handlers.Add(cur);

						prev = cur;
					}
					else if (prev.Port != port || !string.Equals(prev.Address, address, StringComparison.Ordinal))
					{
						// Address or port change, start new listener first
						var cur = new RunnerInstance(wrapper, address, port, usessl, config);

						if (!prev.RunnerTask.IsFaulted)
							await prev.StopAsync();

						m_handlers.Remove(prev);
						m_handlers.Add(cur);

						prev = cur;
					}
					else if (prev.Config.SocketBacklog != config.SocketBacklog)
					{
						await prev.RestartAsync(wrapper, address, port, usessl, config);
					}
					else
					{
						addcrashhandler = false; // We already have it
						prev.Wrapper = wrapper; // Assign the new wrapper
					}
				}

				if (addcrashhandler)
				{
					var dummy = prev.RunnerTask.ContinueWith(x =>
					{
						if (!prev.ShouldStop && InstanceCrashed != null)
							InstanceCrashed(address, usessl, x.IsFaulted ? x.Exception : new Exception("Unexpected stop"));
					});
				}
			}
			else if (prev != null)
			{
				await prev.StopAsync();
				m_handlers.Remove(prev);
			}
		}

		/// <summary>
		/// Stops all instances
		/// </summary>
		public async Task StopAsync()
		{
			await Task.WhenAll(m_handlers.Select(x => x.StopAsync()));
			m_handlers.Clear();
			m_stopped.TrySetResult(true);
		}

		/// <summary>
		/// Releases all resource used by the <see cref="T:Ceen.Httpd.Cli.AppDomainHandler"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="T:Ceen.Httpd.Cli.AppDomainHandler"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="T:Ceen.Httpd.Cli.AppDomainHandler"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="T:Ceen.Httpd.Cli.AppDomainHandler"/> so the garbage collector can reclaim the memory that the
		/// <see cref="T:Ceen.Httpd.Cli.AppDomainHandler"/> was occupying.</remarks>
		public void Dispose()
		{
			StopAsync().Wait();
		}
	}
}
