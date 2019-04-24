using System;
using System.Text;
using System.Threading.Tasks;
using CoCoL;

namespace PerfTester
{
    /// <summary>
    /// The curl request worker
    /// </summary>
    public class CurlWorker : RunnerBase
    {
        /// <summary>
        /// The curl commandline to execute
        /// </summary>
        private readonly string m_commandline;

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
        public CurlWorker(IWorkerOptions options, IReadChannel<bool> reqchan, IWriteChannel<RequestResult> respchan, string response)
            //: base(options, maximumrequests, response)
            : base(options, reqchan, respchan, response)
        {
            var sb = new StringBuilder();
            sb.Append($"--silent --show-error --request {EscapeAndQuoteArgument(options.Verb)} {EscapeAndQuoteArgument(options.RequestUrl)}");
            foreach (var h in options.Headers)
                sb.Append($" --header {EscapeAndQuoteArgument(h)}");
            
            if (!string.IsNullOrEmpty(options.Body))
                sb.Append($" --data {EscapeAndQuoteArgument(options.Body)}");

            m_commandline = sb.ToString();
            m_initialized.TrySetResult(true);
        }

        /// <summary>
        /// Escapes a commandline argument
        /// </summary>
        /// <param name="s">The string to escape</param>
        /// <returns>The escaped</returns>
        private static string EscapeArgument(string s)
        {
            return s.Replace("\"", "\\\"");
        }

        /// <summary>
        /// Escapes a commandline argument
        /// </summary>
        /// <param name="s">The string to escape</param>
        /// <returns>The escaped</returns>
        private static string EscapeAndQuoteArgument(string s)
        {
            return "\"" + EscapeArgument(s) + "\"";
        }

        /// <inheritdoc />
        protected override async Task<string> PeformRequestAsync()
        {
            await m_initialized.Task;
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = "curl",
                Arguments = m_commandline,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            // Start reading
            var tstdout = p.StandardOutput.ReadToEndAsync();
            var tstderr = p.StandardError.ReadToEndAsync();
            var tres = new TaskCompletionSource<bool>();

            var trun = Task.Run(() => p.WaitForExit());
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(m_options.RequestTimeoutSeconds)), trun);

            if (!p.HasExited)
            {
                p.Kill();
                throw new TimeoutException("Timeout while waiting for response");
            }

            var stderr = await tstderr;
            if (!string.IsNullOrWhiteSpace(stderr))
                throw new Exception($"Got error message from curl: {stderr}");
            if (p.ExitCode != 0)
                throw new Exception($"Got error message code curl: {p.ExitCode}");

            var res = await tstdout;
            return res;
        }
    }
}