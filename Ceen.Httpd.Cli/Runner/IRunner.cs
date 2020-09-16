using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ceen.Httpd.Cli.Runner
{
    /// <summary>
    /// Interface for a wrapped runner instance
    /// </summary>
    public interface IWrappedRunner
    {
        /// <summary>
        /// Handles a request
        /// </summary>
        /// <param name="socket">The socket handle.</param>
        /// <param name="remoteEndPoint">The remote endpoint.</param>
        /// <param name="logtaskid">The task ID to use.</param>
        Task HandleRequest(Socket client, EndPoint remoteEndPoint, string logtaskid);


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
    /// The runner performs the listen step
    /// </summary>
    public interface ISelfListen : IWrappedRunner
    {
        /// <summary>
        /// Flag used to toggle using managed listening
        /// </summary>
        /// <value><c>true</c> if using managed listen; otherwise, <c>false</c>.</value>
        bool UseManagedListen { get; }

        /// <summary>
        /// Listens for a new connection
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The connection and endpoint.</returns>
        Task<KeyValuePair<long, EndPoint>> ListenAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Handles a request
        /// </summary>
        /// <param name="client">The socket handle.</param>
        /// <param name="remoteEndPoint">The remote endpoint.</param>
        /// <param name="logtaskid">The task ID to use.</param>
        Task HandleRequest(long client, EndPoint remoteEndPoint, string logtaskid);

        /// <summary>
        /// Binds this instance to the given endpoint
        /// </summary>
        /// <param name="endPoint">End point.</param>
        /// <param name="backlog">The connection backlog</param>
        void Bind(EndPoint endPoint, int backlog);
    }

    /// <summary>
    /// Shared interface for an isolated handler
    /// </summary>
    public interface IRunnerHandler
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

