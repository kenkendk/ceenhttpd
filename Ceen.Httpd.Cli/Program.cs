using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;

namespace Ceen.Httpd.Cli
{
	class MainClass
	{
		public static int Main(string[] args)
		{
			if (args.Length != 1)
			{
				Console.WriteLine("Usage: Ceen.Httpd.Cli [path-to-config-file]");
				return 1;
			}

			if (!File.Exists(args[0]))
			{
				Console.WriteLine("File not found: {0}", args[0]);
				return 1;
			}

			AppDomainHandler app = null;
			var tcs = new System.Threading.CancellationTokenSource();
			var config = ConfigParser.ParseTextFile(args[0]);
			var tasks = new List<Task>();

			if (!config.IsolatedAppDomain)
			{
				var serverconfig = ConfigParser.ValidateConfig(config);
				serverconfig.Storage = new MemoryStorageCreator() 
				{ 
					ExpireCheckInterval = TimeSpan.FromSeconds(config.StorageExpirationCheckIntervalSeconds) 
				};

				if (!string.IsNullOrWhiteSpace(config.HttpAddress))
					tasks.Add(HttpServer.ListenAsync(
						new IPEndPoint(ConfigParser.ParseIPAddress(config.HttpAddress), config.HttpPort),
						false,
						serverconfig,
						tcs.Token));

				if (!string.IsNullOrWhiteSpace(config.HttpsAddress) && !string.IsNullOrWhiteSpace(config.CertificatePath))
					tasks.Add(HttpServer.ListenAsync(
						new IPEndPoint(ConfigParser.ParseIPAddress(config.HttpsAddress), config.HttpsPort),
						true,
						serverconfig,
						tcs.Token));
			}
			else
			{
				app = new AppDomainHandler(args[0]);
				tasks.Add(app.StoppedAsync);
			}

			var reloadevent = new TaskCompletionSource<bool>();
			var stopevent = new TaskCompletionSource<bool>();
			var hasrequestedstop = false;
			var hasrequestedreload = false;

			if (app == null)
				Console.WriteLine("Server is running, press CTRL+C to stop...");
			else
				Console.WriteLine("Server is running, press CTRL+C to reload config...");

			Func<bool> stop = () =>
			{
				if (app != null && !hasrequestedstop)
				{
					hasrequestedstop = true;
					stopevent.SetResult(true);
					return true;
				}

				return false;
			};

			Func<bool> reload = () =>
			{
				if (app != null && !hasrequestedreload)
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

			if (app != null)
			{
				app.InstanceCrashed += (address, ssl, ex) =>
				{
					Console.WriteLine($"Crashed for {(ssl ? "https" : "http")} {address}: {ex}");
					reloadevent.SetResult(true);
				};
				var primarytask = app.ReloadAsync(true, true);
				primarytask.Wait();
				if (primarytask.IsFaulted)
					throw primarytask.Exception;
			}

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

			while(t == reloadevent.Task)
			{
				reloadevent = new TaskCompletionSource<bool>();
				var waitdelay = Task.Delay(TimeSpan.FromSeconds(2));
				if (app != null)
				{
					Console.WriteLine("Reloading configuration ...");
					try
					{
						var tr = app.ReloadAsync(true, true);
						tr.Wait();
						if (tr.IsFaulted)
							throw tr.Exception;
					}
					catch(Exception ex)
					{
						Console.WriteLine("Failed to reload configuration with message: {0}", ex);
					}
				}
				else
				{
					Console.WriteLine("Not reloading as we are not using isolated domains ...");
				}

				waitdelay.Wait();
				hasrequestedreload = false;

				t = Task.WhenAny(allitems, stopevent.Task, reloadevent.Task).Result;
			}

			if (t == stopevent.Task)
			{
				Console.WriteLine("Got stop signal, stopping server ...");
				if (app != null)
					app.StopAsync();
				tcs.Cancel();
				allitems.Wait();
				sigtask.Wait();
			}

			Console.WriteLine("Server has stopped...");

			return 0;
		}

		/// <summary>
		/// Installs a signal handler if Mono.Posix is found on the system
		/// </summary>
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		private static Task SignalHandler(System.Threading.CancellationToken token, Func<bool> reload, Func<bool> stop)
		{
			var asm = System.Reflection.Assembly.Load(new System.Reflection.AssemblyName("Mono.Posix, Culture=neutral, PublicKeyToken=0738eb9f132ed756"));
			var unixsignal_t = Type.GetType("Mono.Unix.UnixSignal, Mono.Posix");
			var unixsignum_t = Type.GetType("Mono.Unix.Native.Signum, Mono.Posix");

			if (unixsignal_t == null || unixsignum_t == null)
				return Task.FromResult(true);

			var sighup = unixsignum_t
				.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
				.Where(x => x.Name == "SIGHUP")
				.Select(x => x.GetValue(null))
				.First();
			var sigint = unixsignum_t
				.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
				.Where(x => x.Name == "SIGINT")
				.Select(x => x.GetValue(null))
				.First();
			var sigquit = unixsignum_t
				.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
				.Where(x => x.Name == "SIGQUIT")
				.Select(x => x.GetValue(null))
				.First();

			var sighup_int = 0;
			var sigint_int = 1;
			var sigquit_int = 2;

			var sigarray = Array.CreateInstance(unixsignal_t, 3);
			sigarray.SetValue(Activator.CreateInstance(unixsignal_t, sighup), sighup_int);
			sigarray.SetValue(Activator.CreateInstance(unixsignal_t, sigint), sigint_int);
			sigarray.SetValue(Activator.CreateInstance(unixsignal_t, sigquit), sigquit_int);

			var method = unixsignal_t.GetMethod(
				"WaitAny",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
				null,
				new Type[] { sigarray.GetType(), typeof(TimeSpan) },
				null
			);

			if (method == null)
				return Task.FromResult(true);

			var args = new object[] { sigarray, TimeSpan.FromSeconds(5) };

			return Task.Run(() => {
				while (!token.IsCancellationRequested)
				{
					var sig = (int)method.Invoke(null, args);
					if (sig == sighup_int || sig == sigint_int)
						reload();
					else if (sig == sigquit_int)
						stop();
				}
			});
		}
	}
}
