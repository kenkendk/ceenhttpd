using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Ceen.Httpd.Cli.Spawn;
using LeanIPC;

namespace Ceen.Httpd.Cli.SubProcess
{
    /// <summary>
    /// Handler for spawning a remote process and interfacing with it
    /// </summary>
    public class ProcessSpawnHandler : Spawn.SpawnHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.SubProcess.ProcessSpawnHandler"/> class.
        /// </summary>
        /// <param name="path">The path to the configuration file.</param>
        public ProcessSpawnHandler(string path)
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
            return new SpawnRemoteInstance(path, useSSL, storage, token);
        }
    }

    /// <summary>
    /// Class to keep the connection with the spawned process
    /// </summary>
    internal class SpawnRemoteInstance : Spawn.IWrappedRunner
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
        private readonly string m_socketpath = string.Format("ceen-socket-{0}", new Random().Next().ToString("x8"));

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
        /// Gets the runner task that can be used to check if the process is running
        /// </summary>
        public Task RunnerTask => m_main;

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Ceen.Httpd.Cli.SubProcess.SpawnRemoteInstance"/> should stop.
        /// </summary>
        /// <value><c>true</c> if should stop; otherwise, <c>false</c>.</value>
        public bool ShouldStop { get; private set; } = false;

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

            // Setup a socket server, start the child, and stop the server
            using (var serveripcsocket = SystemHelper.IsCurrentOSPosix ? new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP) : null)
            using (var serverfdsocket = SystemHelper.IsCurrentOSPosix ? new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP) : null)
            {
                if (SystemHelper.IsCurrentOSPosix)
                {
                    serveripcsocket.Bind(new UnixEndPoint(prefix + m_socketpath));
                    serveripcsocket.Listen(1);

                    serverfdsocket.Bind(new UnixEndPoint(prefix + m_socketpath + "_fd"));
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
                    //TODO: need to be able to kill this on timeout
                    // Get the first connection,
                    m_ipcSocket = serveripcsocket.Accept();
                    m_fdSocket = serverfdsocket.Accept();
                }

                //and then don't listen anymore
            }


            var ipc = new InterProcessConnection(new NetworkStream(m_ipcSocket));
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

            m_main = Task.Run(() => ipc.RunMainLoopAsync(false));

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
        /// <returns>The async.</returns>
        private System.Diagnostics.Process StartRemoteProcess()
        {
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

                var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var pi = new System.Diagnostics.ProcessStartInfo(exe)
                {
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

#if NETCOREAPP
                // With .Net Core we need some workarounds, in non-compiled mode
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
#endif

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

                Console.WriteLine($"Sent protocol data with {ms.Length} bytes");
            }
        }

        /// <summary>
        /// Handles a request on this instance
        /// </summary>
        /// <param name="client">The socket to transfer.</param>
        /// <param name="endPoint">The remote endpoint</param>
        /// <param name="logtaskid">The log task ID</param>
        public async Task HandleRequest(TcpClient client, EndPoint endPoint, string logtaskid)
        {
            // Make sure we free the handle from this process space
            // the DuplicateAndClose() call should ensure that we do not
            // actually close the connection, just the socket in this process
            using (client)
            {
                Console.WriteLine("Processing new request");

                // On POSIX systems we use SCM_RIGHTS
                if (SystemHelper.IsCurrentOSPosix)
                {
                    try
                    {
                        // Extract the OS handle
                        var handle = client.Client.Handle.ToInt64();

#if NETCOREAPP
                        SocketInformation sr = default(SocketInformation);
#else

                        // Create a serialized version of the socket
                        var sr = client.Client.DuplicateAndClose(m_proc.Handle.ToInt32());

                        // Check that the serialized for is implemented as expected
                        if (BitConverter.ToInt64(sr.ProtocolInformation, sr.ProtocolInformation.Length - sizeof(long)) != handle)
                            throw new Exception("Expected the serialized socket information to contain the handle as the last 8 bytes");
#endif

                        // Prepare a stream
                        using (var ms = new System.IO.MemoryStream())
                        using (var bcs = new BinaryConverterStream(ms, m_fdSocketTypeSerializer))
                        {
                            // Serialize the request
                            await bcs.WriteObjectAsync(new SocketRequest()
                            {
                                Handle = handle,
                                SocketOptions = sr.Options,
                                SocketData = sr.ProtocolInformation,
                                RemoteIP = ((IPEndPoint)endPoint).Address.ToString(),
                                RemotePort = ((IPEndPoint)endPoint).Port,
                                LogTaskID = logtaskid
                            });
                            await bcs.FlushAsync();

                            Console.WriteLine($"Sending socket {handle} with {ms.Length} bytes of data");

                            // Send the request data with the handle to the remote process
                            using (await m_lock.LockAsync())
                                ScmRightsImplementation.send_fds(m_fdSocket.Handle.ToInt32(), new int[] { (int)handle }, ms.ToArray());

                            //Console.WriteLine("Data sent, closing local socket");
                            // Make sure the handle is no longer in this process
                            //ScmRightsImplementation.native_close((int)handle);

#if NETCOREAPP
                            // We cannot use DuplicateAndClose() so we hack it in here
                            client.Client.Close();
#endif

                            Console.WriteLine("Completed sending the socket");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to handle request: {0}", ex);
                    }

                }
                // On Windows we can send directly, as the DuplicateAndClose() call does all the work for us
                else if (SystemHelper.CurrentOS == Platform.Windows)
                {
                    var sr = client.Client.DuplicateAndClose(m_proc.Handle.ToInt32());
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
            ShouldStop = true;
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
            ShouldStop = true;
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
    }
}
