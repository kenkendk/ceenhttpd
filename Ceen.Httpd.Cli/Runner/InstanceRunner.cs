using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ceen.Httpd.Cli.Runner
{
    /// <summary>
    /// Class for keeping the state of a listener
    /// </summary>
    internal class InstanceRunner
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
        public IWrappedRunner Wrapper { get; set; }

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
        public async Task RestartAsync(string address, int port, bool usessl, ServerConfig config)
        {
            if (RunnerTask != null)
                await StopAsync();

            m_token = new CancellationTokenSource();
            Port = port;
            Config = config;
            UseSSL = usessl;
            Address = address;
            ShouldStop = false;

            if (Wrapper is ISelfListen isl && !isl.UseManagedListen)
            {
                isl.Bind(new IPEndPoint(ConfigParser.ParseIPAddress(address), port), config.SocketBacklog);
                RunnerTask = HttpServer.ListenToSocketAsync(
                    isl.ListenAsync, 
                    usessl, 
                    m_token.Token, 
                    config,
                    (c, e, l) => ((ISelfListen)Wrapper).HandleRequest(c, e, l)
                );
            }
            else
            {
                RunnerTask = HttpServer.ListenToSocketAsync(
                    new IPEndPoint(ConfigParser.ParseIPAddress(address), port),
                    usessl,
                    m_token.Token,
                    config,
                    (c, e, l) => Wrapper.HandleRequest(c, e, l)
                );
            }
        }
    }
}
