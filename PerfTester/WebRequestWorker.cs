using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoCoL;

namespace PerfTester
{
    public class WebRequestWorker : RunnerBase
    {
        /// <summary>
        /// The pre-parsed headers to use
        /// </summary>
        private readonly Dictionary<string, string> m_headers = new Dictionary<string, string>();

        /// <summary>
        /// The encoded body
        /// </summary>
        private readonly byte[] m_data;

        /// <summary>
        /// Helper signal to delay the initial request until the constructor has completed
        /// </summary>
        private readonly TaskCompletionSource<bool> m_initialized = new TaskCompletionSource<bool>();

        /// <summary>
        /// Constructs a new runner
        /// </summary>
        /// <param name="options">The options to use</param>
        /// <param name="reqchan">The request channel</param>
        /// <param name="respchan">The response channel</param>
        /// <param name="response">The expected response string</param>
        public WebRequestWorker(IWorkerOptions options, IReadChannel<bool> reqchan, IWriteChannel<RequestResult> respchan, string response)
            //: base(options, maximumrequests, response)
            : base(options, reqchan, respchan, response)
        {
            foreach (var h in options.Headers)
            {
                var parts = h?.Split(':', 2);
                if (parts == null || parts.Length < 2)
                    throw new ArgumentException($"Bad header format: {h}");

                m_headers.Add(parts[0], parts[1]);
            }

            m_data = string.IsNullOrEmpty(options.Body) ? null : System.Text.Encoding.UTF8.GetBytes(options.Body);
            m_initialized.TrySetResult(true);
        }

        /// <inheritdoc />
        protected override async Task<string> PeformRequestAsync()
        {
            await m_initialized.Task;

            var req = System.Net.WebRequest.CreateHttp(m_options.RequestUrl);
            req.Method = m_options.Verb;
            req.Timeout = (int)TimeSpan.FromSeconds(m_options.RequestTimeoutSeconds).TotalMilliseconds;

            foreach (var h in m_headers)
                req.Headers.Add(h.Key, h.Value);

            if (m_data != null)
                using (var rs = await req.GetRequestStreamAsync())
                    await rs.WriteAsync(m_data, 0, m_data.Length);

            using(var r = await req.GetResponseAsync())
            using(var resp = r.GetResponseStream())
            using(var sr = new System.IO.StreamReader(resp, System.Text.Encoding.UTF8, true))
                return sr.ReadToEnd();
        }
    }
}