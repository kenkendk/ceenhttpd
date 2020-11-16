using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoCoL;

namespace PerfTester
{
    public class SocketWorker : RunnerBase
    {
        /// <summary>
        /// Helper signal to delay the initial request until the constructor has completed
        /// </summary>
        private readonly TaskCompletionSource<bool> m_initialized = new TaskCompletionSource<bool>();

        /// <summary>
        /// The encoded request
        /// </summary>
        private readonly byte[] m_data;

        /// <summary>
        /// The CRLF pair
        /// </summary>
        private const string CRLF = "\r\n";

        /// <summary>
        /// Constructs a new runner
        /// </summary>
        /// <param name="options">The options to use</param>
        /// <param name="reqchan">The request channel</param>
        /// <param name="respchan">The response channel</param>
        /// <param name="response">The expected response string</param>
        public SocketWorker(IWorkerOptions options, IReadChannel<bool> reqchan, IWriteChannel<RequestResult> respchan, string response) 
            : base(options, reqchan, respchan, response)
        {
            var sb = new StringBuilder();
            var uri = new Uri(options.RequestUrl);

            sb.Append($"{options.Verb} {uri.PathAndQuery} HTTP/1.1{CRLF}");
            sb.Append($"Host: {uri.Host}{CRLF}");
            foreach (var h in options.Headers)
                sb.Append($"{h}{CRLF}");
            sb.Append(CRLF);
            if (!string.IsNullOrEmpty(options.Body))
                sb.Append(options.Body);

            m_data = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            m_initialized.TrySetResult(true);
        }

        protected override async Task<string> PeformRequestAsync()
        {
            var uri = new Uri(m_options.RequestUrl);
            var ct = new CancellationTokenSource();
            //ct.CancelAfter(m_options.RequestTimeoutSeconds);

            using(var client = new TcpClient())
            {
                await client.ConnectAsync(uri.Host, uri.Port);
                using (var rawstream = client.GetStream())
                using (var stream = 
                    string.Equals("https", uri.Scheme, StringComparison.OrdinalIgnoreCase)
                    ? (Stream)new System.Net.Security.SslStream(rawstream)
                    : rawstream
                )
                {
                    if (stream is System.Net.Security.SslStream sslStream)
                        await sslStream.AuthenticateAsClientAsync(uri.Host);

                    await stream.WriteAsync(m_data, 0, m_data.Length, ct.Token);
                    await stream.FlushAsync(ct.Token);

                    using(var bsr = new Ceen.Httpd.BufferedStreamReader(stream))
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        ct.Token.Register(() => tcs.TrySetCanceled());

                        // Allow up to 2GiB responses
                        bsr.GetType()
                            .GetMethod("ResetReadLength", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                            .Invoke(bsr, new object[] { int.MaxValue });

                        var contentType = "text/plain; charset=utf-8";
                        var contentLength = string.Empty;
                        var code = string.Empty;

                        await bsr.ReadHeaders(2 * 1024, 8 * 1024, TimeSpan.FromSeconds(m_options.RequestTimeoutSeconds), (s) => {
                            if (s != null)
                            {
                                // HACK: We read the status line as a header line
                                if (string.IsNullOrEmpty(code))
                                {
                                    code = s;
                                }
                                else
                                {
                                if (s.StartsWith("Content-Type: ", StringComparison.OrdinalIgnoreCase))
                                    contentType = s.Substring("Content-Type: ".Length);
                                if (s.StartsWith("Content-Length: ", StringComparison.OrdinalIgnoreCase))
                                    contentLength = s.Substring("Content-Length: ".Length);
                                }
                            }                                
                        }, tcs.Task, tcs.Task);

                        var encoding = Ceen.RequestUtility.GetEncodingForContentType(contentType);
                        var length = -1L;
                        if (long.TryParse(contentLength, out var len))
                            length = len;

                        if (length == 0)
                            return string.Empty;
                        
                        if (length < 0)
                            return await Ceen.RequestUtility.ReadAllAsStringAsync(bsr, encoding, ct.Token);
                        
                        using(var ss = new Ceen.Httpd.LimitedBodyStream(bsr, length, TimeSpan.FromSeconds(m_options.RequestTimeoutSeconds), tcs.Task, tcs.Task))
                        {
                            var res = new byte[length];
                            return await Ceen.RequestUtility.ReadAllAsStringAsync(ss, encoding, ct.Token);
                        }
                    }
                }
            }
        }
    }
}