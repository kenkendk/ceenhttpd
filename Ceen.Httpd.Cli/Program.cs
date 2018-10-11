using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;

namespace Ceen.Httpd.Cli
{
	public static class Program
	{
        public static void DebugConsoleOutput(string msg, params object[] args)
        {
			System.IO.File.AppendAllLines("/Users/kenneth/crashlog.txt", new string[] { string.Format(msg, args) });
            Console.WriteLine(msg, args);
        }

        public static void ConsoleOutput(string msg, params object[] args)
        {
            Console.WriteLine(msg, args);
        }

        /// <summary>
        /// Gets a value indicating whether the current process is launched as a spawned child.
        /// </summary>
        public static bool IsSpawnedChild => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Runner.SubProcess.SpawnedRunner.SOCKET_PATH_VARIABLE_NAME));

        /// <summary>
        /// The current config file (null if the instance is a spawned child)
        /// </summary>
        public static string ConfigFile { get; private set; }

        /// <summary>
        /// The current parsed configuration (null if the instance is a spawned child)
        /// </summary>
        public static CLIServerConfiguration CLIConfiguration { get; private set; }

        public static int Main(string[] args)
		{
            DebugConsoleOutput("{0}: Started new process", System.Diagnostics.Process.GetCurrentProcess().Id);
            if (IsSpawnedChild)
            {
                try
                {
                    DebugConsoleOutput("{0}: Starting child process", System.Diagnostics.Process.GetCurrentProcess().Id);
                    Runner.SubProcess.SpawnedRunner.RunClientRPCListenerAsync().Wait();
                    DebugConsoleOutput("{0}: Finished child process", System.Diagnostics.Process.GetCurrentProcess().Id);
                }
                catch(Exception ex)
                {
                    DebugConsoleOutput("{0}: Crashed child: {1}", System.Diagnostics.Process.GetCurrentProcess().Id, ex);
                    return 1;
                }

                return 0;
            }

			if (args.Length != 1)
			{
                ConsoleOutput("Usage: {0} [path-to-config-file]", Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location));
				return 1;
			}

			if (!File.Exists(args[0]))
			{
                ConsoleOutput("CWD: {0}", Directory.GetCurrentDirectory());
                ConsoleOutput("File not found: {0}", args[0]);
				return 1;
			}

			var tcs = new System.Threading.CancellationTokenSource();
            CLIConfiguration = ConfigParser.ParseTextFile(args[0]);
			var tasks = new List<Task>();

            Runner.IRunnerHandler app;
            if (CLIConfiguration.IsolatedProcesses)
            {
                app = new Runner.SubProcess.Runner(args[0]);
            }
            else if (CLIConfiguration.IsolatedAppDomain)
            {
                app = new Runner.AppDomain.Runner(args[0]);
            }
			else
			{
                app = new Runner.InProcess.Runner(args[0]);
			}

            tasks.Add(app.StoppedAsync);

			var reloadevent = new TaskCompletionSource<bool>();
			var stopevent = new TaskCompletionSource<bool>();
			var hasrequestedstop = false;
			var hasrequestedreload = false;
			IDisposable fsw = null;

            ConsoleOutput("Server is running, press CTRL+C to reload config...");

			Func<bool> stop = () =>
			{
				if (!hasrequestedstop)
				{
					hasrequestedstop = true;
					stopevent.SetResult(true);
					return true;
				}

				return false;
			};

			Func<bool> reload = () =>
			{
				if (!hasrequestedreload)
				{
					hasrequestedreload = true;
					reloadevent.SetResult(true);
					return true;
				}
				else
					return stop();
			};

			Console.CancelKeyPress += (sender, e) =>
			{
				if (reload())
					e.Cancel = true;
			};

			if (CLIConfiguration.WatchConfigFile)
			{
				var configname = Path.GetFullPath(args[0]);
				var f = new FileSystemWatcher(Path.GetDirectoryName(configname));
				f.Changed += (sender, e) => {
					if (e.FullPath == configname)
						Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith( _ => reloadevent.SetResult(true));
				};
				f.EnableRaisingEvents = true;
				fsw = f;
			}

			app.InstanceCrashed += (address, ssl, ex) =>
			{
                ConsoleOutput($"Crashed for {(ssl ? "https" : "http")} {address}: {ex}");
				reloadevent.SetResult(true);
			};

            var primarytask = app.ReloadAsync(CLIConfiguration.ListenHttp, CLIConfiguration.ListenHttps);
			primarytask.Wait();
			if (primarytask.IsFaulted)
				throw primarytask.Exception;

			var sigtask = SignalHandler(tcs.Token, () =>
			{
				reloadevent.TrySetResult(true);
				return true;
			}, () => {
				stopevent.TrySetResult(true);
				return true;
			});

			var allitems = Task.WhenAll(tasks);
			var t = Task.WhenAny(allitems, stopevent.Task, reloadevent.Task).Result;

			using(fsw)
			while(t == reloadevent.Task)
			{
				reloadevent = new TaskCompletionSource<bool>();
				var waitdelay = Task.Delay(TimeSpan.FromSeconds(2));
                ConsoleOutput("Reloading configuration ...");
				try
				{
                    CLIConfiguration = ConfigParser.ParseTextFile(args[0]);
                    var tr = app.ReloadAsync(CLIConfiguration.ListenHttp, CLIConfiguration.ListenHttps);

                    tr.Wait();
					if (tr.IsFaulted)
						throw tr.Exception;
                    ConsoleOutput("Configuration reloaded!");
				}
				catch(Exception ex)
				{
                    ConsoleOutput("Failed to reload configuration with message: {0}", ex);
				}

				waitdelay.Wait();
				hasrequestedreload = false;

				t = Task.WhenAny(allitems, stopevent.Task, reloadevent.Task).Result;
			}

			if (t == stopevent.Task)
			{
                ConsoleOutput("Got stop signal, stopping server ...");
				if (app != null)
					app.StopAsync();
				tcs.Cancel();
				allitems.Wait();
				sigtask.Wait();
			}

            ConsoleOutput("Server has stopped...");

			return 0;
		}

		/// <summary>
		/// Installs a signal handler if Mono.Posix is found on the system
		/// </summary>
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		private static Task SignalHandler(System.Threading.CancellationToken token, Func<bool> reload, Func<bool> stop)
		{
            if (!SystemHelper.IsCurrentOSPosix)
                return Task.FromResult(true);

            var signals = new Mono.Unix.UnixSignal[] {
                new Mono.Unix.UnixSignal(Mono.Unix.Native.Signum.SIGHUP),
                new Mono.Unix.UnixSignal(Mono.Unix.Native.Signum.SIGINT),
                new Mono.Unix.UnixSignal(Mono.Unix.Native.Signum.SIGQUIT),
            };

            return Task.Run(() => {
                while (!token.IsCancellationRequested)
                {
                   var sig = Mono.Unix.UnixSignal.WaitAny(signals, TimeSpan.FromSeconds(5));

                    if (sig == 0 || sig == 1)
                        reload();
                    else if (sig == 2)
                        stop();
                }
            });


			//var asm = System.Reflection.Assembly.Load(new System.Reflection.AssemblyName("Mono.Posix, Culture=neutral, PublicKeyToken=0738eb9f132ed756"));
			//var unixsignal_t = Type.GetType("Mono.Unix.UnixSignal, Mono.Posix");
			//var unixsignum_t = Type.GetType("Mono.Unix.Native.Signum, Mono.Posix");

			//if (unixsignal_t == null || unixsignum_t == null)
			//	return Task.FromResult(true);

			//var sighup = unixsignum_t
			//	.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
			//	.Where(x => x.Name == "SIGHUP")
			//	.Select(x => x.GetValue(null))
			//	.First();
			//var sigint = unixsignum_t
			//	.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
			//	.Where(x => x.Name == "SIGINT")
			//	.Select(x => x.GetValue(null))
			//	.First();
			//var sigquit = unixsignum_t
			//	.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
			//	.Where(x => x.Name == "SIGQUIT")
			//	.Select(x => x.GetValue(null))
			//	.First();

			//var sighup_int = 0;
			//var sigint_int = 1;
			//var sigquit_int = 2;

			//var sigarray = Array.CreateInstance(unixsignal_t, 3);
			//sigarray.SetValue(Activator.CreateInstance(unixsignal_t, sighup), sighup_int);
			//sigarray.SetValue(Activator.CreateInstance(unixsignal_t, sigint), sigint_int);
			//sigarray.SetValue(Activator.CreateInstance(unixsignal_t, sigquit), sigquit_int);

			//var method = unixsignal_t.GetMethod(
			//	"WaitAny",
			//	System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
			//	null,
			//	new Type[] { sigarray.GetType(), typeof(TimeSpan) },
			//	null
			//);

			//if (method == null)
			//	return Task.FromResult(true);

			//var args = new object[] { sigarray, TimeSpan.FromSeconds(5) };

			//return Task.Run(() => {
			//	while (!token.IsCancellationRequested)
			//	{
			//		var sig = (int)method.Invoke(null, args);
			//		if (sig == sighup_int || sig == sigint_int)
			//			reload();
			//		else if (sig == sigquit_int)
			//			stop();
			//	}
			//});
		}
	}
}
