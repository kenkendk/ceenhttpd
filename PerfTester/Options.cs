using System.Collections.Generic;
using CommandLine;

namespace PerfTester
{
    /// <summary>
    /// The options shared among all workers
    /// </summary>
    public interface IWorkerOptions
    {
        string Verb { get; set; }
        IEnumerable<string> Headers { get; set; }
        string Body { get; set; }
        bool Verbose { get; set; }
        string RequestUrl { get; set; }
        int RequestTimeoutSeconds { get; set; }
    }

    /// <summary>
    /// The implemented http clients
    /// </summary>
    public enum HttpClientType
    {
        /// <summary>Spawn curl and make the request with curl (requires curl to be installed and in the current path)</summary>
        Curl,
        /// <summary>Use the .Net WebRequest</summary>
        WebRequest,
        /// <summary>Use the .Net HttpClient</summary>
        HttpClient,
        /// <summary>Use a raw socket</summary>
        Socket
    }

    /// <summary>
    /// Commandline options supported
    /// </summary>
    public class Options : IWorkerOptions
    {
        [Option(HelpText = "Sets the number of parallel requests", Default = 1)]
        public int ParallelRequests { get; set; }

        [Option(HelpText = "Choose the http client to use", Default = HttpClientType.WebRequest)]
        public HttpClientType Client { get; set; }

        [Option(HelpText = "The maximum number of requests to perform with each worker. Zero or negative values means infinite", Default = 0)]
        public int RecycleWorkerCount { get; set; }

        [Option(HelpText = "Sets the number of warmup requests to perform", Default = 1)]
        public int WarmupRequests { get; set; } = 1;

        [Option(HelpText = "The HTTP verb to use", Default = "GET")]
        public string Verb { get; set; }

        [Option(HelpText = "The HTTP headers to send")]
        public IEnumerable<string> Headers { get; set; }

        [Option(HelpText = "The HTTP body to use", Default = null)]
        public string Body { get; set; }

        [Option(HelpText = "Prints all messages to standard output", Default = false)]
        public bool Verbose { get; set; }

        [Option(HelpText = "The number of seconds to wait for the request before declaring timeout", Default = 20)]
        public int RequestTimeoutSeconds { get; set; }

        [Value(0, MetaName = "url", HelpText = "The URL to request", Required = true)]
        public string RequestUrl { get; set; }

        [Value(1, MetaName = "count", HelpText = "The total number of requests to perform", Required = false, Default = 1000)]
        public long RequestCount { get; set; }
    }
}