using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ceen.Database;

namespace Ceen.Extras
{
    /// <summary>
    /// Module that provides a persisted queue functionality
    /// </summary>
    public class QueueModule : DatabaseBackedModule, INamedModule, IWithShutdown
    {
        /// <summary>
        /// The types stored in the database
        /// </summary>
        protected override Type[] UsedTypes => new Type[] { 
            typeof(QueueEntry),
            typeof(QueueRunLog)
        };

        /// <summary>
        /// A shared lock to guard the named queue lookup
        /// </summary>
        private static object _lock = new object();

        /// <summary>
        /// The queue modules that are loaded
        /// </summary>
        private static Dictionary<string, QueueModule> _modules = new Dictionary<string, QueueModule>();

        /// <summary>
        /// Returns a named queue
        /// </summary>
        /// <param name="name">The name of the queue</param>
        /// <returns>The queue</returns>
        public static QueueModule GetQueue(string name) 
        { 
            lock (_lock) 
                return _modules[name];   
        }

        /// <summary>
        /// Checks if the request has the secure headers
        /// </summary>
        /// <param name="name">The name of the queue to use</param>
        /// <param name="request">The request to check</param>
        /// <returns><c>true</c> if the request has the secure headers</returns>
        public static bool IsSecureRequest(string name, IHttpRequest request)
        {
            var q = GetQueue(name);            
            return q != null && q.IsSecureRequest(request);
        }


        /// <summary>
        /// The unique name of the queue
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// A description of the queue
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The url prefix of the server for sending requests back to the server.
        /// Should not have a trailing slash
        /// </summary>
        public string SelfUrl { get; set; }

        /// <summary>
        /// The maximum rate of queue entries, in the form &quot;number/period&quot;
        /// where the period can be in weeks, days, hours, minutes, or seconds.
        /// </summary>
        public string Ratelimit { get; set; } = "1/s";

        /// <summary>
        /// The maximum number of concurrent requests to allow
        /// </summary>
        public int ConcurrentRequests { get; set; } = 1;

        /// <summary>
        /// The maximum number of retries to attempt
        /// </summary>
        /// <value></value>
        public int MaxRetries { get; set; } = 8;

        /// <summary>
        /// The time to wait before activating the runner
        /// </summary>
        public TimeSpan ProcessingStartupDelay { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The back-off period for retries, in the format
        /// &quot;initial;[lin(ear)|exp(onential)]increase;max&quot;
        /// Each setting is a timespan, and the increase needs either linear or exponential specifier
        /// </summary>
        public string RetryBackoff { get; set; } = "1s; exp 5s; 5m";

        /// <summary>
        /// The header with a secure known value
        /// </summary>
        public string SecureHeaderName { get; set; } = "X-Ceen-Secure";

        /// <summary>
        /// The header with a secure known value
        /// </summary>
        public string SecureHeaderValue { get; set; } = "";

        /// <summary>
        /// The maximum time a request is allowed to run
        /// </summary>
        public TimeSpan MaxProcessingTimePerRequest { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// The time tasks will linger before being removed from the database
        /// </summary>
        public TimeSpan OldTaskLingerTime { get; set; } = TimeSpan.FromHours(2);

        /// <summary>
        /// The window to check rate limits in
        /// </summary>
        private TimeSpan m_ratelimitWindow;
        /// <summary>
        /// The maximum nubmer in the rate limit window
        /// </summary>
        private int m_ratelimitcount;

        /// <summary>
        /// The initial retry back-off period
        /// </summary>
        private TimeSpan m_initialbackoff;
        /// <summary>
        /// The increased retry back-off period
        /// </summary>
        private TimeSpan m_additionalbackoff;
        /// <summary>
        /// The maximum retry back-off period
        /// </summary>
        private TimeSpan m_maximumbackoff;
        /// <summary>
        /// Using exponential or linear back-off
        /// </summary>
        private bool m_exponentialBackoff;

        /// <summary>
        /// A task completion source used to signal the runner
        /// </summary>
        private TaskCompletionSource<bool> m_invokeRunner = new TaskCompletionSource<bool>();

        /// <summary>
        /// The queue scheduler
        /// </summary>
        private Task m_runner;

        /// <summary>
        /// The rate limiter
        /// </summary>
        private RateLimit m_ratelimiter;

        /// <summary>
        /// The number of currently active tasks
        /// </summary>
        private int m_activeTasks;

        /// <summary>
        /// Flag to make sure we only wait on the first activation
        /// </summary>
        private bool m_isFirstActivation = true;

        /// <summary>
        /// A list of items that are force started
        /// </summary>
        private List<long> m_forcestarts = new List<long>();

        /// <summary>
        /// The cancellation token
        /// </summary>
        private readonly System.Threading.CancellationTokenSource m_cancelSource = new System.Threading.CancellationTokenSource();

        /// <summary>
        /// The current event count
        /// </summary>
        public int CurrentRate => m_ratelimiter.EventCount;

        /// <summary>
        /// Gets the number of currently running tasks
        /// </summary>
        public int CurrentRunning => m_activeTasks;

        /// <summary>
        /// Gets the status of the runner task
        /// </summary>
        public bool RunnerActive => m_runner != null && !m_runner.IsCompleted;     

        /// <summary>
        /// Gets the crash message, if any, from the runner
        /// </summary>
        public string RunnerCrashMessage => (m_runner != null && m_runner.IsFaulted) ? m_runner.Exception.ToString() : null; 

        /// <summary>
        /// Gets the current size of the queue
        /// </summary>
        /// <returns>The queue size</returns>
        public Task<long> GetQueueSizeAsync()
            => m_con.RunInTransactionAsync(db => db.SelectCount<QueueEntry>(x => x.QueueName == this.Name && x.Status == QueueEntryStatus.Waiting));
        

        /// <summary>
        /// Helper map to support various request encodings
        /// The key is the actual content-type, the values are the supported shorthands
        /// </summary>
        private static readonly Dictionary<string, string[]> m_encodingMimeTypes 
            = new Dictionary<string, string[]> {
                { "application/json", new string[] { "application/json", "json", "x-json", "application/x-json" } },
                { "text/plain", new string[] { "text/plain", "text" } },
                { "text/html", new string[] { "text/html", "html" } },
                { "application/octet-stream", new string[] { "application/octet-stream", "bytes", "binary", "octet-stream", "octets" } },
                { "multipart/form-data", new string[] { "multipart/form-data", "form-data", "form", "multipart" } },
                { "application/x-www-form-urlencoded", new string[] { "application/x-www-form-urlencoded", "url", "urlencoded" } },
            };

        /// <summary>
        /// Creates a new queue module instance
        /// </summary>
        /// <param name="name">The name of the module</param>
        public QueueModule(string name)
        {
            Name = name;
        }


        /// <summary>
        /// Configures the database and sets it up
        /// </summary>
        public override void AfterConfigure()
        {
            // If we share the database with another queue, share the connection as well
            lock (_lock)
                foreach (var m in _modules)
                    if (m.Value.ConnectionClass == this.ConnectionClass && m.Value.ConnectionString == this.ConnectionString)
                    {
                        m_con = m.Value.m_con;
                        break;
                    }

            // Initialize
            base.AfterConfigure();

            // The CLI will help us a little bit
            if (string.IsNullOrWhiteSpace(this.SelfUrl))
                this.SelfUrl = Environment.GetEnvironmentVariable("CEEN_SELF_HTTPS_URL");
            if (string.IsNullOrWhiteSpace(this.SelfUrl))
                this.SelfUrl = Environment.GetEnvironmentVariable("CEEN_SELF_HTTP_URL");

            if (string.IsNullOrWhiteSpace(this.SelfUrl))
                throw new Exception($"The QueueModule needs to know the server url to call back to, please set the {nameof(SelfUrl)} variable");

            // Remove trailing slashes to make it easier to concatenate strings
            this.SelfUrl = this.SelfUrl.Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(Name))
                throw new Exception("The name of the queue cannot be empty");
            if (string.IsNullOrWhiteSpace(SecureHeaderName))
                throw new Exception("The secure header name cannot be empty");
            if (string.IsNullOrWhiteSpace(Ratelimit))
                throw new Exception("The rate limit value cannot be empty");
            if (string.IsNullOrWhiteSpace(RetryBackoff))
                throw new Exception("The retry backoff value cannot be empty");
            if (MaxRetries <= 0)
                throw new Exception("Invalid max retry count");

            // Assign a random value
            if (string.IsNullOrWhiteSpace(SecureHeaderValue))
                SecureHeaderValue = Guid.NewGuid().ToString();

            var rl = Ratelimit.Split(new char[] { '/' }, 2);
            if (rl.Length != 2 || rl[0].Length < 1 || rl[1].Length < 1)
                throw new Exception("Unable to parse the ratelimit");
            if (rl[1][0] < '0' || rl[1][0] > '9')
                rl[1] = '1' + rl[1];

            m_ratelimitcount = int.Parse(rl[0]);
            m_ratelimitWindow = ParseUtil.ParseDuration(rl[1]);
            if (m_ratelimitWindow.Ticks <= 0 || m_ratelimitcount <= 0)
                throw new Exception("Invalid rate limit");

            var rb = RetryBackoff.Split(new char[] {';'}, 3);
            var re = new System.Text.RegularExpressions.Regex(@"(?<mode>[exp|lin|exponential|linear])\w+(?<rate>.+)");
            
            // Only have the exp/lin, compute start and limit
            if (rb.Length == 1)
            {
                var m = re.Match(rb[0]);
                if (!m.Success)
                    throw new Exception("Unable to parse the backoff");
                var duration = m.Groups["rate"].Value;
                var ps = ParseUtil.ParseDuration(duration);
                rb = new string[] { duration, rb[0], $"{ps.TotalSeconds * MaxRetries}s" };
            }
            else if (rb.Length == 2)
            {
                // First is exp/lin, last is limit, compute start
                var m = re.Match(rb[0]);
                if (m.Success)
                {
                    rb = new string[] { m.Groups["rate"].Value, rb[0], rb[1] };
                }
                // Second is exp/lin, first is start, compute last
                else
                {
                    m = re.Match(rb[1]);
                    if(!m.Success)
                        throw new Exception("Unable to parse the backoff");

                    var duration = m.Groups["rate"].Value;
                    var ps = ParseUtil.ParseDuration(duration);
                    rb = new string[] { rb[0], rb[1], $"{ps.TotalSeconds * MaxRetries}s" };

                }
            }

            var mx = re.Match(rb[1]);
            if (!mx.Success)
                throw new Exception("Unable to parse the backoff");

            m_initialbackoff = ParseUtil.ParseDuration(rb[0]);
            m_maximumbackoff = ParseUtil.ParseDuration(rb[2]);
            m_additionalbackoff = ParseUtil.ParseDuration(mx.Groups["rate"].Value);
            m_exponentialBackoff = mx.Groups["mode"].Value.StartsWith("exp", true, System.Globalization.CultureInfo.InvariantCulture);

            if (m_initialbackoff.Ticks < 0 || m_maximumbackoff.Ticks < 0 || m_additionalbackoff.Ticks < 0)
                throw new Exception("Invalid back-off values");

            m_ratelimiter = new RateLimit(m_ratelimitcount, m_ratelimitWindow);

            lock(_lock)
                _modules.Add(Name, this);

            // Activate the runner
            SignalRunner();
        }

        /// <summary>
        /// Checks if the request has the secure headers
        /// </summary>
        /// <param name="request">The request to check</param>
        /// <returns><c>true</c> if the request has the secure headers</returns>
        public bool IsSecureRequest(IHttpRequest request)
        {
            return request.Headers[SecureHeaderName] == SecureHeaderValue;
        }

        /// <summary>
        /// Adds a new job to the queue
        /// </summary>
        /// <param name="url">The url to invoke</param>
        /// <param name="method">The http method to use</param>
        /// <param name="data">The data to include</param>
        /// <param name="contentType">The content type to use</param>
        /// <param name="eta">An optional ETA for transmitting the job</param>
        /// <param name="headers">Any headers to send</param>
        /// <returns>An awaitable task</returns>
        public async Task SubmitJobAsync(string url, object data, string method = "POST", string contentType = "json", DateTime eta = default(DateTime), Dictionary<string, string> headers = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentNullException(nameof(url));
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentNullException(nameof(method));

            if (string.IsNullOrWhiteSpace(contentType) && headers != null)
                headers.TryGetValue("Content-Type", out contentType);

            if (eta < DateTime.Now)
                eta = DateTime.Now;

            string payload;
            if (data == null)
                payload = null;
            else
            {
                // Allow the short forms, and expand to the correct form
                string parsed = null;
                foreach (var k in m_encodingMimeTypes.Keys)
                    foreach (var n in m_encodingMimeTypes[k])
                        if (string.Equals(contentType, n, StringComparison.OrdinalIgnoreCase))
                        {
                            parsed = k;
                            break;
                        }

                if (parsed == null)
                    throw new ArgumentException($"Cannot send request with content type: {contentType}");

                contentType = parsed;
                switch (contentType)
                {
                    case "multipart/form-data":
                        payload = Newtonsoft.Json.JsonConvert.SerializeObject(
                            Ceen.QueryStringSerializer
                                .GetLookup(data.GetType())
                                .ToDictionary(x => x.Key, x => {
                                    if (x.Value is System.Reflection.PropertyInfo pi)
                                        return pi.GetValue(data)?.ToString();
                                    else if (x.Value is System.Reflection.FieldInfo fi)
                                        return fi.GetValue(data)?.ToString();
                                    else
                                        return null;
                                })
                        );
                        break;
                    case "application/json":
                        payload = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                        break;

                    case "application/x-www-form-urlencoded":
                        payload = Ceen.QueryStringSerializer.Serialize(data);
                        break;

                    case "text/plan":
                    case "text/html":
                        payload = data.ToString();
                        break;

                    case "application/octet-stream":
                        if (!(data is byte[]))
                            throw new ArgumentException("Can only send a byte[] if the type is octet-stream");
                        payload = Convert.ToBase64String(data as byte[]);
                        break;
                            
                    default:
                        throw new ArgumentException($"Cannot send request with content type: {contentType}");

                }
            }

            // Store the entry
            await this.RunInTransactionAsync(db => {
                db.InsertItem(new QueueEntry() {
                    QueueName = Name,
                    Method = method,
                    Url = url,
                    Payload = payload,
                    ContentType = contentType,
                    Headers = headers == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(headers),
                    ETA = eta,
                    NextTry = new DateTime(Math.Max(DateTime.Now.Ticks, eta.Ticks)),
                    Retries = 0,
                    Status = QueueEntryStatus.Waiting
                });
            });

            // Signal the runner that we have added stuff to the queue
            SignalRunner();
        }

        /// <summary>
        /// Forces the supplied jobs to run immediately if they are not already running
        /// </summary>
        /// <param name="ids">The list of jobs IDs to run</param>
        public void ForceRun(params long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return;

            lock (_lock)
                m_forcestarts.AddRange(ids);
            SignalRunner();
        }

        /// <summary>
        /// Signals the runner that something has changed
        /// </summary>
        private void SignalRunner()
        {
            m_invokeRunner.TrySetResult(true);
            if (m_runner == null || m_runner.IsCompleted)
                lock (_lock)
                    if (m_runner == null || m_runner.IsCompleted)
                        m_runner = Task.Run(SchedulerRunAsync);                
        }

        /// <summary>
        /// Attempts to cancel all tasks
        /// </summary>
        /// <returns>The current task</returns>
        Task IWithShutdown.ShutdownAsync()
        {
            m_cancelSource.Cancel();
            return m_runner;
        }

        /// <summary>
        /// Runs the scheduler
        /// </summary>
        /// <returns>An awaitable task</returns>
        private async Task SchedulerRunAsync()
        {
            // Wait for the first startup
            if (m_isFirstActivation)
            {
                m_isFirstActivation = false;
                await Task.Delay(ProcessingStartupDelay);
            }

            // The running tasks
            var activeTasks = new List<KeyValuePair<long, Task>>();

            // The last time a task was removed
            var removalTime = new DateTime(0);

            // Set up cancellation
            var cancelTask = new TaskCompletionSource<bool>();
            m_cancelSource.Token.Register(() => cancelTask.TrySetCanceled());

            while(true)
            {
                // Handle completed/failed tasks
                for(var i = activeTasks.Count - 1; i >= 0; i--)
                {
                    var at = activeTasks[i];
                    if (at.Value.IsCompleted)
                    {
                        activeTasks.RemoveAt(i);
                        if (removalTime.Ticks == 0)
                            removalTime = DateTime.Now + OldTaskLingerTime;                            
                        await this.RunInTransactionAsync(db =>
                        {
                            var el = db.SelectItemById<QueueEntry>(at.Key);
                            // If the request failed, try to reschedule it
                            if (at.Value.IsCanceled || at.Value.IsFaulted)
                            {
                                el.Retries++;
                                if (el.Retries > MaxRetries)
                                {
                                    el.Status = QueueEntryStatus.Failed;
                                }
                                else
                                {
                                    el.NextTry = ComputeNextTry(DateTime.Now, el.Retries);
                                    el.Status = QueueEntryStatus.Waiting;
                                }
                            }
                            // All good, just mark it as done
                            else
                            {
                                el.Status = QueueEntryStatus.Completed;
                            }

                            db.UpdateItem(el);
                        });
                    }
                }

                if (removalTime.Ticks > 0 && removalTime < DateTime.Now)
                {
                    removalTime = new DateTime(0);
                    var cutoff = DateTime.Now - OldTaskLingerTime;
                    await this.RunInTransactionAsync(db => {
                        // Remove old tasks
                        db.Query<QueueEntry>()
                            .Delete()
                            .Where(x => x.Status == QueueEntryStatus.Completed || x.Status == QueueEntryStatus.Failed)
                            .Where(x => x.LastTried > cutoff);

                        // Remove any run tasks no longer associated with a task
                        db.Delete<QueueRunLog>($"{db.QuotedColumnName<QueueRunLog>(nameof(QueueRunLog.ID))} NOT IN (SELECT {db.QuotedColumnName<QueueEntry>(nameof(QueueEntry.ID))} FROM {db.QuotedTableName<QueueEntry>()})");

                        // Get the earliest next cleanup time
                        var oldest = db.SelectSingle(
                            db.Query<QueueEntry>()
                            .Select()
                            .Where(x => x.Status == QueueEntryStatus.Completed || x.Status == QueueEntryStatus.Failed)
                            .OrderBy(x => x.LastTried)
                            .Limit(1)
                        );

                        if (oldest != null)
                            removalTime = oldest.LastTried;
                    });
                }

                // If we have forced entries, run those first
                if (m_forcestarts.Count > 0)
                {
                    List<long> ids = null;

                    // Get the forced list, if it has any entries
                    lock(_lock)
                        if (m_forcestarts.Count > 0) 
                            ids = System.Threading.Interlocked.Exchange(ref m_forcestarts, new List<long>());

                    if (ids != null)
                        ids = ids
                            // Make sure we do not run the tasks multiple times
                            .Where(x => !activeTasks.Any(y => y.Key == x))
                            .ToList();

                    if (ids.Count > 0)
                    {
                        var forced = await this.RunInTransactionAsync(db =>
                            db.Select(
                                db.Query<QueueEntry>()
                                .Select()
                                .Where(x =>
                                    x.QueueName == Name
                                    &&
                                    x.Status != QueueEntryStatus.Completed
                                )
                                .Where(QueryUtil.In(
                                    QueryUtil.Property(
                                        nameof(QueueEntry.ID)
                                    ),
                                    ids.Cast<object>())
                                )
                            ).ToList()
                        );

                        // Start all forced tasks without obeying limits
                        foreach (var item in forced)
                        {
                            activeTasks.Add(
                                new KeyValuePair<long, Task>(
                                    item.ID,
                                    Task.Run(() => RunTaskAsync(item))
                                )
                            );
                            // Make sure the normal schedule also counts
                            // the manually activated events
                            m_ratelimiter.AddEvent(1);
                        }
                    }
                }


                // Get pending queue entries, ordered by NextTry
                var pending = await this.RunInTransactionAsync(db =>
                    db.Select(
                        db.Query<QueueEntry>()
                            .Select()
                            .Where(x =>
                                x.QueueName == Name
                                &&
                                x.Status == QueueEntryStatus.Waiting
                                &&
                                x.NextTry <= DateTime.Now
                            )
                            .OrderBy(x => x.NextTry)
                            .Limit(activeTasks.Count - ConcurrentRequests + 1)
                    ).ToList()
                );

                // Keep starting tasks
                while(pending.Count > 0 && activeTasks.Count < ConcurrentRequests)
                {
                    // If there are too many events, stop adding
                    if (m_ratelimiter.EventCount > m_ratelimitcount)
                        break;

                    var t = pending.First();
                    if (t.NextTry > DateTime.Now)
                        break;

                    pending.RemoveAt(0);

                    activeTasks.Add(
                        new KeyValuePair<long, Task>(
                            t.ID, 
                            Task.Run(() => RunTaskAsync(t))
                        )
                    );

                    m_ratelimiter.AddEvent(1);
                }

                m_activeTasks = activeTasks.Count;

                var delay = 
                    pending.Count == 0 
                    ? TimeSpan.FromSeconds(30) 
                    : (DateTime.Now - pending.First().NextTry + TimeSpan.FromMilliseconds(100));

                var ratelimit_delay = m_ratelimiter.WaitTime;
                if (ratelimit_delay.Ticks > 0)
                    delay = TimeSpan.FromTicks(Math.Min(delay.Ticks, ratelimit_delay.Ticks));

                if (await Task.WhenAny(m_invokeRunner.Task, Task.Delay(delay), cancelTask.Task) == m_invokeRunner.Task)
                    System.Threading.Interlocked.Exchange(ref m_invokeRunner, new TaskCompletionSource<bool>());

                // Stop if we are shutting down
                if(m_cancelSource.IsCancellationRequested)
                { 
                    // If we have no runners, just quit now
                    if(activeTasks.Count == 0)
                        return;

                    // If we have runners, check on them, but do not spin
                    await Task.Delay(200);
                }
            }       
        }

        /// <summary>
        /// Runs the queued task
        /// </summary>
        /// <param name="e">The task to run</param>
        /// <returns>An awaitable task</returns>
        private async Task RunTaskAsync(QueueEntry e)
        {
            // Start the task
            var t = await this.RunInTransactionAsync(db => {
                // Mark the task itself as running
                var now = DateTime.Now;
                e.LastTried = now;
                e.Status = QueueEntryStatus.Running;
                db.Update<QueueEntry>(new { e.LastTried, e.Status }, x => x.ID == e.ID);

                // Create a runner entry
                return db.InsertItem<QueueRunLog>(
                    new QueueRunLog() {
                        QueueName = Name,
                        TaskID = e.ID,
                        Method = e.Method,
                        Url = e.Url,
                        ContentType = e.ContentType,
                        Started = now
                    }
                );
            });

            using (var c = new System.Net.Http.HttpClient())
            using (var rq = new System.Net.Http.HttpRequestMessage())
            try
            {
                rq.Method = new System.Net.Http.HttpMethod(e.Method);
                var isSelfRequest = e.Url.StartsWith("/");
                var targeturl = e.Url;

                if (isSelfRequest)
                    targeturl = this.SelfUrl + targeturl;

                rq.RequestUri = new Uri(targeturl);
                if (!string.IsNullOrWhiteSpace(e.Headers))
                {
                    var headers = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(e.Headers);
                    foreach (var h in headers)
                    {
                        rq.Headers.Remove(h.Key);
                        rq.Headers.Add(h.Key, h.Value);   
                    }
                }

                if (isSelfRequest)
                {
                    rq.Headers.Remove(SecureHeaderName);
                    rq.Headers.Add(SecureHeaderName, SecureHeaderValue);
                }

                switch (e.ContentType)
                {
                    case "multipart/form-data":
                        var mp = new System.Net.Http.MultipartFormDataContent();
                        foreach (var item in Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(e.Payload))
                            mp.Add(new System.Net.Http.StringContent(item.Value), item.Key);
                        rq.Content = mp;
                        break;

                    case "application/x-www-form-urlencoded":
                    case "application/json":
                    case "text/plan":
                    case "text/html":
                        rq.Content = new System.Net.Http.StringContent(e.Payload, System.Text.Encoding.UTF8, e.ContentType);
                        break;

                    case "application/octet-stream":
                        rq.Content = new System.Net.Http.ByteArrayContent(Convert.FromBase64String(e.Payload));
                        break;

                    default:
                        throw new ArgumentException($"Cannot send request with content type: {e.ContentType}");

                }

                using(var resp = await c.SendAsync(rq, m_cancelSource.Token))
                {
                    t.Finished = DateTime.Now;
                    t.StatusCode = (int)resp.StatusCode;
                    t.StatusMessage = resp.ReasonPhrase;
                    t.Result = await resp.Content.ReadAsStringAsync();
                    resp.EnsureSuccessStatusCode();
                    await this.RunInTransactionAsync(db => db.UpdateItem(t));
                }
            }
            catch(Exception ex)
            {
                t.Result = string.Join(
                    Environment.NewLine, 
                    new string[] { 
                        t.Result, 
                        "Exception: " + ex.ToString() 
                    }.Where(x => !string.IsNullOrWhiteSpace(x))
                );

                throw;
            }
            finally
            {
                    t.Finished = DateTime.Now;
                    await this.RunInTransactionAsync(db => db.UpdateItem(t));

                    // Make sure the runner picks up on out results
                    // But make sure the runner task has finished before we signal
                    Task.Delay(TimeSpan.FromMilliseconds(500)).ContinueWith(_ => SignalRunner());
            }
        }

        /// <summary>
        /// Computes the next time to try the request
        /// </summary>
        /// <param name="previous">The time when the request last failed</param>
        /// <param name="retryCount">The retry count to compute the time for</param>
        /// <returns>The next tried count</returns>
        private DateTime ComputeNextTry(DateTime previous, int retryCount)
        {
            return previous + new TimeSpan((long)
                Math.Min(
                    m_maximumbackoff.Ticks,
                    m_exponentialBackoff
                    ? Math.Pow(m_additionalbackoff.Ticks, retryCount)
                    : m_additionalbackoff.Ticks
                )
            );
        } 

        /// <summary>
        /// Helper for typed access to a queue
        /// </summary>
        /// <typeparam name="T">The type to submit and retrieve from the queue</typeparam>
        public class QueueHelper<T>
        {
            /// <summary>
            /// The parent module
            /// </summary>
            private readonly QueueModule m_parent;

            /// <summary>
            /// Constructs a new queue helper
            /// </summary>
            /// <param name="queue">The queue to use module</param>
            public QueueHelper(string queue)
                : this(QueueModule.GetQueue(queue))
            {
            }

            /// <summary>
            /// Constructs a new queue helper
            /// </summary>
            /// <param name="parent">The parent module</param>
            public QueueHelper(QueueModule parent)
            {
                m_parent = parent ?? throw new ArgumentNullException(nameof(parent));;
            }

            /// <summary>
            /// Adds a new job to the queue
            /// </summary>
            /// <param name="url">The url to invoke</param>
            /// <param name="method">The http method to use</param>
            /// <param name="data">The data to include</param>
            /// <param name="contentType">The content type to use</param>
            /// <param name="eta">An optional ETA for transmitting the job</param>
            /// <param name="headers">Any headers to send</param>
            /// <returns>An awaitable task</returns>
            public Task SubmitJobAsync(string url, T data, string method = "POST", string contentType = "json", DateTime eta = default(DateTime), Dictionary<string, string> headers = null)
            {
                return m_parent.SubmitJobAsync(url, data, method, contentType, eta, headers);
            }
        }

        /// <summary>
        /// Helper for typed access to a queue, only submitting to the same url
        /// </summary>
        /// <typeparam name="T">The type to submit and retrieve from the queue</typeparam>
        public class QueueHelperFixedUrl<T>
        {
            /// <summary>
            /// The parent module
            /// </summary>
            private readonly QueueModule m_parent;

            /// <summary>
            /// The fixed url to use
            /// </summary>
            private readonly string m_url;

            /// <summary>
            /// Constructs a new queue helper
            /// </summary>
            /// <param name="queue">The queue to use module</param>
            /// <param name="url">The url</param>
            public QueueHelperFixedUrl(string queue, string url)
                : this(QueueModule.GetQueue(queue), url)
            {
            }

            /// <summary>
            /// Constructs a new queue helper
            /// </summary>
            /// <param name="parent">The parent module</param>
            /// <param name="url">The url</param>
            public QueueHelperFixedUrl(QueueModule parent, string url)
            {
                m_parent = parent ?? throw new ArgumentNullException(nameof(parent));
                m_url = url ?? throw new ArgumentNullException(nameof(parent));
            }

            /// <summary>
            /// Adds a new job to the queue
            /// </summary>
            /// <param name="method">The http method to use</param>
            /// <param name="data">The data to include</param>
            /// <param name="contentType">The content type to use</param>
            /// <param name="eta">An optional ETA for transmitting the job</param>
            /// <param name="headers">Any headers to send</param>
            /// <returns>An awaitable task</returns>
            public Task SubmitJobAsync(T data, string method = "POST", string contentType = "json", DateTime eta = default(DateTime), Dictionary<string, string> headers = null)
            {
                return m_parent.SubmitJobAsync(m_url, data, method, contentType, eta, headers);
            }
        }

        /// <summary>
        /// The states a queue entry can be in
        /// </summary>
        public enum QueueEntryStatus
        {
            /// <summary>The entry is waiting for activation</summary>
            Waiting,
            /// <summary>The entry is currently executing</summary>
            Running,
            /// <summary>The entry is completed</summary>
            Completed,
            /// <summary>The entry has failed</summary>
            Failed,
        }

        /// <summary>
        /// Represents a single queued request
        /// </summary>
        public class QueueEntry
        {
            /// <summary>
            /// The entry ID
            /// </summary>
            [PrimaryKey]
            public long ID;

            /// <summary>
            /// The name of the queue this job belongs to
            /// </summary>
            public string QueueName;

            /// <summary>
            /// The HTTP method to use
            /// </summary>
            public string Method;

            /// <summary>
            /// The url to invoke
            /// </summary>
            public string Url;

            /// <summary>
            /// The payload, either in URL or body format
            /// </summary>
            public string Payload;

            /// <summary>
            /// The headers as a JSON serialized string
            /// </summary>
            public string Headers;

            /// <summary>
            /// The content type the data should be encoded as
            /// </summary>
            public string ContentType;

            /// <summary>
            /// The desired execution time 
            /// </summary>
            public DateTime ETA;

            /// <summary>
            /// The time this item is scheduled next
            /// </summary>
            public DateTime NextTry;

            /// <summary>
            /// The time this entry was last tried
            /// </summary>
            public DateTime LastTried;

            /// <summary>
            /// The number of retries so far
            /// </summary>
            public int Retries;

            /// <summary>
            /// The status of the entry
            /// </summary>
            public QueueEntryStatus Status;
        }

        public class QueueRunLog
        {
            /// <summary>
            /// The entry ID
            /// </summary>
            [PrimaryKey]
            public long ID;

            /// <summary>
            /// The ID of the QueueEntry this run is for
            /// </summary>
            public long TaskID;

            /// <summary>
            /// The name of the queue this job belongs to
            /// </summary>
            public string QueueName;

            /// <summary>
            /// The HTTP method to use
            /// </summary>
            public string Method;

            /// <summary>
            /// The url to invoke
            /// </summary>
            public string Url;

            /// <summary>
            /// The content type the data should be encoded as
            /// </summary>
            public string ContentType;

            /// <summary>
            /// The time this entry was last tried
            /// </summary>
            public DateTime Started;

            /// <summary>
            /// The time this entry completed
            /// </summary>
            public DateTime Finished;

            /// <summary>
            /// The result data from the run
            /// </summary>
            public string Result;

            /// <summary>
            /// The http response status code
            /// </summary>
            public int StatusCode;

            /// <summary>
            /// The http response status message
            /// </summary>
            public string StatusMessage;
        }

    }
}
