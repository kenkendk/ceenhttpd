using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.IO;
using System.Net.Security;
using Ceen.Common;

namespace Ceen.Httpd
{
	public class HttpServer
	{
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
			/// The stop token
			/// </summary>
			public readonly CancellationToken StopToken;
			/// <summary>
			/// A value indicating if the server is stopped
			/// </summary>
			public volatile bool m_isStopped = false;
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
			/// Initializes a new instance of the <see cref="Ceenhttpd.HttpServer+RunnerControl"/> class.
			/// </summary>
			/// <param name="stoptoken">The stoptoken.</param>
			/// <param name="maxactive">The maximum number of active handlers.</param>
			public RunnerControl(CancellationToken stoptoken, int maxactive, DebugLogDelegate debuglogger) 
			{
				StopToken = stoptoken;
				StopToken.Register(() => m_stoptask.TrySetCanceled());
				m_maxactive = maxactive;
				m_throttletask.SetResult(true);
				m_debuglogger = debuglogger;
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
		/// Gets the server config.
		/// </summary>
		/// <value>The config.</value>
		public ServerConfig Config { get; private set; }

		/// <summary>
		/// Gets the total number of active clients
		/// </summary>
		/// <value>The total active clients.</value>
		public static int TotalActiveClients { get { return RunnerControl.TotalActiveClients; } }

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.HttpServer"/> class.
		/// </summary>
		/// <param name="config">The server config.</param>
		public HttpServer(ServerConfig config)
		{
			Config = config;
		}

		/// <summary>
		/// Listens to incoming connections and calls the spawner method for each new connection
		/// </summary>
		/// <returns>Awaitable task.</returns>
		/// <param name="addr">The address to listen to.</param>
		/// <param name="stoptoken">The stoptoken.</param>
		/// <param name="spawner">The method handling the new connection.</param>
		private async Task ListenInternalAsync(IPEndPoint addr, CancellationToken stoptoken, Action<TcpClient, string, RunnerControl> spawner)
		{
			var listener = new TcpListener(addr);
			listener.Start(Config.SocketBacklog);

			var rc = new RunnerControl(stoptoken, Config.MaxActiveRequests, Config.DebugLogHandler);
			var taskid = Guid.NewGuid().ToString("N");

			while (!stoptoken.IsCancellationRequested)
			{
				// Wait if there are too many active
				if (Config.DebugLogHandler != null) Config.DebugLogHandler("Waiting for throttle", taskid, null);
				await rc.ThrottleTask;
				if (Config.DebugLogHandler != null) Config.DebugLogHandler("Waiting for socket", taskid, null);
				var ls = listener.AcceptTcpClientAsync();

				if (await Task.WhenAny(rc.StopTask, ls) == ls)
				{
					if (Config.DebugLogHandler != null) Config.DebugLogHandler("Re-waiting for socket", taskid, null);
					var client = await ls;
					var newtaskid = Guid.NewGuid().ToString("N");

					int wt, cpt;
					ThreadPool.GetAvailableThreads(out wt, out cpt);
					if (Config.DebugLogHandler != null) Config.DebugLogHandler(string.Format("Threadpool says {0}, {1}", wt, cpt), taskid, newtaskid);

					if (Config.DebugLogHandler != null) Config.DebugLogHandler(string.Format("Spawning runner with id: {0}", newtaskid), taskid, newtaskid);
					ThreadPool.QueueUserWorkItem(x => spawner(client, newtaskid, rc));
				}
			}

			if (Config.DebugLogHandler != null) Config.DebugLogHandler("Stopping", taskid, null);

			listener.Stop();
			rc.Stop(taskid);

			if (Config.DebugLogHandler != null) Config.DebugLogHandler("Socket stopped, waiting for workers ...", taskid, null);
			await rc.FinishedTask;

			if (Config.DebugLogHandler != null) Config.DebugLogHandler("Stopped", taskid, null);
		}

		/// <summary>
		/// Listens to a port, using the given endpoint. 
		/// Note that this method does not use SSL.
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="addr">The address to listen to.</param>
		/// <param name="stoptoken">The stoptoken.</param>
		public Task ListenAsync(IPEndPoint addr, CancellationToken stoptoken = default(CancellationToken))
		{
			return ListenInternalAsync(addr, stoptoken, RunClient);
		}

		/// <summary>
		/// Listens to a port, using the given endpoint
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="addr">The address to listen to.</param>
		/// <param name="stoptoken">The stoptoken.</param>
		public Task ListenSSLAsync(IPEndPoint addr, CancellationToken stoptoken = default(CancellationToken))
		{
			if (Config.SSLCertificate as X509Certificate2 == null || !(Config.SSLCertificate as X509Certificate2).HasPrivateKey)
				throw new Exception("Certificate does not have a private key and cannot be used for signing");

			return ListenInternalAsync(addr, stoptoken, (ls, taskid, rc) => {
				if (Config.DebugLogHandler != null) Config.DebugLogHandler("Running SSL", taskid, ls);
				RunSslClient(ls, taskid, rc);
			});
		}

		/// <summary>
		/// Handler method for SSL connections
		/// </summary>
		/// <param name="client">The new connection.</param>
		/// <param name="logtaskid">The task id for logging and tracing</param>
		/// <param name="controller">The runner controller.</param>
		private async void RunSslClient(TcpClient client, string logtaskid, RunnerControl controller)
		{
			using (var s = client.GetStream())
			using (var ssl = new SslStream(s, false))
			{
				try
				{
					// Slightly higher value here to avoid races with the other timeout mechanisms
					s.ReadTimeout = s.WriteTimeout = (Config.RequestIdleTimeoutSeconds + 1) * 1000;

					if (Config.DebugLogHandler != null) Config.DebugLogHandler("Authenticate SSL", logtaskid, client);
					await ssl.AuthenticateAsServerAsync(Config.SSLCertificate, Config.SSLRequireClientCert, Config.SSLEnabledProtocols, Config.SSLCheckCertificateRevocation);
				}
				catch (Exception aex)
				{
					if (Config.Logger != null)
						try { await Config.Logger.LogRequest(new HttpContext(new HttpRequest(client.Client.RemoteEndPoint, logtaskid, null), null), aex, DateTime.Now, new TimeSpan()); }
						catch { }

					return;
				}

				if (Config.DebugLogHandler != null) Config.DebugLogHandler("Run SSL", logtaskid, client);
				await Runner(ssl, client.Client.RemoteEndPoint, logtaskid, ssl.RemoteCertificate, controller);
			}

			if (Config.DebugLogHandler != null) Config.DebugLogHandler("Done running", logtaskid, client);
		}

		/// <summary>
		/// Handler method for Non-SSL connections
		/// </summary>
		/// <param name="client">The new connection.</param>
		/// <param name="logtaskid">The task id for logging and tracing</param>
		/// <param name="controller">The runner controller.</param>
		private async void RunClient(TcpClient client, string logtaskid, RunnerControl controller)
		{
			using (var s = client.GetStream())
			{
				// Slightly higher value here to avoid races with the other timeout mechanisms
				s.ReadTimeout = s.WriteTimeout = (Config.RequestIdleTimeoutSeconds + 1) * 1000;
				await Runner(s, client.Client.RemoteEndPoint, logtaskid, null, controller);
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
		private async Task Runner(Stream stream, EndPoint endpoint, string logtaskid, X509Certificate clientcert, RunnerControl controller)
		{
			var requests = Config.KeepAliveMaxRequests;

			bool keepingalive = false;

			HttpContext context = null;
			HttpRequest cur = null;
			HttpResponse resp = null;
			DateTime started = new DateTime();

			try
			{
				if (Config.DebugLogHandler != null) Config.DebugLogHandler("Running task", logtaskid, endpoint);
				if (!controller.RegisterActive(logtaskid))
					return;

				using (var bs = new BufferedStreamReader(stream))
				{
					do
					{
						bs.ResetReadLength(Config.MaxPostSize);
						started = DateTime.Now;
						context = new HttpContext(
							cur = new HttpRequest(endpoint, logtaskid, clientcert),
							resp = new HttpResponse(stream, Config)
						);

						var timeouttask = Task.Delay(TimeSpan.FromSeconds(keepingalive ? Config.KeepAliveTimeoutSeconds : Config.RequestIdleTimeoutSeconds));
						var idletime = TimeSpan.FromSeconds(Config.RequestHeaderReadTimeoutSeconds);

						if (Config.DebugLogHandler != null) Config.DebugLogHandler("Parsing headers", logtaskid, endpoint);
						try
						{
							await cur.Parse(bs, Config, idletime, timeouttask, controller.StopTask);
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
								resp.AddHeader("Keep-Alive", string.Format("timeout={0}, max={1}", Config.KeepAliveTimeoutSeconds, Config.KeepAliveMaxRequests));
						}
						else
							resp.KeepAlive = false;

						if (Config.Logger as IStartLogger != null)
							await ((IStartLogger)Config.Logger).LogRequestStarted(cur);

						if (Config.DebugLogHandler != null) Config.DebugLogHandler("Running handler", logtaskid, cur);

						try
						{
							// TODO: Set a timer on the processing as well?

							// Process the request
							if (!await Config.Router.Process(context))
								throw new HttpException(Ceen.Common.HttpStatusCode.NotFound);
						}
						catch (HttpException hex)
						{
							// Try to set the status code to 500
							if (resp.HasSentHeaders)
								throw;

							resp.StatusCode = hex.StatusCode;
							resp.StatusMessage = hex.StatusMessage;
						}

						if (Config.DebugLogHandler != null) Config.DebugLogHandler("Flushing response", logtaskid, cur);

						// If the handler has not flushed, we do it
						await resp.FlushAndSetLengthAsync();

						// Check if keep-alive is possible
						keepingalive = resp.KeepAlive && resp.HasWrittenCorrectLength;
						requests--;

						if (Config.Logger != null)
							await Config.Logger.LogRequest(context, null, started, DateTime.Now - started);
					
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
						resp.StatusCode = Ceen.Common.HttpStatusCode.InternalServerError;
						resp.StatusMessage = HttpStatusMessages.DefaultMessage(Ceen.Common.HttpStatusCode.InternalServerError);
					}

					try { await resp.FlushAsErrorAsync(); }
					catch { }
				}

				try { stream.Close(); }
				catch {}

				try
				{
					if (Config.Logger != null)
						await Config.Logger.LogRequest(context, ex, started, DateTime.Now - started);
				}
				catch
				{
				}

				if (Config.DebugLogHandler != null) Config.DebugLogHandler("Failed handler", logtaskid, cur);

			}
			finally
			{
				controller.RegisterStopped(logtaskid);
				if (Config.DebugLogHandler != null) Config.DebugLogHandler("Terminating handler", logtaskid, cur);
			}
		}
	}
}

