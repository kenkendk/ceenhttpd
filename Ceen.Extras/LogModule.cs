using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ceen;
using Ceen.Database;

namespace Ceen.Extras
{
    /// <summary>
    /// Handles logging to a separate database.
    /// Queues requests and flushes them in batches out-of-band,
    /// to avoid clogging/interfering with normal execution
    /// </summary>
    public class LogModule : DatabaseBackedModule, IMessageLogger, IStartLogger, ILoggerWithSetup, INamedModule
    {
        /// <summary>
        /// Represents a logged HTTP request
        /// </summary>
        public class HttpLogEntry
        {
            /// <summary>
            /// The log entry ID
            /// </summary>
            [PrimaryKey]
            public string ID;

            /// <summary>
            /// The connection this request belongs to
            /// </summary>
            public string ConnectionID;

            /// <summary>
            /// The time the request was recived
            /// </summary>
            public DateTime Started;
            /// <summary>
            /// The time the request was processed
            /// </summary>
            public DateTime Finished;

            /// <summary>
            /// The response status code
            /// </summary>
            public int ResponseStatusCode;
            /// <summary>
            /// The response status message
            /// </summary>
            public string ResponseStatusMessage;
            /// <summary>
            /// The response body size
            /// </summary>
            public long ResponseSize;

            /// <summary>
            /// The request URL
            /// </summary>
            public string RequestUrl;
            /// <summary>
            /// The request query string
            /// </summary>
            public string RequestQueryString;
            /// <summary>
            /// The request verb
            /// </summary>
            public string RequestVerb;
            /// <summary>
            /// The request body size
            /// </summary>
            public long RequestSize;
            /// <summary>
            /// The request user agent string
            /// </summary>
            public string UserAgent;
            /// <summary>
            /// The userID attached to the request, if any
            /// </summary>
            public string UserID;
            /// <summary>
            /// The session ID attached to the request, if any
            /// </summary>
            public string SessionID;
        }

        /// <summary>
        /// Represents a log message for a request
        /// </summary>
        public class HttpLogEntryLine
        {
            /// <summary>
            /// The primary key for the line
            /// </summary>
            [PrimaryKey]
            public int ID;

            /// <summary>
            /// The log entry this line belongs to
            /// </summary>
            public string ParentID;
            /// <summary>
            /// The time this log message was recorded
            /// </summary>
            public DateTime When;
            /// <summary>
            /// The log level associated with this message
            /// </summary>
            public Ceen.LogLevel LogLevel;
            /// <summary>
            /// The log message
            /// </summary>
            public string Data;
            /// <summary>
            /// Any exception data
            /// </summary>
            public string Exception;
        }

        /// <summary>
        /// Gets or sets the name of the module
        /// </summary>
        public string Name { get; set; } = "LogModule";

        /// <summary>
        /// The time between flushing the log data
        /// </summary>
        public TimeSpan FLUSH_PERIOD { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The maximum number of days to keep logs around
        /// </summary>
        private TimeSpan MAX_LOG_AGE { get; set; } = TimeSpan.FromDays(90);

        /// <summary>
        /// The last time a cleanup was performed
        /// </summary>
        private DateTime m_lastCleanup = DateTime.Now;

        /// <summary>
        /// A list of requests that are started and needs to be created in the log table
        /// </summary>
        private Dictionary<string, HttpLogEntry> m_startedEntries = new Dictionary<string, HttpLogEntry>();

        /// <summary>
        /// The lock guarding access to the lists with entries and lines
        /// </summary>
        /// <returns></returns>
        private readonly AsyncLock m_lock = new AsyncLock();

        /// <summary>
        /// A list of entries that are completed and needs to be updated in the log table
        /// </summary>
        private List<HttpLogEntry> m_entries = new List<HttpLogEntry>();

        /// <summary>
        /// A list of log message lines that needs to be written in the log table
        /// </summary>
        private List<HttpLogEntryLine> m_lines = new List<HttpLogEntryLine>();

        /// <summary>
        /// The runner task
        /// </summary>
        private Task m_runner = null;

        /// <summary>
        /// Creates a new instance of the log handler
        /// </summary>
        public LogModule()
            : base()
        {
            ConnectionString = "logdata.sqlite";
        }

        /// <summary>
        /// The types used in the logging module
        /// </summary>
        /// <value></value>
        protected override Type[] UsedTypes => new Type[] {
            typeof(HttpLogEntry),
            typeof(HttpLogEntryLine)
        };

        /// <summary>
        /// Enqueues a task that will flush the log data after the flush period
        /// </summary>
        /// <returns>An awaitable task</returns>
        private void EnqueueDatabaseFlusher()
        {
            if (m_runner == null || m_runner.IsCompleted)
                m_runner = Task.Run(async () =>
                {
                    await Task.Delay(FLUSH_PERIOD);
                    await CommitAsync();
                });
        }

        /// <summary>
        /// Logs a message
        /// </summary>
        /// <param name="context">The execution context</param>
        /// <param name="ex">An optional exception to log</param>
        /// <param name="loglevel">The log level to use</param>
        /// <param name="message">The message, if any</param>
        /// <param name="when">The time when the message was logged</param>
        /// <returns>An awaitable task</returns>
        public async Task LogMessageAsync(IHttpContext context, Exception ex, LogLevel loglevel, string message, DateTime when)
        {
            using (await m_lock.LockAsync())
            {
                m_lines.Add(new HttpLogEntryLine()
                {
                    ParentID = context.Request.LogRequestID,
                    When = when.ToUniversalTime(),
                    LogLevel = loglevel,
                    Data = message,
                    Exception = ex?.ToString()
                });
                EnqueueDatabaseFlusher();
            }

        }

        /// <summary>
        /// Logs a completed processing task
        /// </summary>
        /// <param name="context">The execution context</param>
        /// <param name="ex">An optional exception result</param>
        /// <param name="started">The time the request started</param>
        /// <param name="duration">The duration of the request</param>
        /// <returns>An awaitable task</returns>
        public async Task LogRequestCompletedAsync(IHttpContext context, Exception ex, DateTime started, TimeSpan duration)
        {
            var e = new HttpLogEntry()
            {
                ID = context.Request.LogRequestID,
                ConnectionID = context.Request.LogConnectionID,
                Started = started.ToUniversalTime(),
                Finished = started.ToUniversalTime() + duration,
                ResponseStatusCode = (int)context.Response.StatusCode,
                ResponseStatusMessage = context.Response.StatusMessage,
                ResponseSize = context.Response.ContentLength,
                RequestUrl = context.Request.Path,
                RequestQueryString = context.Request.RawQueryString,
                RequestVerb = context.Request.Method,
                RequestSize = context.Request.ContentLength,
                UserAgent = context.Request.Headers["User-Agent"],
                UserID = context.Request.UserID,
                SessionID = context.Request.SessionID
            };

            using (await m_lock.LockAsync())
            {
                if (m_startedEntries.ContainsKey(e.ID))
                    m_startedEntries[e.ID] = e;
                else
                    m_entries.Add(e);

                EnqueueDatabaseFlusher();
            }
        }

        /// <summary>
        /// Logs a started request
        /// </summary>
        /// <param name="request">The request that starts</param>
        /// <returns>An awaitable task</returns>
        public async Task LogRequestStartedAsync(IHttpRequest request)
        {
            var e = new HttpLogEntry()
            {
                ID = request.LogRequestID,
                ConnectionID = request.LogConnectionID,
                Started = request.RequestProcessingStarted.ToUniversalTime(),
                ResponseStatusCode = 0,
                ResponseStatusMessage = "-",
                ResponseSize = -1,
                RequestUrl = request.Path,
                RequestQueryString = request.RawQueryString,
                RequestVerb = request.Method,
                RequestSize = request.ContentLength,
                UserAgent = request.Headers["User-Agent"]
            };

            using (await m_lock.LockAsync())
            {
                m_startedEntries[request.LogRequestID] = e;
                EnqueueDatabaseFlusher();
            }
        }

        /// <summary>
        /// Commits the current logged data to the database
        /// </summary>
        /// <returns>An awaitable task</returns>
        private async Task CommitAsync()
        {
            // Copy the reference
            var e = m_entries;
            var l = m_lines;
            var s = m_startedEntries;

            // Then detach the lists
            using (await m_lock.LockAsync())
            {
                m_startedEntries = new Dictionary<string, HttpLogEntry>();
                m_entries = new List<HttpLogEntry>();
                m_lines = new List<HttpLogEntryLine>();
                m_runner = null;
            }

            // This detaching allows us to take and hold the database lock
            // an not delay new log entries while we keep the lock
            try
            {
                await m_con.RunInTransactionAsync(db => {
                    foreach (var n in s)
                        try { db.InsertItem(n.Value); }
                        catch (Exception ex) { Console.WriteLine(ex); }

                    foreach (var n in e)
                        try { db.UpdateItem(n); }
                        catch (Exception ex) { Console.WriteLine(ex); }

                    foreach (var n in l)
                        try { db.InsertItem(n); }
                        catch (Exception ex) { Console.WriteLine(ex); }

                    if ((DateTime.Now - m_lastCleanup).Ticks > MAX_LOG_AGE.Ticks / 4)
                    {
                        m_lastCleanup = DateTime.Now;

                        try
                        {
                            // Remove old lines
                            db.Delete<HttpLogEntry>(
                                x => x.Finished < DateTime.UtcNow - MAX_LOG_AGE
                            );

                            // Remove any line not attached to an entry
                            db.Delete<HttpLogEntryLine>(
                                $"WHERE {db.QuotedColumnName<HttpLogEntryLine>(nameof(HttpLogEntryLine.ParentID))} " +
                                $"NOT IN (SELECT {db.QuotedColumnName<HttpLogEntry>(nameof(HttpLogEntry.ID))} FROM {db.QuotedTableName<HttpLogEntry>()})"
                            );
                        }
                        catch (Exception ex) { Console.WriteLine(ex); }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}