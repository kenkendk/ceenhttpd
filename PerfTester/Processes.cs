using CoCoL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PerfTester
{
    public struct StatRequest
    {
        public TaskCompletionSource<ResultStats> Result;
    }

    public struct RequestResult
    {
        public DateTime Started;
        public DateTime Finished;
        public bool Failed;
        public Exception Exception;
    }

    public struct ResultStats
    {
        public TimeSpan Duration;
        public long Requests;
        public long Failures;
        public TimeSpan Min;
        public TimeSpan Mean;
        public TimeSpan Max;
        public TimeSpan StdDeviation;
        public TimeSpan Pct90;
        public TimeSpan Pct95;
        public TimeSpan Pct99;
    }

    public static class Processes
    {
        private const bool D = false;

        public static Task RunGranterAsync(IWriteChannel<bool> channel, long count, CancellationToken token)
        {
            var canceltask = new TaskCompletionSource<bool>();
            token.Register(() => canceltask.TrySetCanceled());
            var total = count;

            return AutomationExtensions.RunTask(
                new { channel },

                async self => {
                    while(count > 0)
                    {
                        DebugWriteLine($"Emitting task {total - count} of {total}");
                        if (await Task.WhenAny(new [] { canceltask.Task, channel.WriteAsync(true) }) == canceltask.Task)
                            throw new TaskCanceledException();

                        count--;
                        DebugWriteLine($"Emitted task {total - count} of {total}");
                    }

                    DebugWriteLine("Stopping task granter");

                }
            );
        }

        public static Task RunStatPrinterAsync(IWriteChannel<StatRequest> channel, TimeSpan period, long total, CancellationToken token)
        {
            return AutomationExtensions.RunTask(
                new { channel },
                async _ => {
                    while(true)
                    {
                        await Task.Delay(period, token);
                        var tcs = new TaskCompletionSource<ResultStats>();

                        await channel.WriteAsync(new StatRequest() {
                            Result = tcs
                        });

                        var res = await tcs.Task;

                        var pg = (res.Requests / (double)total) * 100;
                        Console.WriteLine($"    {pg:0.00}% ({res.Requests} of {total}) {(res.Failures == 0 ? "" : $"{res.Failures} {(res.Failures == 1 ? "failure" : "failures")}")}");
                    }
                }
            );
        }

        public static async Task RunWorkersAsync(IReadChannel<bool> requests, int workers, Func<RunnerBase> starter)
        {
            var active = new List<RunnerBase>();
            while(!await requests.IsRetiredAsync)
            {
                while (active.Count < workers)
                {
                    DebugWriteLine($"Starting worker {active.Count + 1} of {workers}");
                    active.Add(starter());
                }

                DebugWriteLine("Waiting for workers to complete");
                await Task.WhenAny(active.Select(x => x.Result));
                DebugWriteLine("One or more workers completed");

                for (var i = active.Count - 1; i >= 0; i--)
                    if (active[i].Result.IsCompleted)
                        active.RemoveAt(i);
                DebugWriteLine($"{workers - active.Count} workers completed");
            }

            DebugWriteLine("No more requests, waiting for all workers to complete");
            await Task.WhenAll(active.Select(x => x.Result));
        }
    
        public static async Task<ResultStats> RunCollectorAsync(IReadChannel<RequestResult> channel, IReadChannel<StatRequest> stats, CancellationToken token)
        {
            var canceltask = new TaskCompletionSource<bool>();
            token.Register(() => canceltask.TrySetCanceled());

            var durations = new List<long>();

            var started = 0L;
            var finished = 0L;
            var count = 0L;
            var failures = 0L;

            var chans = new IMultisetRequestUntyped[] {channel.RequestRead(), stats.RequestRead()};

            try
            {
                using(channel.AsReadOnly())
                {
                    while(true)
                    {
                        var res = await chans.ReadFromAnyAsync();
                        DebugWriteLine($"Got message of type {res.Value.GetType()}");
                        if (res.Value is RequestResult a)
                        {
                            started = started == 0 ? a.Started.Ticks : Math.Min(a.Started.Ticks, started);
                            finished = finished == 0 ? a.Finished.Ticks : Math.Max(a.Finished.Ticks, finished);

                            count++;

                            if (a.Failed)
                                failures++;
                            else
                                durations.Add((a.Finished - a.Started).Ticks);
                        }
                        else if(res.Value is StatRequest s)
                        {
                            s.Result.TrySetResult(new ResultStats() {
                                Duration = new TimeSpan(finished - started),
                                Requests = count,
                                Failures = failures,
                            });
                        }
                    }
                }
            }
            catch
            {
            }

            if (durations.LongCount() == 0)
                return new ResultStats();
            DebugWriteLine($"All requests performed, computing stats");
            var mean = durations.Sum() / durations.LongCount();
            var sqdev = durations.Select(x => Math.Pow(x - mean, 2)).Sum();
            var avgdev = sqdev / durations.LongCount();
            var sqr = Math.Sqrt(Math.Abs(avgdev));

            var sorted = durations.Select(x => x).OrderBy(x => x);
            var n99 = (int)Math.Ceiling(0.99 * (durations.Count - 1));
            var n95 = (int)Math.Ceiling(0.95 * (durations.Count - 1));
            var n90 = (int)Math.Ceiling(0.90 * (durations.Count - 1));

            var min = sorted.FirstOrDefault();
            var t0 = sorted.Skip(n90);
            var t90 = t0.FirstOrDefault();
            t0 = t0.Skip(n95 - n90);
            var t95 = t0.FirstOrDefault();
            t0 = t0.Skip(n99 - n95);
            var t99 = t0.FirstOrDefault();
            var max = t0.LastOrDefault();

            DebugWriteLine($"All requests performed, returning stats");
            return new ResultStats()
            {
                Duration = new TimeSpan(finished - started),
                Requests = count,
                Failures = failures,
                
                Min = new TimeSpan(min),
                Mean = new TimeSpan(mean),
                Max = new TimeSpan(max),
                StdDeviation = new TimeSpan((long)sqr),
                Pct90 = new TimeSpan(t90),
                Pct95 = new TimeSpan(t95),
                Pct99 = new TimeSpan(t99)
            };
        }

        private static void DebugWriteLine(string msg)
        {
            //Console.WriteLine(msg);
        }
    }
}