using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CoCoL;

namespace PerfTester
{
    /// <summary>
    /// The WebRequest worker
    /// </summary>
    public class HttpClientWorker : RunnerBase
    {
        /// <summary>
        /// The http client to use
        /// </summary>
        private readonly HttpClient m_client = new HttpClient();

        /// <summary>
        /// Constructs a new runner
        /// </summary>
        /// <param name="options">The options to use</param>
        /// <param name="reqchan">The request channel</param>
        /// <param name="respchan">The response channel</param>
        /// <param name="response">The expected response string</param>
        public HttpClientWorker(IWorkerOptions options, IReadChannel<bool> reqchan, IWriteChannel<RequestResult> respchan, string response)
            //: base(options, maximumrequests, response)
            : base(options, reqchan, respchan, response)
        {
        }

        /// <summary>
        /// Builds a new HttpRequestMessage
        /// </summary>
        /// <returns>The HttpRequestMessage</returns>
        private HttpRequestMessage BuildMessage()
        {
            var method = typeof(HttpMethod)
                .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .FirstOrDefault(x => string.Equals(x.Name, m_options.Verb, StringComparison.OrdinalIgnoreCase))
                ?.GetValue(null, null) as HttpMethod;

            if (method == null)
                throw new ArgumentException("Unable to use the verb {} with HttpClient");

            var request = new HttpRequestMessage()
            {
                Method = method,
                RequestUri = new Uri(m_options.RequestUrl, UriKind.Absolute),                
            };

            if (!string.IsNullOrEmpty(m_options.Body))
                request.Content = new System.Net.Http.ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(m_options.Body));

            foreach (var h in m_options.Headers)
            {
                var parts = h?.Split(':', 2);
                if (parts == null || parts.Length < 2)
                    throw new ArgumentException($"Bad header format: {h}");

                request.Headers.Add(parts[0], parts[1]);
            }

            return request;
        }

        /// <inheritdoc />
        protected override async Task<string> PeformRequestAsync()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(m_options.RequestTimeoutSeconds));
            var resp = await m_client.SendAsync(BuildMessage(), cts.Token);
            return await resp.Content.ReadAsStringAsync();

        }
    }
}