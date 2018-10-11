using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ceen.Httpd.Cli.Runner
{
    /// <summary>
    /// Base class for handling the logic around starting and stopping instances
    /// </summary>
    public abstract class RunHandlerBase : IRunnerHandler
    {
        /// <summary>
        /// Path to the configuration file
        /// </summary>
        private readonly string m_path;

        /// <summary>
        /// The HTTP runner instance
        /// </summary>
        private InstanceRunner m_http_runner;
        /// <summary>
        /// The https runner instance
        /// </summary>
        private InstanceRunner m_https_runner;

        /// <summary>
        /// The task signalling stopped
        /// </summary>
        private readonly TaskCompletionSource<bool> m_stopped = new TaskCompletionSource<bool>();
        /// <summary>
        /// Gets a task that signals completion
        /// </summary>
        public Task StoppedAsync => m_stopped.Task;

        /// <summary>
        /// The storage creator
        /// </summary>
        private readonly IStorageCreator m_storage = new MemoryStorageCreator();

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.UnixSpawn.UnixSpawnHandler"/> class.
        /// </summary>
        /// <param name="path">The config file path.</param>
        public RunHandlerBase(string path)
        {
            m_path = path;
        }

        protected abstract IWrappedRunner CreateRunner(string path, bool useSSL, IStorageCreator storage, System.Threading.CancellationToken token);

        /// <summary>
        /// Reload this instance.
        /// </summary>
        public async Task ReloadAsync(bool http, bool https)
        {
            Program.DebugConsoleOutput("Reloading instance");
            var cfg = ConfigParser.ParseTextFile(m_path);
            var config = ConfigParser.CreateServerConfig(cfg);
            config.Storage = m_storage;

            ((MemoryStorageCreator)m_storage).ExpireCheckInterval = TimeSpan.FromSeconds(cfg.StorageExpirationCheckIntervalSeconds);

            var prevhttp = m_http_runner?.Wrapper;
            var prevhttps = m_https_runner?.Wrapper;

            IWrappedRunner new_http_runner = null;
            IWrappedRunner new_https_runner = null;

            Program.DebugConsoleOutput("Creating runners");

            try
            {
                if (http)
                    new_http_runner = CreateRunner(m_path, false, m_storage, default(System.Threading.CancellationToken));
                if (https)
                    new_https_runner = CreateRunner(m_path, true, m_storage, default(System.Threading.CancellationToken));
            }
            catch (Exception ex)
            {
                Program.DebugConsoleOutput("Failed to create runners: {0}", ex);

                // If we fail to start, just kill any of the instances we just started
                if (new_http_runner != null)
                    try { new_http_runner.Kill(); }
                    catch { }

                if (new_https_runner != null)
                    try { new_https_runner.Kill(); }
                    catch { }

                throw;
            }

            Program.DebugConsoleOutput("Created runners, replacing existing runners");

            // Set up the new instances
            m_http_runner = await ReplaceOrRestartAsync(m_http_runner, prevhttp, new_http_runner, cfg.HttpAddress, cfg.HttpPort, false, config);
            m_https_runner = await ReplaceOrRestartAsync(m_https_runner, prevhttps, new_https_runner, cfg.HttpsAddress, cfg.HttpsPort, true, config);


            Program.DebugConsoleOutput("Stopping old runners");

            if (new_https_runner == null)
            {
                if (m_https_runner != null)
                    m_https_runner.StopAsync();
            }
            else
            {
                if (m_https_runner == null)
                    m_https_runner = new InstanceRunner();

                m_https_runner.Wrapper = new_https_runner;
            }

            // TODO: If the runner is reconfigured, then restart it

            Program.DebugConsoleOutput("Setting up crash handlers.");

            // Set up a crash handler to capture crash in log
            var dummy = m_http_runner?.RunnerTask.ContinueWith(x =>
            {
                if (!m_http_runner.ShouldStop && InstanceCrashed != null)
                    InstanceCrashed(cfg.HttpAddress, false, x.IsFaulted ? x.Exception : new Exception("Unexpected stop"));
            });
            // Set up a crash handler to capture crash in log
            dummy = m_https_runner?.RunnerTask.ContinueWith(x =>
            {
                if (!m_https_runner.ShouldStop && InstanceCrashed != null)
                    InstanceCrashed(cfg.HttpsAddress, true, x.IsFaulted ? x.Exception : new Exception("Unexpected stop"));
            });

            Program.DebugConsoleOutput("Ensuring that old runners are stopped");

            if (prevhttp != null || prevhttps != null)
            {
                await Task.Run(async () =>
                {
                    // We need both to stop
                    var t = Task.WhenAll(new[] { prevhttp?.StopAsync(), prevhttps?.StopAsync() }.Where(x => x != null));

                    // Give old processes time to terminate (if they are handling requests)
                    var maxtries = cfg.MaxUnloadWaitSeconds;
                    while (maxtries-- > 0 && !t.IsCompleted)
                        await Task.WhenAny(t, Task.Delay(1000));

                    // If we failed to stop, request a kill
                    if (!t.IsCompleted)
                    {
                        Program.DebugConsoleOutput("Requesting kill of old runners");
                        if (prevhttp != null)
                            prevhttp.Kill();
                        if (prevhttps != null)
                            prevhttps.Kill();
                    }
                });
            }

            Program.DebugConsoleOutput("Reloading completed");
        }

        private async Task<InstanceRunner> ReplaceOrRestartAsync(InstanceRunner runner, IWrappedRunner prevhandler, IWrappedRunner newhandler, string newaddr, int newport, bool usessl, ServerConfig config)
        {
            if (newhandler == null)
            {
                // We are stopping the handler

                if (runner != null)
                    runner.StopAsync();
                runner = null;
            }
            else
            {
                // We are starting, or restarting the handler
                if (runner == null)
                {
                    // Just 
                    runner = new InstanceRunner { Wrapper = newhandler };
                    await runner.RestartAsync(newaddr, newport, usessl, config);
                }
                else
                {
                    // If any of these change, we need to restart the listen socket
                    if (runner.Address != newaddr || runner.Port != newport)
                    {
                        // Start the new instance first
                        var newrunner = new InstanceRunner { Wrapper = newhandler };
                        await newrunner.RestartAsync(newaddr, newport, usessl, config);

                        runner.StopAsync();
                        return newrunner;
                    }
                    // In this case we must restart the socket, but need to stop first
                    else if (config.SocketBacklog != runner.Config.SocketBacklog)
                    {
                        await runner.StopAsync();
                        runner = new InstanceRunner();
                        runner.Wrapper = newhandler;
                        await runner.RestartAsync(newaddr, newport, usessl, config);

                    }
                    else
                    {
                        // No changes, just apply the new handler for future requests
                        runner.Wrapper = newhandler;
                    }
                }
            }

            return runner;
        }

        public event Action<string, bool, Exception> InstanceCrashed;

        /// <summary>
        /// Stops the instance
        /// </summary>
        /// <returns>The async.</returns>
        public async Task StopAsync()
        {
            if (m_http_runner == null && m_https_runner == null)
                return;

            await Task.WhenAll(new Task[] { m_http_runner?.StopAsync(), m_https_runner?.StopAsync() }.Where(x => x != null));
            m_stopped.TrySetResult(true);
        }
    }
}
