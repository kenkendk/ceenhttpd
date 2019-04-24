using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

namespace PerfTester
{
 
    class Program
    {
        static int Main(string[] args)
        {
            int code = 0;
            
            var parser = new CommandLine.Parser(c => {
                c.CaseInsensitiveEnumValues = true;
            });

            parser
                .ParseArguments<Options>(args)
                .WithParsed<Options>(opts => code = Main(opts).Result)
                .WithNotParsed<Options>(errs =>
                {
                    foreach (var e in errs)
                         code = ReportError(e.ToString());
                });

            return code;

        }

        static int ReportError(string message)
        {
            Console.WriteLine(message);
            return -1;
        }

        static string FormatTimeToNow(DateTime start)
        {
            return FormatTimespan(DateTime.Now - start);
        }

        static string FormatTimespan(TimeSpan duration)
        {
            if (duration.Ticks < 0)
                return duration.ToString();

            if (duration.TotalMinutes < 1)
                return $"{duration.Seconds}.{duration.Milliseconds}s";
            if (duration.TotalHours < 1)
                return $"{duration.Minutes}m {duration.Seconds}.{duration.Milliseconds}s";
            if (duration.TotalDays < 1)
                return $"{duration.Hours}m {duration.Minutes}m {duration.Seconds}.{duration.Milliseconds}s";                
            return $"{duration.Days}d {duration.Hours}m {duration.Minutes}m {duration.Seconds}.{duration.Milliseconds}s";
        }

        static async Task<int> Main(Options options)
        {
            if (string.IsNullOrWhiteSpace(options.RequestUrl))
                return ReportError($"Missing request url");
            if (string.IsNullOrWhiteSpace(options.Verb))
                return ReportError($"Missing HTTP verb");
            if (options.ParallelRequests < 0)
                return ReportError($"The number of parallel requests must be a positive integer");
            if (options.RequestCount < options.ParallelRequests)
                return ReportError($"The number of requests must be equal to or larger than the number of workers");
            if (options.WarmupRequests < 0)
                return ReportError($"The number of warmup requests must be a positive integer");

            var reqchan = CoCoL.ChannelManager.CreateChannel<bool>();
            var respchan = CoCoL.ChannelManager.CreateChannel<RequestResult>();
            var statchan = CoCoL.ChannelManager.CreateChannel<StatRequest>();

            string expectedresponse = null;

            Func<RunnerBase> starter = null;
            if (options.Client == HttpClientType.Curl)
                starter = () => new CurlWorker(options, reqchan, respchan, expectedresponse);
            if (options.Client == HttpClientType.HttpClient)
                starter = () => new HttpClientWorker(options, reqchan, respchan, expectedresponse);
            if (options.Client == HttpClientType.WebRequest)
                starter = () => new WebRequestWorker(options, reqchan, respchan, expectedresponse);
            if (options.Client == HttpClientType.Socket)
                starter = () => new SocketWorker(options, reqchan, respchan, expectedresponse);
            if (starter == null)
                return ReportError($"Unable to determine the HTTP client type");


            var start = DateTime.Now;
            Console.WriteLine($"Performing {options.WarmupRequests} warmup {(options.WarmupRequests == 1 ? "request" : "requests")} ...");
            
            // Start the warmups
            expectedresponse = await starter().RunWarmup(options.WarmupRequests);
            if (expectedresponse == null)
                return ReportError($"Got a null response during warmup, stopping");

            ThreadPool.GetMinThreads(out var pmin1, out var pmin2);
            ThreadPool.SetMinThreads(Math.Max(pmin1, options.ParallelRequests), Math.Max(pmin2, options.ParallelRequests));

            Console.WriteLine($"{options.WarmupRequests} warmup {(options.WarmupRequests == 1 ? "request" : "requests")} performed in {FormatTimeToNow(start)}");

            Console.WriteLine($"Performing {options.RequestCount} {(options.RequestCount == 1 ? "request" : "requests")} with {options.ParallelRequests} {(options.ParallelRequests == 1 ? "worker" : "workers")}");

            var cancel = new CancellationTokenSource();
            var collector = Processes.RunCollectorAsync(respchan, statchan, cancel.Token);
            var printer = Processes.RunStatPrinterAsync(statchan, TimeSpan.FromSeconds(10), options.RequestCount, cancel.Token);

            await Task.WhenAll(new[] {
                Processes.RunGranterAsync(reqchan, options.RequestCount, cancel.Token),
                collector,
                Processes.RunWorkersAsync(reqchan, options.ParallelRequests, starter),
            });

            cancel.Cancel();
            var res = await collector;


            Console.WriteLine($"Completed {options.RequestCount} {(options.RequestCount == 1 ? "request" : "requests")}");
            if (res.Failures > 0)
                Console.WriteLine($"Failures: {res.Failures} {(((res.Failures * 100f) / options.RequestCount)):0.00}%");
            Console.WriteLine($"Duration: {FormatTimespan(res.Duration)}");
            Console.WriteLine("  Min  /   Avg  /   Max");
            Console.WriteLine($"{FormatTimespan(res.Min)} / {FormatTimespan(res.Mean)} / {FormatTimespan(res.Max)}");
            // Console.WriteLine($"Min: {FormatTimespan(durations.Min())}");
            // Console.WriteLine($"Max: {FormatTimespan(durations.Max())}");
            // Console.WriteLine($"Avg: {FormatTimespan(new TimeSpan(mean))}");
            Console.WriteLine($"Std dev: {FormatTimespan(res.StdDeviation)}");
            Console.WriteLine($"90th percentile: {FormatTimespan(res.Pct90)}");
            Console.WriteLine($"95th percentile: {FormatTimespan(res.Pct95)}");
            Console.WriteLine($"99th percentile: {FormatTimespan(res.Pct99)}");

            return 0;
        }
    }
}
