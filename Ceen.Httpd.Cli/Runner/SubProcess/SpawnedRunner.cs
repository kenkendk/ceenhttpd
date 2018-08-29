using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LeanIPC;

namespace Ceen.Httpd.Cli.Runner.SubProcess
{
    /// <summary>
    /// The inital data send over the file descriptor socket
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct InitialProtocolDescription
    {
        /// <summary>
        /// The version number for the protocol
        /// </summary>
        public byte Version;
        /// <summary>
        /// The handle for the server instance
        /// </summary>
        public long ServerHandle;
        /// <summary>
        /// The type signature for the socket requests
        /// </summary>
        public string RequestSignature;
    }

    /// <summary>
    /// A serializable request used to pass socket information
    /// across domain boundaries
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SocketRequest
    {
        /// <summary>
        /// The handle in the source process
        /// </summary>
        public long Handle;
        /// <summary>
        /// The remote client IP
        /// </summary>
        public string RemoteIP;
        /// <summary>
        /// The remote client port
        /// </summary>
        public int RemotePort;
        /// <summary>
        /// The request log tag
        /// </summary>
        public string LogTaskID;
    }

    /// <summary>
    /// Helper interface for the proxy, to allow easy disposal of the remote instance
    /// </summary>
    public interface ISpawnedServerProxy : ISpawnedServer, IDisposable
    {
    }

    /// <summary>
    /// Proxy interface for a spawned server
    /// </summary>
    public interface ISpawnedServer
    {
        /// <summary>
        /// Handles a request
        /// </summary>
        /// <param name="sockinfo">The socket to use.</param>
        /// <param name="remoteEndPoint">The remote endpoint.</param>
        /// <param name="logtaskid">The task ID to use.</param>
        Task HandleRequest(SocketInformation sockinfo, EndPoint remoteEndPoint, string logtaskid);

        /// <summary>
        /// Handles a request
        /// </summary>
        /// <param name="handle">The socket handle to use.</param>
        /// <param name="ip">The remote endpoint IP.</param>
        /// <param name="port">The remote endpoint port</param>
        /// <param name="logtaskid">The task ID to use.</param>
        Task HandleRequestSimple(int handle, EndPoint remoteEndPoint, string logtaskid);

        /// <summary>
        /// Requests that this instance stops serving requests
        /// </summary>
        void Stop();

        /// <summary>
        /// Returns an awaitable task that can be used to wait for termination
        /// </summary>
        Task StopTask { get; }

        /// <summary>
        /// Gets the number of active clients.
        /// </summary>
        int ActiveClients { get; }
    }

    /// <summary>
    /// Implementation of a spawned server
    /// </summary>
    public class SpawnedServer : Ceen.Httpd.HttpServer.InterProcessBridge, ISpawnedServer
    {
        /// <summary>
        /// The method used to create a socket from the handle
        /// </summary>
        //private readonly Func<long, Socket> m_createSocket;

        /// <summary>
        /// The epoll handler instance
        /// </summary>
        private readonly SockRock.ISocketHandler m_pollhandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.UnixSpawn.SpawnedServer"/> class.
        /// </summary>
        /// <param name="usessl">If set to <c>true</c> usessl.</param>
        /// <param name="configfile">Configfile.</param>
        /// <param name="storage">Storage.</param>
        public SpawnedServer(bool usessl, string configfile, IStorageCreator storage)
        {
            //// Mono version
            //var safeSocketHandleType = Type.GetType("System.Net.Sockets.SafeSocketHandle");
            //var safeSocketHandleConstructor = safeSocketHandleType?.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new Type[] { typeof(IntPtr), typeof(bool) }, null);
            //var socketConstructorSafeSocketHandle = safeSocketHandleType == null ? null : typeof(System.Net.Sockets.Socket).GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new Type[] { typeof(AddressFamily), typeof(SocketType), typeof(ProtocolType), safeSocketHandleType }, null);

            //// .Net Core version
            //var safeCloseSocketType = Type.GetType("System.Net.Sockets.SafeCloseSocket");
            //var safeCloseSocketConstructor = safeCloseSocketType?.GetMethod("CreateSocket", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new Type[] { typeof(IntPtr) }, null);
            //var socketConstructorSafeCloseSocket = safeCloseSocketType == null ? null : typeof(System.Net.Sockets.Socket).GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new Type[] { safeCloseSocketType }, null);

            //if (safeSocketHandleConstructor != null && socketConstructorSafeSocketHandle != null)
            //    m_createSocket = (handle) => (Socket)Activator.CreateInstance(typeof(Socket), AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, Activator.CreateInstance(safeSocketHandleType, new IntPtr(handle), false));
            //else if (safeCloseSocketConstructor != null && socketConstructorSafeCloseSocket != null)
            //    m_createSocket = (handle) => (Socket)Activator.CreateInstance(typeof(Socket), safeCloseSocketConstructor.Invoke(null, new object[] { new IntPtr(handle) }));
            //else
                //throw new Exception("Unable to find a method to create sockets from handles ....");

            var config = ConfigParser.ValidateConfig(ConfigParser.ParseTextFile(configfile));
            config.Storage = storage ?? new MemoryStorageCreator();

            try
            {
                m_pollhandler = new SockRock.KqueueHandler();
            }
            catch (Exception e1)
            {
                try
                {
                    m_pollhandler = new SockRock.EpollHandler();
                }
                catch (Exception e2)
                {
                    throw new PlatformNotSupportedException("Unable to create kqueue or epoll instance", new AggregateException(e1, e2));
                }
            }

            base.Setup(usessl, config);
        }

        /// <summary>
        /// Handles a remote request.
        /// </summary>
        /// <param name="sockinfo">The socket with the request to handle.</param>
        /// <param name="remoteEndPoint">The remote peer end point.</param>
        /// <param name="logtaskid">The id used for logging.</param>
        public Task HandleRequest(SocketInformation sockinfo, EndPoint remoteEndPoint, string logtaskid)
        {
            base.HandleRequest(new Socket(sockinfo), remoteEndPoint, logtaskid);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Handles a request
        /// </summary>
        /// <param name="handle">The socket handle to use.</param>
        /// <param name="ip">The remote endpoint IP.</param>
        /// <param name="port">The remote endpoint port</param>
        /// <param name="logtaskid">The task ID to use.</param>
        public Task HandleRequestSimple(int handle, EndPoint remoteEndPoint, string logtaskid)
        {
            Program.DebugConsoleOutput("Got simple handling request for handle {0}", handle);

            var stream = m_pollhandler.MonitorHandle(handle);
            base.HandleRequest(stream, remoteEndPoint, logtaskid, () => stream.ReaderClosed);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// An instance that runs in the spawned process
    /// </summary>
    public static class SpawnedRunner
    {
        /// <summary>
        /// The environment variable used to define what unix socket to use
        /// </summary>
        public const string SOCKET_PATH_VARIABLE_NAME = "ceen-comm-socket";

        /// <summary>
        /// The environment variable used to define if a null prefix is used
        /// </summary>
        public const string SOCKET_PREFIX_VARIABLE_NAME = "ceen-comm-prefix";

        /// <summary>
        /// Runs the RPC peer
        /// </summary>
        /// <returns>An awaitable task.</returns>
        public static Task RunClientRPCListenerAsync()
        {
            // Make sure we have loaded these assemblies into the current domain
            var myTypes = new Type[] { typeof(Ceen.AsyncLock), typeof(Ceen.Httpd.HttpServer), typeof(Ceen.Mvc.Controller), typeof(Ceen.Security.PRNG) };

            // Read environment setup
            var path = Environment.GetEnvironmentVariable(SOCKET_PATH_VARIABLE_NAME);
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException($"No path found in the environment variable");
            var prefix = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SOCKET_PREFIX_VARIABLE_NAME)) ? string.Empty : "\0";

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            socket.Connect(new SockRock.UnixEndPoint(prefix + path));

            var ipc = new LeanIPC.InterProcessConnection(new NetworkStream(socket));
            var peer = new LeanIPC.RPCPeer(ipc, typeof(SpawnedServer));

            // Pass back the SpawnedServer instance as a reference
            peer.TypeSerializer.RegisterSerializationAction(typeof(SpawnedServer), LeanIPC.SerializationAction.Reference);

            // Support these types with automatically generated proxies
            peer.AddAutomaticProxy(typeof(IStorageEntry), typeof(IStorageEntry));
            peer.AddAutomaticProxy(typeof(IStorageCreator), typeof(IStorageCreator));

            // Add support for EndPoint's
            peer.TypeSerializer.RegisterEndPointSerializers();
            peer.TypeSerializer.RegisterIPEndPointSerializers();

            return Task.WhenAll(
                ipc.RunMainLoopAsync(true),
                ListenForRequests(peer, path, prefix)
            );
        }

        private static async Task ListenForRequests(RPCPeer peer, string prefix, string path)
        {
            Program.DebugConsoleOutput($"Setting up listener ...");

            var tp = new TypeSerializer(false, false);

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            socket.Connect(new SockRock.UnixEndPoint(prefix + path + "_fd"));

            Program.DebugConsoleOutput($"Connected to fd socket, reading initial data");

            SpawnedServer server;

            // Start by reading and verifying the protocol intial data
            var buffer = new byte[1024];
            var count = socket.Receive(buffer);

            Program.DebugConsoleOutput($"Got protocol data with {count} bytes");

            using (var ms = new System.IO.MemoryStream(buffer, 0, count, false))
            using (var bcs = new BinaryConverterStream(ms, tp, false))
            {
                var desc = await bcs.ReadAnyAsync<InitialProtocolDescription>();
                if (desc.Version != 1)
                    throw new Exception($"Expected protocol version 1, but got {desc.Version}");
                if (desc.RequestSignature != tp.GetShortTypeName(typeof(SocketRequest)))
                    throw new Exception($"Expected type name to be {tp.GetShortTypeName(typeof(SocketRequest))}, but it was {desc.RequestSignature}");
                if (!peer.RemoteHandler.TryGetLocalObject(desc.ServerHandle, out var obj) || obj == null)
                    throw new Exception($"Unable to find the instance with the given handle: {desc.ServerHandle}");
                server = obj as SpawnedServer;
                if (server == null)
                    throw new Exception($"Unable to find the instance with the given handle: {desc.ServerHandle}, got something that is not a server ...");
            }

            Program.DebugConsoleOutput($"Protocol verification completed, starting main loop");

            // Prepare the handle
            var rchandle = socket.Handle.ToInt32();

            // Use a single allocated buffer for all requests
            using (var ms = new System.IO.MemoryStream())
            using (var bcs = new BinaryConverterStream(ms, tp, false))
            {
                while (socket.Connected)
                {
                    try
                    {
                        // Get the next request from the socket
                        var req = SockRock.ScmRightsImplementation.recv_fds(rchandle);

                        Program.DebugConsoleOutput("Got request, parsing ...");

                        // Copy the buffer into the stream we read from
                        ms.Position = 0;
                        ms.Write(req.Item2, 0, req.Item2.Length);
                        ms.Position = 0;

                        // Extract the data
                        var data = await bcs.ReadAnyAsync<SocketRequest>();

                        Program.DebugConsoleOutput("Decoded request, local handle is {0} remote handle is {1}", req.Item1[0], data.Handle);

                        // All set, fire the request
                        server.HandleRequestSimple(req.Item1[0], new IPEndPoint(IPAddress.Parse(data.RemoteIP), data.RemotePort), data.LogTaskID);

                        Program.DebugConsoleOutput("Request handling completed");
                    }
                    catch(Exception ex)
                    {
                        Program.ConsoleOutput("Processing failed: {0}", ex);
                        return;
                    }
                }
            }

        }
    }
}
