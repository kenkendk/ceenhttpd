﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LeanIPC;

namespace Ceen.Httpd.Cli.Runner.SubProcess
{
    /// <summary>
    /// An indirection helper for restarting the listener process
    /// without restarting the handler framework
    /// </summary>
    internal class IndirectRunner : IWrappedRunner, ISelfListen
    {
        /// <summary>
        /// The instance we are serving
        /// </summary>
        public SpawnRemoteInstance Instance;

        /// <summary>
        /// The path to use
        /// </summary>
        private string m_path;
        /// <summary>
        /// The ssl flag
        /// </summary>
        private bool m_useSSL;
        /// <summary>
        /// The storage creator
        /// </summary>
        private IStorageCreator m_storage;
        /// <summary>
        /// The cancellation token
        /// </summary>
        private CancellationToken m_token;
        /// <summary>
        /// The ready task
        /// </summary>
        private readonly Task m_task;

        /// <summary>
        /// The socket used to listen for new connections
        /// </summary>
        private SockRock.ListenSocket m_listenSocket;


        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Ceen.Httpd.Cli.Runner.SubProcess.SpawnRemoteInstance"/>
        /// uses managed listen.
        /// </summary>
        /// <value><c>true</c> if using managed listen; otherwise, <c>false</c>.</value>
        public bool UseManagedListen => !SystemHelper.IsCurrentOSPosix;

        /// <summary>
        /// Creates a new indirect runner
        /// </summary>
        /// <param name="path">The path to the config file.</param>
        /// <param name="useSSL">If set to <c>true</c> use ssl.</param>
        /// <param name="storage">The storage interface to use.</param>
        /// <param name="token">A cancellation token to use.</param>
        public IndirectRunner(string path, bool useSSL, IStorageCreator storage, CancellationToken token)
        {
            m_path = path;
            m_useSSL = useSSL;
            m_storage = storage;
            m_token = token;

            // Make sure we have an instance before continuing
            Instance = new SpawnRemoteInstance(m_path, m_useSSL, m_storage, m_token);

            m_task = MonitorForRestartAsync();
        }

        /// <summary>
        /// Restarts the remote process if it crashes
        /// </summary>
        /// <returns>An awaitable task</returns>
        private async Task MonitorForRestartAsync()
        {
            while(!m_token.IsCancellationRequested)
            {
                try { await Instance.Stopped; } 
                catch { }

                if (m_token.IsCancellationRequested)
                    return;

                Instance = new SpawnRemoteInstance(m_path, m_useSSL, m_storage, m_token);

            }
        }

        /// <summary>
        /// Handles a request on the wrapped instance
        /// </summary>
        /// <param name="client">The socket to transfer.</param>
        /// <param name="endPoint">The remote endpoint</param>
        /// <param name="logtaskid">The log task ID</param>
        public Task HandleRequest(Socket client, EndPoint remoteEndPoint, string logtaskid)
        {
            return Instance.HandleRequest(client, remoteEndPoint, logtaskid);
        }

        /// <summary>
        /// Kills the remote process
        /// </summary>
        public void Kill()
        {
            Instance.Kill();
        }

        /// <summary>
        /// Stops the remote process
        /// </summary>
        /// <returns>An awaitable task.</returns>
        public Task StopAsync()
        {
            return Instance.StopAsync();
        }

        /// <summary>
        /// Listens for a new connection
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task<KeyValuePair<long, EndPoint>> ListenAsync(CancellationToken cancellationToken)
        {
            return m_listenSocket.AcceptAsync(cancellationToken);
        }

        /// <summary>
        /// Binds this instance to the given endpoint
        /// </summary>
        /// <param name="endPoint">The endpoint to use.</param>
        /// <param name="backlog">The connection backlog.</param>
        public void Bind(EndPoint endPoint, int backlog)
        {
            m_listenSocket?.Dispose();

            m_listenSocket = new SockRock.ListenSocket();
            m_listenSocket.Bind(endPoint, backlog);
        }

        public Task HandleRequest(long client, EndPoint remoteEndPoint, string logtaskid)
        {
            return Instance.HandleRequest(client, remoteEndPoint, logtaskid);
        }
    }

    /// <summary>
    /// Handler for spawning a remote process and interfacing with it
    /// </summary>
    public class Runner : RunHandlerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.SubProcess.ProcessSpawnHandler"/> class.
        /// </summary>
        /// <param name="path">The path to the configuration file.</param>
        public Runner(string path)
            : base(path)
        {
        }

        /// <summary>
        /// Creates a new runner instance
        /// </summary>
        /// <returns>The runner instance.</returns>
        /// <param name="path">The path to the config file.</param>
        /// <param name="useSSL">If set to <c>true</c> use ssl.</param>
        /// <param name="storage">The storage interface to use.</param>
        /// <param name="token">A cancellation token to use.</param>
        protected override IWrappedRunner CreateRunner(string path, bool useSSL, IStorageCreator storage, CancellationToken token)
        {
            return new IndirectRunner(path, useSSL, storage, token);
        }
    }

    /// <summary>
    /// Class to keep the connection with the spawned process
    /// </summary>
    internal class SpawnRemoteInstance : IWrappedRunner
    {
        /// <summary>
        /// The RPC peer
        /// </summary>
        private readonly RPCPeer m_peer;

        /// <summary>
        /// The proxy for the spawned server
        /// </summary>
        private readonly ISpawnedServerProxy m_proxy;

        /// <summary>
        /// The connected socket we use to send IPC/RPC message
        /// </summary>
        private readonly Socket m_ipcSocket;

        /// <summary>
        /// The connected socket we use to pass file descriptors
        /// </summary>
        private readonly Socket m_fdSocket;

        /// <summary>
        /// The serializer used to send data over the file descriptor socket
        /// </summary>
        private readonly TypeSerializer m_fdSocketTypeSerializer;

        /// <summary>
        /// The socket path for this instance
        /// </summary>
        private readonly string m_socketpath;

        /// <summary>
        /// A flag indicating if the instance is using SSL
        /// </summary>
        private readonly bool m_useSSL;
        /// <summary>
        /// The path to the config file
        /// </summary>
        private readonly string m_path;

        /// <summary>
        /// Flag used to determine if the socket path should be hidden (i.e. not a real file)
        /// </summary>
        private readonly bool m_hiddenSocketPath = false;

        /// <summary>
        /// The running process
        /// </summary>
        private readonly System.Diagnostics.Process m_proc;

        /// <summary>
        /// The main runner task
        /// </summary>
        private readonly Task m_main;

        /// <summary>
        /// The startup event
        /// </summary>
        private readonly TaskCompletionSource<bool> m_startupEvent = new TaskCompletionSource<bool>();

        /// <summary>
        /// The lock guarding access to write to the socket
        /// </summary>
        private readonly AsyncLock m_lock = new AsyncLock();

        /// <summary>
        /// The storage backend
        /// </summary>
        private readonly IStorageCreator m_storage;

        /// <summary>
        /// Task used to monitor if this instance has completed
        /// </summary>
        public Task Stopped { get => m_main; }

        /// <summary>
        /// Helper class to delete files after use
        /// </summary>
        private class DeleteFilesHelper : IDisposable
        {
            /// <summary>
            /// The list of files to delete
            /// </summary>
            private readonly string[] m_filenames;

            /// <summary>
            /// Initializes a new instance of the
            /// <see cref="T:Ceen.Httpd.Cli.Runner.SubProcess.SpawnRemoteInstance.DeleteFilesHelper"/> class.
            /// </summary>
            /// <param name="filenames">The files to delete on dispose.</param>
            public DeleteFilesHelper(params string[] filenames)
            {
                m_filenames = filenames ?? new string[0];
            }

            /// <summary>
            /// Deletes all the files
            /// </summary>
            public void Dispose()
            {
                foreach (var f in m_filenames)
                    try { System.IO.File.Delete(f); }
                    catch { }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.SubProcess.SpawnRemoteInstance"/> class.
        /// </summary>
        /// <param name="path">The path to the config file</param>
        /// <param name="usessl">If set to <c>true</c> use ssl.</param>
        public SpawnRemoteInstance(string path, bool usessl, IStorageCreator storage, CancellationToken token)
        {
            m_path = path;
            m_useSSL = usessl;
            m_storage = storage;

            var prefix = m_hiddenSocketPath ? "\0" : string.Empty;
            var sockid = string.Format("ceen-socket-{0}", new Random().Next().ToString("x8"));
            if (m_hiddenSocketPath)
            {
                m_socketpath = sockid;
            }
            else
            {
                var pathPrefix = Environment.GetEnvironmentVariable("CEEN_SOCKET_FOLDER");
                if (string.IsNullOrWhiteSpace(pathPrefix))
                    pathPrefix = System.IO.Path.GetTempPath();

                m_socketpath = System.IO.Path.GetFullPath(System.IO.Path.Combine(pathPrefix, sockid));
            }

            // Setup a socket server, start the child, and stop the server
            using ((SystemHelper.IsCurrentOSPosix && !m_hiddenSocketPath) ? new DeleteFilesHelper(m_socketpath, m_socketpath + "_fd") : null)
            using (var serveripcsocket = SystemHelper.IsCurrentOSPosix ? new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP) : null)
            using (var serverfdsocket = SystemHelper.IsCurrentOSPosix ? new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP) : null)
            {
                if (SystemHelper.IsCurrentOSPosix)
                {
                    serveripcsocket.Bind(new SockRock.UnixEndPoint(prefix + m_socketpath));
                    serveripcsocket.Listen(1);

                    serverfdsocket.Bind(new SockRock.UnixEndPoint(prefix + m_socketpath + "_fd"));
                    serverfdsocket.Listen(1);
                }

                // Launch the child process
                m_proc = StartRemoteProcess();

                // TODO: Consider some multiplexer to allow multiple outputs without mixing the contents
                var tasks = Task.WhenAll(
                    m_proc.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardOutput()),
                    m_proc.StandardError.BaseStream.CopyToAsync(Console.OpenStandardError()),
                    Console.OpenStandardInput().CopyToAsync(m_proc.StandardInput.BaseStream)
                );

                if (SystemHelper.IsCurrentOSPosix)
                {
                    var ct = new CancellationTokenSource();

                    // Prepare cancellation after 5 seconds
                    Task.Delay(5000, ct.Token).ContinueWith(_ =>
                    {
                        serveripcsocket.Dispose();
                        serverfdsocket.Dispose();
                    });

                    // Get the first connection
                    m_ipcSocket = serveripcsocket.Accept();
                    m_fdSocket = serverfdsocket.Accept();

                    // Stop the timer
                    ct.Cancel();
                }

                //and then don't listen anymore
            }


            var ipc = new InterProcessConnection(new NetworkStream(m_ipcSocket, true));
            m_peer = new RPCPeer(
                ipc,
                typeof(IStorageEntry),
                typeof(IStorageCreator)
            );

            // We pass these by reference
            m_peer.TypeSerializer.RegisterSerializationAction(typeof(IStorageEntry), SerializationAction.Reference);
            m_peer.TypeSerializer.RegisterSerializationAction(typeof(IStorageCreator), SerializationAction.Reference);

            // Set up special handling for serializing an endpoint instance
            m_peer.TypeSerializer.RegisterEndPointSerializers();
            m_peer.TypeSerializer.RegisterIPEndPointSerializers();

            m_main = Task.Run(async () => {
                try
                {
                    using(m_fdSocket)
                    using(m_ipcSocket)
                        await ipc.RunMainLoopAsync(false);
                }
                finally
                {
                    Program.DebugConsoleOutput("{0}: Finished main loop, no longer connected to: {1}", System.Diagnostics.Process.GetCurrentProcess().Id, m_proc.Id);
                    m_proc.WaitForExit((int)TimeSpan.FromMinutes(1).TotalMilliseconds);
                    if (!m_proc.HasExited)
                        m_proc.Kill();
                    Program.DebugConsoleOutput("{0}: Target now stopped: {1}", System.Diagnostics.Process.GetCurrentProcess().Id, m_proc.Id);
                }
            });

            if (token.CanBeCanceled)
                token.Register(() => this.Stop());

            // Register the storage item, since it is a different concrete class (i.e. not the interface)
            if (storage != null)
            {
                m_peer.TypeSerializer.RegisterSerializationAction(storage.GetType(), SerializationAction.Reference);
                m_peer.RegisterLocalObjectOnRemote(storage, typeof(IStorageCreator)).Wait();
            }

            m_peer.AddAutomaticProxy(typeof(SpawnedServer), typeof(ISpawnedServerProxy));
            m_proxy = m_peer.CreateAsync<ISpawnedServerProxy>(typeof(SpawnedServer), usessl, path, storage).Result;

            if (SystemHelper.IsCurrentOSPosix)
            {
                // Prepare the file descriptor socket
                m_fdSocketTypeSerializer = new TypeSerializer(false, false);
                SetupDescriptorSocket().Wait();
            }
        }

        /// <summary>
        /// Gets an event that can be awaited to ensure the process has started
        /// </summary>
        /// <value>The awaitable task.</value>
        public Task StartedAsync => m_startupEvent.Task;

        /// <summary>
        /// Starts the remote process
        /// </summary>
        /// <returns>The process that was started.</returns>
        private System.Diagnostics.Process StartRemoteProcess()
        {
            // If we fake spawn, we actually run in the same process,
            // but send handles as if we were two different processes
            // this helps with debugging, as it is possible to set 
            // breakpoints in the spawned code
            var FAKE_SPAWN = false;

            if (FAKE_SPAWN)
            {
                Environment.SetEnvironmentVariable(SpawnedRunner.SOCKET_PATH_VARIABLE_NAME, m_socketpath);
                Environment.SetEnvironmentVariable(SpawnedRunner.SOCKET_PREFIX_VARIABLE_NAME, m_hiddenSocketPath ? "1" : "");
                Task.Run(SpawnedRunner.RunClientRPCListenerAsync);

                var pi = new System.Diagnostics.ProcessStartInfo("sleep", "10000")
                {
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = System.IO.Directory.GetCurrentDirectory()
                };

                pi.EnvironmentVariables[SpawnedRunner.SOCKET_PATH_VARIABLE_NAME] = m_socketpath;
                pi.EnvironmentVariables[SpawnedRunner.SOCKET_PREFIX_VARIABLE_NAME] = m_hiddenSocketPath ? "1" : "";
                return System.Diagnostics.Process.Start(pi);

            }
            else
            {
                var entry = System.IO.Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location);
                var exe = System.IO.Path.Combine(Program.CLIConfiguration.Assemblypath, entry);
                if (!System.IO.File.Exists(exe))
                    exe = System.Reflection.Assembly.GetEntryAssembly().Location;

                var pi = new System.Diagnostics.ProcessStartInfo(exe)
                {
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                // Special handling for post-Mono versions
                if (SystemHelper.ProcessStartRequiresDotnetPrefix)
                {
                    // With non-Mono we need some workarounds, in non-compiled mode
                    if (string.Equals(System.IO.Path.GetExtension(exe), ".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        pi.FileName = "dotnet";
                        pi.Arguments = exe;
                    }
                    else if (SystemHelper.IsCurrentOSPosix && string.Equals(System.IO.Path.GetExtension(exe), ".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        // Even with a .exe file we need to wrap it on non-windows
                        pi.FileName = "dotnet";
                        pi.Arguments = exe;
                    }
                }

                pi.EnvironmentVariables[SpawnedRunner.SOCKET_PATH_VARIABLE_NAME] = m_socketpath;
                pi.EnvironmentVariables[SpawnedRunner.SOCKET_PREFIX_VARIABLE_NAME] = m_hiddenSocketPath ? "1" : "";
                return System.Diagnostics.Process.Start(pi);
            }
        }

        /// <summary>
        /// Sends the initial descriptor to the file descriptor socket
        /// </summary>
        /// <returns>An awaitable task.</returns>
        private async Task SetupDescriptorSocket()
        {
            using (var ms = new System.IO.MemoryStream())
            using (var bcs = new BinaryConverterStream(ms, m_fdSocketTypeSerializer))
            {
                await bcs.WriteObjectAsync(new InitialProtocolDescription()
                {
                    Version = 1,
                    ServerHandle = ((IRemoteInstance)m_proxy).Handle,
                    RequestSignature = m_fdSocketTypeSerializer.GetShortTypeDefinition(typeof(SocketRequest))
                });
                await bcs.FlushAsync();
                m_fdSocket.Send(ms.ToArray());

                Program.DebugConsoleOutput($"Sent protocol data with {ms.Length} bytes to {m_proc.Id}");
            }
        }

        /// <summary>
        /// Handles a request on this instance
        /// </summary>
        /// <param name="client">The socket to transfer.</param>
        /// <param name="endPoint">The remote endpoint</param>
        /// <param name="logtaskid">The log task ID</param>
        public async Task HandleRequest(Socket client, EndPoint endPoint, string logtaskid)
        {
            // Make sure we free the handle from this process space
            // the DuplicateAndClose() call should ensure that we do not
            // actually close the connection, just the socket in this process
            using (client)
            {
                Program.DebugConsoleOutput("Processing new request with .net handler");

                // On Windows we can send directly, as the DuplicateAndClose() call does all the work for us
                if (SystemHelper.CurrentOS == Platform.Windows)
                {
                    var sr = client.DuplicateAndClose(m_proc.Handle.ToInt32());
                    await m_proxy.HandleRequest(sr, endPoint, logtaskid);
                }
                else
                {
                    throw new PlatformNotSupportedException($"Unable to transmit the socket on the current platform: {SystemHelper.CurrentOS}");
                }
            }
        }

        /// <summary>
        /// Terminates the remote instance
        /// </summary>
        public void Stop()
        {
            m_peer.Dispose();
        }

        /// <summary>
        /// Stops the remote process
        /// </summary>
        /// <returns>An awaitable task.</returns>
        public Task StopAsync()
        {
            Stop();
            return m_main;
        }

        /// <summary>
        /// Kills the remote process
        /// </summary>
        public void Kill()
        {
            m_proc.Kill();
        }

        /// <summary>
        /// Kills the remote process
        /// </summary>
        /// <returns>An awaitable task.</returns>
        public Task KillAsync()
        {
            Kill();
            return m_main;
        }

        /// <summary>
        /// Handles a request on this instance
        /// </summary>
        /// <param name="client">The socket to transfer.</param>
        /// <param name="remoteEndPoint">The remote endpoint</param>
        /// <param name="logtaskid">The log task ID</param>
        public async Task HandleRequest(long client, EndPoint remoteEndPoint, string logtaskid)
        {
            Program.DebugConsoleOutput("Processing new request with native handle");

            // On POSIX systems we use SCM_RIGHTS
            if (SystemHelper.IsCurrentOSPosix)
            {
                try
                {
                    // Prepare a stream
                    using (var ms = new System.IO.MemoryStream())
                    using (var bcs = new BinaryConverterStream(ms, m_fdSocketTypeSerializer))
                    {
                        // Serialize the request
                        await bcs.WriteObjectAsync(new SocketRequest()
                        {
                            Handle = client,
                            RemoteIP = ((IPEndPoint)remoteEndPoint).Address.ToString(),
                            RemotePort = ((IPEndPoint)remoteEndPoint).Port,
                            LogTaskID = logtaskid
                        });
                        await bcs.FlushAsync();

                        Program.DebugConsoleOutput($"Sending socket {client} with {ms.Length} bytes of data to {m_proc.Id}");

                        // Send the request data with the handle to the remote process
                        using (await m_lock.LockAsync())
                            SockRock.ScmRightsImplementation.send_fds(m_fdSocket.Handle.ToInt32(), new int[] { (int)client }, ms.ToArray());

                        Program.DebugConsoleOutput($"Data sent to {m_proc.Id}, closing local socket");

                        // Make sure the handle is no longer in this process
                        SockRock.ScmRightsImplementation.native_close((int)client);

                        Program.DebugConsoleOutput($"Completed sending the socket to {m_proc.Id}");
                    }
                }
                catch (Exception ex)
                {
                    Program.ConsoleOutput("Failed to handle request: {0}", ex);
                }
            }
            else
            {
                throw new PlatformNotSupportedException($"Unable to transmit the socket on the current platform: {SystemHelper.CurrentOS}");
            }
        }
    }
}
