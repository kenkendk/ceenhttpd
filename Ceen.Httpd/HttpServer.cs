using System;
using System.Net;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.IO;
using System.Net.Security;

namespace Ceen.Httpd
{
	/// <summary>
	/// The Http server implementation
	/// </summary>
	public static class HttpServer
	{
		/// <summary>
		/// Handler class that encapsulates a configured server setup,
		/// in a way that is callable from another AppDomain
		/// </summary>
		public class AppDomainBridge : MarshalByRefObject
		{
			/// <summary>
			/// The controller instance
			/// </summary>
			private RunnerControl Controller;
			/// <summary>
			/// The stop token source
			/// </summary>
			private CancellationTokenSource StopToken;

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Httpd.HttpServer.AppDomainBridge"/> class.
			/// </summary>
			public AppDomainBridge() { }

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Ceen.Httpd.HttpServer.AppDomainBridge"/> class.
			/// </summary>
			/// <param name="usessl">If set to <c>true</c> use ssl.</param>
			/// <param name="config">The server config.</param>
			public AppDomainBridge(bool usessl, ServerConfig config) { Setup(usessl, config); }

			/// <summary>
			/// Setup this instance
			/// </summary>
			/// <param name="usessl">If set to <c>true</c> usessl.</param>
			/// <param name="config">Config.</param>
			public void Setup(bool usessl, ServerConfig config)
			{
				if (StopToken != null)
					throw new Exception("Cannot call setup more than once");
				if (config == null)
					throw new ArgumentNullException(nameof(config));
				
				StopToken = new CancellationTokenSource();
				Controller = new RunnerControl(StopToken.Token, usessl, config);
			}

			/// <summary>
			/// Handles a request
			/// </summary>
			/// <param name="socket">The socket handle.</param>
			/// <param name="logtaskid">The task ID to use.</param>
			public void HandleRequest(SocketInformation socket, string logtaskid)
			{				
				RunClient(socket, logtaskid, Controller);
			}

			/// <summary>
			/// Requests that this instance stops serving requests
			/// </summary>
			public void Stop()
			{
				StopToken.Cancel();
			}

			/// <summary>
			/// Waits for all clients to finish processing
			/// </summary>
			/// <returns><c>true</c>, if for stop succeeded, <c>false</c> otherwise.</returns>
			/// <param name="waitdelay">The maximum time to wait for the clients to stop.</param>
			public bool WaitForStop(TimeSpan waitdelay)
			{
				return Controller.FinishedTask.Wait(waitdelay);
			}

			/// <summary>
			/// Gets the number of active clients.
			/// </summary>
			public int ActiveClients { get { return Controller.ActiveClients; } }
		}

		/// <summary>
		/// Helper class to keep track of all active requests and potentially abort them
		/// </summary>
		private class RunnerControl
		{			
			/// <summary>
			/// Backing field for the total number of active clients
			/// </summary>
			private static int m_totalActiveClients;
			/// <summary>
			/// Gets the total number of active clients.
			/// </summary>
			/// <value>The total active clients.</value>
			public static int TotalActiveClients { get { return m_totalActiveClients; } }
			/// <summary>
			/// Backing field for the number of active clients
			/// </summary>
			private int m_activeClients;
			/// <summary>
			/// The number of active clients
			/// </summary>
			public int ActiveClients { get { return m_activeClients; } }
			/// <summary>
			/// The task that signals stopping the server
			/// </summary>
			/// <value>The stop task.</value>
			public Task StopTask { get { return m_stoptask.Task; } }
			/// <summary>
			/// The task signalling that all clients have completed
			/// </summary>
			/// <value>The finished task.</value>
			public Task FinishedTask { get { return m_finishedtask.Task; } }
			/// <summary>
			/// Gets a task that throttles start of new handlers
			/// </summary>
			/// <value>The throttle task.</value>
			public Task ThrottleTask { get { return m_throttletask.Task; } }
			/// <summary>
			/// Gets the server configuration
			/// </summary>
			/// <value>The config.</value>
			public ServerConfig Config { get; private set; }

			/// <summary>
			/// The stop token
			/// </summary>
			public readonly CancellationToken StopToken;
			/// <summary>
			/// A flag indicating if SSL is used
			/// </summary>
			public readonly bool m_useSSL;
			/// <summary>
			/// A value indicating if the server is stopped
			/// </summary>
			public volatile bool m_isStopped = false;
			/// <summary>
			/// Gets a value indicating whether this <see cref="T:Ceen.Httpd.HttpServer.RunnerControl"/> is stopped.
			/// </summary>
			public bool IsStopped { get { return m_isStopped; } }
			/// <summary>
			/// The maximum number of active handlers
			/// </summary>
			private readonly int m_maxactive;
			/// <summary>
			/// The lock object
			/// </summary>
			private readonly object m_lock = new object();

			/// <summary>
			/// The task used to signal all requests are stopped
			/// </summary>
			private readonly TaskCompletionSource<bool> m_finishedtask = new TaskCompletionSource<bool>();
			/// <summary>
			/// The task used to signal all handlers to stop
			/// </summary>
			private readonly TaskCompletionSource<bool> m_stoptask = new TaskCompletionSource<bool>();
			/// <summary>
			/// The task used to signal waiting for handlers to complete before starting new handlers
			/// </summary>
			private TaskCompletionSource<bool> m_throttletask = new TaskCompletionSource<bool>();

			/// <summary>
			/// A logger for reporting the internal log state
			/// </summary>
			private DebugLogDelegate m_debuglogger;

			/// <summary>
			/// Initializes a new instance of the <see cref="Ceen.Httpd+RunnerControl"/> class.
			/// </summary>
			/// <param name="stoptoken">The stoptoken.</param>
			/// <param name="usessl">A flag indicating if this runner is using SSL</param>
			/// <param name="config">The server config.</param>
			public RunnerControl(CancellationToken stoptoken, bool usessl, ServerConfig config) 
			{
				if (config == null)
					throw new ArgumentNullException(nameof(config));
				
				StopToken = stoptoken;
				StopToken.Register(() => m_stoptask.TrySetCanceled());
				Config = config;
				m_maxactive = config.MaxActiveRequests;
				m_throttletask.SetResult(true);
				m_debuglogger = config.DebugLogHandler;
				m_useSSL = usessl;
			}

			/// <summary>
			/// Called by a handler to signal it is in the active state
			/// </summary>
			/// <param name="logtaskid">The task id used for logging and tracing</param>
			/// <returns><c>true</c>, if active was registered, <c>false</c> otherwise.</returns>
			public bool RegisterActive(string logtaskid)
			{
				if (m_debuglogger != null) m_debuglogger("RegisterActive", logtaskid, null);
				
				if (m_isStopped)
					return false;
				
				var res = Interlocked.Increment(ref m_activeClients);
				Interlocked.Increment(ref m_totalActiveClients);

				if (m_debuglogger != null) m_debuglogger(string.Format("RegisterActive: {0}", res), logtaskid, null);

				// If we have too many active, block the throttle task
				if (res >= m_maxactive)
				{
					if (m_debuglogger != null) m_debuglogger("Blocking throttle", logtaskid, null);
					lock (m_lock)
						if (m_throttletask.Task.IsCompleted)
							m_throttletask = new TaskCompletionSource<bool>();
				}

				return true;
			}

			/// <summary>
			/// Called by a handler to signal it has completed handling a request
			/// <param name="logtaskid">The task id used for logging and tracing</param>
			/// </summary>
			public void RegisterStopped(string logtaskid)
			{
				var res = Interlocked.Decrement(ref m_activeClients);
				Interlocked.Decrement(ref m_totalActiveClients);

				if (m_debuglogger != null) m_debuglogger(string.Format("RegisterStopped: {0}", res), logtaskid, null);

				// If the throttle task is blocked and we have few active, unblock it
				if (res < m_maxactive && !m_throttletask.Task.IsCompleted)
				{
					if (m_debuglogger != null) m_debuglogger("Un-blocking throttle", logtaskid, null);
					lock (m_lock)
						if (!m_throttletask.Task.IsCompleted)
							m_throttletask.SetResult(true);
				}


				if (m_isStopped && res == 0)
				{
					if (m_debuglogger != null) m_debuglogger("Stopped and setting finish task", logtaskid, null);
					m_finishedtask.TrySetResult(true);
				}
			}

			/// <summary>
			/// Called to stop handling requests
			/// </summary>
			public void Stop(string logtaskid)
			{
				m_stoptask.TrySetCanceled();
				m_isStopped = true;

				lock (m_lock)
				{
					if (m_activeClients == 0)
					{
						if (m_debuglogger != null) m_debuglogger("Stopping, no active workers", logtaskid, null);
						m_finishedtask.SetResult(true);
					}
					else
					{
						if (m_debuglogger != null) m_debuglogger(string.Format("Stopping with {0} active workers", m_activeClients), logtaskid, null);
					}
				}
			}
		}

		/// <summary>
		/// Gets the total number of active clients
		/// </summary>
		/// <value>The total active clients.</value>
		public static int TotalActiveClients { get { return RunnerControl.TotalActiveClients; } }

		/// <summary>
		/// The method used to set the current socket handlerID in log4net, if available.
		/// This redirection method is used to avoid depending on log4net.
		/// </summary>
		private static readonly Func<string> SetLoggingSocketHandlerID;

		/// <summary>
		/// The method used to set the current taskID in log4net, if available.
		/// This redirection method is used to avoid depending on log4net.
		/// </summary>
		private static readonly Func<string> SetLoggingTaskHandlerID;

		/// <summary>
		/// The method used to set the current requestID in log4net, if available.
		/// This redirection method is used to avoid depending on log4net.
		/// </summary>
		private static readonly Func<string> SetLoggingRequestID;

		/// <summary>
		/// The name of the log4net property that has the socket handler ID
		/// </summary>
		public static readonly string Log4Net_SocketHandlerID = "ceen-socket-handler-id";
		/// <summary>
		/// The name of the log4net property that has the task handler ID
		/// </summary>
		public static readonly string Log4Net_TaskHandlerID = "ceen-task-handler-id";
		/// <summary>
		/// The name of the log4net property that has the request ID
		/// </summary>
		public static readonly string Log4Net_RequestID = "ceen-request-id";

		/// <summary>
		/// Static initialization for the HttpServer class,
		/// used to check for log4net dynamically
		/// </summary>
		static HttpServer()
		{
			Func<string> socketId = () => Guid.NewGuid().ToString("N");
			Func<string> taskId = () => Guid.NewGuid().ToString("N");
			Func<string> requestId = () => Guid.NewGuid().ToString("N");

			// Slowly probe through to get the method
			var t = Type.GetType("log4net.LogicalThreadContext, log4net, Culture=neutral");

			var index_socket = new object[] { Log4Net_SocketHandlerID };
			var index_task = new object[] { Log4Net_TaskHandlerID };
			var index_request = new object[] { Log4Net_RequestID };

			if (t != null)
			{
				var m = t.GetProperty("Properties");
				if (m != null)
				{
					var ins = m.GetValue(null, null);
					if (ins != null)
					{
						var rm = ins.GetType().GetProperties().Where(x => x.GetIndexParameters().Length > 0).FirstOrDefault();
						if (rm != null)
						{
							// We have a default indexer, set up the helper methods
							socketId = () =>
							{
								var g = Guid.NewGuid().ToString("N");
								rm.SetValue(ins, g, index_socket);
								return g;
							};

							taskId = () =>
							{
								var g = Guid.NewGuid().ToString("N");
								rm.SetValue(ins, g, index_task);
								return g;
							};

							requestId = () =>
							{
								var g = Guid.NewGuid().ToString("N");
								rm.SetValue(ins, g, index_request);
								return g;
							};

						}
					}

				}
			}

			// Assign whatever value we had
			SetLoggingSocketHandlerID = socketId;
			SetLoggingTaskHandlerID = taskId;
			SetLoggingRequestID = requestId;
		}

		/// <summary>
		/// Creates and initializes a new AppDomain bridge
		/// </summary>
		/// <returns>The app domain bridge.</returns>
		/// <param name="usessl">If set to <c>true</c> use ssl.</param>
		/// <param name="config">The server config.</param>
		public static AppDomainBridge CreateAppDomainBridge(bool usessl, ServerConfig config)
		{
			return new AppDomainBridge(usessl, config);
		}

		/// <summary>
		/// Listens to incoming connections and calls the spawner method for each new connection
		/// </summary>
		/// <returns>Awaitable task.</returns>
		/// <param name="addr">The address to listen to.</param>
		/// <param name="usessl">A flag indicating if the socket listens for SSL requests</param>
		/// <param name="stoptoken">The stoptoken.</param>
		/// <param name="config">The server configuration</param>
		/// <param name="spawner">The method handling the new connection.</param>
		public static Task ListenToSocketAsync(IPEndPoint addr, bool usessl, CancellationToken stoptoken, ServerConfig config, Action<TcpClient, string> spawner)
		{
			return ListenToSocketInternalAsync(addr, usessl, stoptoken, config, (TcpClient arg1, string arg2, RunnerControl arg3) => spawner(arg1, arg2));
		}

		/// <summary>
		/// Listens to incoming connections and calls the spawner method for each new connection
		/// </summary>
		/// <returns>Awaitable task.</returns>
		/// <param name="addr">The address to listen to.</param>
		/// <param name="usessl">A flag indicating if the socket listens for SSL requests</param>
		/// <param name="stoptoken">The stoptoken.</param>
		/// <param name="config">The server configuration</param>
		/// <param name="spawner">The method handling the new connection.</param>
		private static async Task ListenToSocketInternalAsync(IPEndPoint addr, bool usessl, CancellationToken stoptoken, ServerConfig config, Action<TcpClient, string, RunnerControl> spawner)
		{
			var rc = new RunnerControl(stoptoken, usessl, config);

			var listener = new TcpListener(addr);
			listener.Start(config.SocketBacklog);

			var taskid = SetLoggingSocketHandlerID();

			while (!stoptoken.IsCancellationRequested)
			{
				// Wait if there are too many active
				if (config.DebugLogHandler != null) config.DebugLogHandler("Waiting for throttle", taskid, null);
				await rc.ThrottleTask;
				if (config.DebugLogHandler != null) config.DebugLogHandler("Waiting for socket", taskid, null);
				var ls = listener.AcceptTcpClientAsync();

				if (await Task.WhenAny(rc.StopTask, ls) == ls)
				{
					if (config.DebugLogHandler != null) config.DebugLogHandler("Re-waiting for socket", taskid, null);
					var client = await ls;
					var newtaskid = SetLoggingTaskHandlerID();

					int wt, cpt;
					ThreadPool.GetAvailableThreads(out wt, out cpt);
					if (config.DebugLogHandler != null) config.DebugLogHandler(string.Format("Threadpool says {0}, {1}", wt, cpt), taskid, newtaskid);

					if (config.DebugLogHandler != null) config.DebugLogHandler(string.Format("Spawning runner with id: {0}", newtaskid), taskid, newtaskid);
					ThreadPool.QueueUserWorkItem(x => spawner(client, newtaskid, rc));
				}
			}

			if (config.DebugLogHandler != null) config.DebugLogHandler("Stopping", taskid, null);

			listener.Stop();
			rc.Stop(taskid);

			if (config.DebugLogHandler != null) config.DebugLogHandler("Socket stopped, waiting for workers ...", taskid, null);
			await rc.FinishedTask;

			if (config.DebugLogHandler != null) config.DebugLogHandler("Stopped", taskid, null);
		}

		/// <summary>
		/// Logs a message to all configured loggers
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The request context.</param>
		/// <param name="ex">Exception data, if any.</param>
		/// <param name="start">The request start time.</param>
		/// <param name="duration">The request duration.</param>
		private static Task LogMessageAsync(RunnerControl controller, HttpContext context, Exception ex, DateTime start, TimeSpan duration)
		{
			var config = controller.Config;
			if (config.Loggers == null)
				return Task.FromResult(true);

			var count = config.Loggers.Count;
			if (count == 0)
				return Task.FromResult(true);
			else if (count == 1)
				return config.Loggers[0].LogRequest(context, ex, start, duration);
			else
				return Task.WhenAll(config.Loggers.Select(x => x.LogRequest(context, ex, start, duration)));
		}

		/// <summary>
		/// Listens to a port, using the given endpoint. 
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="addr">The address to listen to.</param>
		/// <param name="usessl">A flag indicating if this instance should use SSL</param>
		/// <param name="config">The server configuration</param>
		/// <param name="stoptoken">The stoptoken.</param>
		public static Task ListenAsync(IPEndPoint addr, bool usessl, ServerConfig config, CancellationToken stoptoken = default(CancellationToken))
		{
			if (usessl && (config.SSLCertificate as X509Certificate2 == null || !(config.SSLCertificate as X509Certificate2).HasPrivateKey))
				throw new Exception("Certificate does not have a private key and cannot be used for signing");
			
			return ListenToSocketInternalAsync(addr, usessl, stoptoken, config, RunClient);
		}

		/// <summary>
		/// Runs a client, using a socket handle from DuplicatiAndClose
		/// </summary>
		/// <param name="socketinfo">The socket handle.</param>
		/// <param name="logtaskid">The log task ID.</param>
		/// <param name="controller">The controller instance</param>
		private static void RunClient(SocketInformation socketinfo, string logtaskid, RunnerControl controller)
		{
			var client = new TcpClient() { Client = new Socket(socketinfo) };
			RunClient(client, logtaskid, controller);
		}

		/// <summary>
		/// Handler method for connections
		/// </summary>
		/// <param name="client">The new connection.</param>
		/// <param name="logtaskid">The task id for logging and tracing</param>
		/// <param name="controller">The runner controller.</param>
		private static async void RunClient(TcpClient client, string logtaskid, RunnerControl controller)
		{
			var config = controller.Config;

			using (var s = client.GetStream())
			using (var ssl = controller.m_useSSL ? new SslStream(s, false) : null)
			{
				if (config.DebugLogHandler != null) config.DebugLogHandler(string.Format("Running {0}", controller.m_useSSL ? "SSL" : "plain"), logtaskid, client);

				// Slightly higher value here to avoid races with the other timeout mechanisms
				s.ReadTimeout = s.WriteTimeout = (controller.Config.RequestIdleTimeoutSeconds + 1) * 1000;

				X509Certificate clientcert = null;

				// For SSL only: negotiate the connection
				if (ssl != null)
				{
					if (config.DebugLogHandler != null) config.DebugLogHandler("Authenticate SSL", logtaskid, client);

					try
					{
						await ssl.AuthenticateAsServerAsync(config.SSLCertificate, config.SSLRequireClientCert, config.SSLEnabledProtocols, config.SSLCheckCertificateRevocation);
					}
					catch (Exception aex)
					{
						if (config.DebugLogHandler != null) config.DebugLogHandler("Failed setting up SSL", logtaskid, client);

						// Log a message indicating that we failed setting up SSL
						await LogMessageAsync(controller, new HttpContext(new HttpRequest(client.Client.RemoteEndPoint, logtaskid, logtaskid, null), null), aex, DateTime.Now, new TimeSpan());

						return;
					}

					if (config.DebugLogHandler != null) config.DebugLogHandler("Run SSL", logtaskid, client);
					clientcert = ssl.RemoteCertificate;
				}

				await Runner(ssl == null ? (Stream)s : ssl, client.Client.RemoteEndPoint, logtaskid, clientcert, controller);

				if (config.DebugLogHandler != null) config.DebugLogHandler("Done running", logtaskid, client);
			}
		}

		/// <summary>
		/// Dispatcher method for handling a request
		/// </summary>
		/// <param name="stream">The underlying stream.</param>
		/// <param name="endpoint">The remote endpoint.</param>
		/// <param name="logtaskid">The task id for logging and tracing</param>
		/// <param name="clientcert">The client certificate if any.</param>
		/// <param name="controller">The runner controller.</param>
		private static async Task Runner(Stream stream, EndPoint endpoint, string logtaskid, X509Certificate clientcert, RunnerControl controller)
		{
			var config = controller.Config;
			var requests = config.KeepAliveMaxRequests;

			bool keepingalive = false;

			HttpContext context = null;
			HttpRequest cur = null;
			HttpResponse resp = null;
			DateTime started = new DateTime();

			try
			{
				if (config.DebugLogHandler != null) config.DebugLogHandler("Running task", logtaskid, endpoint);
				if (!controller.RegisterActive(logtaskid))
					return;

				using (var bs = new BufferedStreamReader(stream))
				{
					do
					{
						var reqid = SetLoggingRequestID();
						bs.ResetReadLength(config.MaxPostSize);
						started = DateTime.Now;
						context = new HttpContext(
							cur = new HttpRequest(endpoint, logtaskid, reqid, clientcert),
							resp = new HttpResponse(stream, config)
						);

						var timeouttask = Task.Delay(TimeSpan.FromSeconds(keepingalive ? config.KeepAliveTimeoutSeconds : config.RequestIdleTimeoutSeconds));
						var idletime = TimeSpan.FromSeconds(config.RequestHeaderReadTimeoutSeconds);

						if (config.DebugLogHandler != null) config.DebugLogHandler("Parsing headers", logtaskid, endpoint);
						try
						{
							await cur.Parse(bs, config, idletime, timeouttask, controller.StopTask);
						}
						catch (EmptyStreamClosedException)
						{
							// Client has closed the connection
							break;
						}
						catch (HttpException hex)
						{
							// Errors during header parsing are unlikely to
							// keep the connection in a consistent state
							resp.StatusCode = hex.StatusCode;
							resp.StatusMessage = hex.StatusMessage;
							await resp.FlushHeadersAsync();

							throw;
						}
							
						string keepalive;
						cur.Headers.TryGetValue("Connection", out keepalive);
						if (("keep-alive".Equals(keepalive, StringComparison.OrdinalIgnoreCase) || keepingalive) && requests > 1)
						{
							resp.KeepAlive = true;
							if (!keepingalive)
								resp.AddHeader("Keep-Alive", string.Format("timeout={0}, max={1}", config.KeepAliveTimeoutSeconds, config.KeepAliveMaxRequests));
						}
						else
							resp.KeepAlive = false;


						if (config.Loggers != null)
						{
							var count = config.Loggers.Count;
							if (count == 1)
							{
								var sl = config.Loggers[0] as IStartLogger;
								if (sl != null)
									await sl.LogRequestStarted(cur);
							}
							else if (count != 0)
								await Task.WhenAll(config.Loggers.Where(x => x is IStartLogger).Cast<IStartLogger>().Select(x => x.LogRequestStarted(cur)));								
						}

						if (config.DebugLogHandler != null) config.DebugLogHandler("Running handler", logtaskid, cur);

						try
						{
							// TODO: Set a timer on the processing as well?
							// TODO: Use a cancellation token?
							// TODO: Abort processing if the client closes?

							// Process the request
							if (!await config.Router.Process(context))
								throw new HttpException(Ceen.HttpStatusCode.NotFound);
						}
						catch (HttpException hex)
						{
							// Try to set the status code to 500
							if (resp.HasSentHeaders)
								throw;

							resp.StatusCode = hex.StatusCode;
							resp.StatusMessage = hex.StatusMessage;
						}

						if (config.DebugLogHandler != null) config.DebugLogHandler("Flushing response", logtaskid, cur);

						// If the handler has not flushed, we do it
						await resp.FlushAndSetLengthAsync();

						// Check if keep-alive is possible
						keepingalive = resp.KeepAlive && resp.HasWrittenCorrectLength;
						requests--;

						await LogMessageAsync(controller, context, null, started, DateTime.Now - started);
					
					} while(keepingalive);
				}
			}
			catch (Exception ex)
			{
				// If possible, report a 500 error to the client
				if (resp != null)
				{
					if (!resp.HasSentHeaders)
					{
						resp.StatusCode = Ceen.HttpStatusCode.InternalServerError;
						resp.StatusMessage = HttpStatusMessages.DefaultMessage(Ceen.HttpStatusCode.InternalServerError);
					}

					try { await resp.FlushAsErrorAsync(); }
					catch { }
				}

				try { stream.Close(); }
				catch {}

				try { await LogMessageAsync(controller, context, ex, started, DateTime.Now - started); }
				catch { }

				if (config.DebugLogHandler != null) config.DebugLogHandler("Failed handler", logtaskid, cur);

			}
			finally
			{
				controller.RegisterStopped(logtaskid);
				if (config.DebugLogHandler != null) config.DebugLogHandler("Terminating handler", logtaskid, cur);
			}
		}
	}
}

