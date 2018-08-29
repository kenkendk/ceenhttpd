using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ceen.Httpd.Cli.Runner.InProcess
{
    /// <summary>
    /// The runner handler for in-process instances
    /// </summary>
    public class Runner : RunHandlerBase
    {
        public Runner(string path)
            : base(path)
        {
        }

        protected override IWrappedRunner CreateRunner(string path, bool useSSL, IStorageCreator storage, CancellationToken token)
        {
            return new InProcessWrapper(useSSL, path, storage, token);
        }


        /// <summary>
        /// The class wrapping a bridge
        /// </summary>
        public class InProcessWrapper : Httpd.HttpServer.InterProcessBridge, IWrappedRunner
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.Runner.InProcessRunner"/> class.
            /// </summary>
            /// <param name="usessl">If set to <c>true</c> use ssl.</param>
            /// <param name="configfile">The config file.</param>
            /// <param name="storage">The storage to use</param>
            /// <param name="token">The cancellation token to use</param>
            public InProcessWrapper(bool usessl, string configfile, IStorageCreator storage, CancellationToken token)
            {
                var config = ConfigParser.ValidateConfig(ConfigParser.ParseTextFile(configfile));
                config.Storage = storage ?? new MemoryStorageCreator();

                base.Setup(usessl, config);

                if (token.CanBeCanceled)
                    token.Register(() => this.Stop());
            }

            /// <summary>
            /// Kills the bridge instance
            /// </summary>
            public void Kill()
            {
                base.Stop();
            }

            /// <summary>
            /// Stops the bridge instance and awaits shutdown
            /// </summary>
            /// <returns>An awaitable task.</returns>
            public Task StopAsync()
            {
                base.Stop();
                return base.StopTask;
            }

            Task IWrappedRunner.HandleRequest(TcpClient client, EndPoint remoteEndPoint, string logtaskid)
            {
                base.HandleRequest(client, remoteEndPoint, logtaskid);
                return Task.FromResult(true);
            }
        }
    }
}
