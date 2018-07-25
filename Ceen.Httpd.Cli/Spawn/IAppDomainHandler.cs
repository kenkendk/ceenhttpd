using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Ceen.Httpd.Cli.Spawn
{
    /// <summary>
    /// Interface for a wrapped runner instance
    /// </summary>
    public interface IWrappedRunner
    {
        /// <summary>
        /// Handles a request
        /// </summary>
        /// <param name="client">The socket handle.</param>
        /// <param name="remoteEndPoint">The remote endpoint.</param>
        /// <param name="logtaskid">The task ID to use.</param>
        Task HandleRequest(TcpClient client, EndPoint remoteEndPoint, string logtaskid);

        /// <summary>
        /// Requests that this instance stops serving requests
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Kill the remote instance.
        /// </summary>
        void Kill();

    }

    /// <summary>
    /// Interface for sending requests over a domain or process boundary
    /// </summary>
    public interface IRemotingWrapper
    {
        /// <summary>
        /// Setup this instance
        /// </summary>
        /// <param name="usessl">If set to <c>true</c> usessl.</param>
        /// <param name="configfile">Path to the configuration file</param>
        /// <param name="storage">The storage instance or null</param>
        void SetupFromFile(bool usessl, string configfile, IStorageCreator storage);

        /// <summary>
        /// Handles a request
        /// </summary>
        /// <param name="socket">The socket handle.</param>
        /// <param name="remoteEndPoint">The remote endpoint.</param>
        /// <param name="logtaskid">The task ID to use.</param>
        void HandleRequest(SocketInformation socket, EndPoint remoteEndPoint, string logtaskid);

        /// <summary>
        /// Requests that this instance stops serving requests
        /// </summary>
        void Stop();

        /// <summary>
        /// Waits for all clients to finish processing
        /// </summary>
        /// <returns><c>true</c>, if for stop succeeded, <c>false</c> otherwise.</returns>
        /// <param name="waitdelay">The maximum time to wait for the clients to stop.</param>
        bool WaitForStop(TimeSpan waitdelay);

        /// <summary>
        /// Gets the number of active clients.
        /// </summary>
        int ActiveClients { get; }
    }

    /// <summary>
    /// Shared interface for an isolated handler
    /// </summary>
    public interface IAppDomainHandler
    {
        /// <summary>
        /// Gets a task that signals completion
        /// </summary>
        Task StoppedAsync { get; }

        /// <summary>
        /// An event that is raised if the listener crashes
        /// </summary>
        event Action<string, bool, Exception> InstanceCrashed;

        /// <summary>
        /// Reload this instance.
        /// </summary>
        Task ReloadAsync(bool http, bool https);

        /// <summary>
        /// Stops all instances
        /// </summary>
        Task StopAsync();
    }
}
