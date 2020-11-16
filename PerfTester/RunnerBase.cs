using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoCoL;

namespace PerfTester
{
    /// <summary>
    /// The base class for a runner
    /// </summary>
    public abstract class RunnerBase
    {
        /// <summary>
        /// The worker options
        /// </summary>
        protected readonly IWorkerOptions m_options;

        /// <summary>
        /// The expected response to check for failures, or null if this is a warmup
        /// </summary>
        protected readonly string m_expectedresponse;

        /// <summary>
        /// The result of running the requests
        /// </summary>
        public readonly Task Result;

        /// <summary>
        /// The ID counter
        /// </summary>
        private static int _id;

        /// <summary>
        /// The instance ID
        /// </summary>
        protected readonly int m_id;

        /// <summary>
        /// Constructs a new runner
        /// </summary>
        /// <param name="options">The options to use</param>
        /// <param name="reqchan">The request channel</param>
        /// <param name="respchan">The response channel</param>
        /// <param name="response">The expected response string</param>
        public RunnerBase(IWorkerOptions options, IReadChannel<bool> reqchan, IWriteChannel<RequestResult> respchan, string response)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrWhiteSpace(options.RequestUrl) || !System.Uri.TryCreate(options.RequestUrl, UriKind.Absolute, out var _))
                throw new ArgumentException($"Invalid request url: {options.RequestUrl}", nameof(options.RequestUrl));
            if (string.IsNullOrWhiteSpace(options.Verb))
                throw new ArgumentException(nameof(options.Verb), "Missing HTTP verb");

            m_id = System.Threading.Interlocked.Increment(ref _id);
            m_options = options;
            m_expectedresponse = response;
            if (m_expectedresponse != null)
                Result = RunAsync(reqchan, respchan);
        }

        /// <summary>
        /// Runs warmup rounds
        /// </summary>
        /// <param name="count">The number of rounds to run</param>
        /// <returns>The message body read</returns>
        public async Task<string> RunWarmup(int count)
        {
            string lastresp = null;
            for(var i = 0; i < count; i++) {

                var resp = await PeformRequestAsync();
                if (resp != null)
                    lastresp = resp;
            }

            return lastresp;
        }

        /// <summary>
        /// The runner helper method that calls the abstract request method
        /// </summary>
        /// <returns>An awaitable task</returns>
        protected Task RunAsync(IReadChannel<bool> reqchan, IWriteChannel<RequestResult> respchan)
        {
            return AutomationExtensions.RunTask(
                new { reqchan, respchan },
                async _ =>
            {
                while(await reqchan.ReadAsync())
                {
                    var start = DateTime.Now;
                    try
                    {
                        var resp = await PeformRequestAsync();
                        await respchan.WriteAsync(new RequestResult() {
                            Started = start,
                            Finished = DateTime.Now,
                            Failed = m_expectedresponse != null && m_expectedresponse != resp
                        });
                    }
                    catch (System.Exception ex)
                    {
                        await respchan.WriteAsync(new RequestResult()
                        {
                            Started = start,
                            Finished = DateTime.Now,
                            Failed = true,
                            Exception = ex
                        });

                        if (m_options.Verbose)
                            Console.WriteLine(ex.Message);
                    }
                }
            });
        }

        // /// <summary>
        // /// The cancellation control
        // /// </summary>
        // protected readonly CancellationTokenSource m_cancelSource = new CancellationTokenSource();
        // /// <summary>
        // /// Stops the runner
        // /// </summary>
        // public void Stop() => m_cancelSource.Cancel();

        // /// <summary>
        // /// The number of requests performed with this instance
        // /// </summary>
        // public long RequestsPerformed { get; protected set; }

        // /// <summary>
        // /// The total number of failures
        // /// </summary>
        // /// <value></value>
        // public long Failures { get; protected set; }

        // /// <summary>
        // /// The last response body
        // /// </summary>
        // public string LastResponseBody { get; protected set; }

        // /// <summary>
        // /// The last exception seen by the worker
        // /// </summary>
        // public Exception LastException { get; protected set; }

        // /// <summary>
        // /// The durations of the performed (successfull) requests
        // /// </summary>
        // public readonly List<TimeSpan> Durations = new List<TimeSpan>();

        // /// <summary>
        // /// The result of running the requests
        // /// </summary>
        // public readonly Task Result;

        // /// <summary>
        // /// The worker options
        // /// </summary>
        // protected readonly IWorkerOptions m_options;
        // /// <summary>
        // /// The expected response to check for failures, or null if this is a warmup
        // /// </summary>
        // protected readonly string m_expectedresponse;
        // /// <summary>
        // /// The number of requests to perform.
        // /// </summary>
        // public readonly long RequestToPerform;

        // /// <summary>
        // /// Constructs a new runner
        // /// </summary>
        // /// <param name="options">The options to use</param>
        // /// <param name="maximumrequests">The number of requests to perform</param>
        // /// <param name="response">The expected response, or null</param>
        // public RunnerBase(IWorkerOptions options, long maximumrequests, string response)
        // {
        //     if (options == null)
        //         throw new ArgumentNullException(nameof(options));

        //     if (string.IsNullOrWhiteSpace(options.RequestUrl) || !System.Uri.TryCreate(options.RequestUrl, UriKind.Absolute, out var _))
        //         throw new ArgumentException($"Invalid request url: {options.RequestUrl}", nameof(options.RequestUrl));
        //     if (string.IsNullOrWhiteSpace(options.Verb))
        //         throw new ArgumentException(nameof(options.Verb), "Missing HTTP verb");

        //     if (maximumrequests <= 0)
        //         throw new ArgumentOutOfRangeException(nameof(maximumrequests), maximumrequests, "The number of requests must be greater than zero");

        //     m_options = options;
        //     m_expectedresponse = response;
        //     RequestToPerform = maximumrequests;
        //     Result = RunAsync();
        // }

        // /// <summary>
        // /// The runner helper method that calls the abstract request method
        // /// </summary>
        // /// <returns>An awaitable task</returns>
        // protected async Task RunAsync()
        // {
        //     for (var i = 0L; i < RequestToPerform; i++)
        //     {
        //         m_cancelSource.Token.ThrowIfCancellationRequested();
        //         try
        //         {
        //             var start = DateTime.Now;
        //             var resp = await PeformRequestAsync();
        //             Durations.Add(DateTime.Now - start);

        //             if (m_expectedresponse != null && m_expectedresponse != resp)
        //                 Failures++;


        //             if (resp != null)
        //                 LastResponseBody = resp;

        //         }
        //         catch (System.Exception ex)
        //         {
        //             LastException = ex;
        //             Failures++;
        //             if (m_options.Verbose)
        //                 Console.WriteLine(ex.Message);
        //         }

        //         RequestsPerformed++;
        //     }
        // }

        /// <summary>
        /// The actual request method
        /// </summary>
        /// <returns>The response body</returns>
        protected abstract Task<string> PeformRequestAsync();
    }
}