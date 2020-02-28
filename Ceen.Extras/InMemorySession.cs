using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ceen.Extras.InMemorySession
{
    /// <summary>
    /// Handler for registering a session on a request
    /// </summary>
    public class InMemorySessionHandler : IHttpModule
	{
		/// <summary>
		/// Gets or sets the name of the cookie with the token.
		/// </summary>
		public string CookieName { get; set; } = "ceen-session-token";

		/// <summary>
		/// Gets or sets the number of seconds a session is valid.
		/// </summary>
		public TimeSpan ExpirationSeconds { get; set; } = TimeSpan.FromMinutes(30);

		/// <summary>
		/// Gets or sets a value indicating if the session cookie gets the &quot;secure&quot; option set,
		/// meaning that it will only be sent over HTTPS
		/// </summary>
		public bool SessionCookieSecure { get; set; } = false;

		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			var sessiontoken = context.Request.Cookies[CookieName];
            if (string.IsNullOrWhiteSpace(sessiontoken) || !InMemorySession.SessionExists(sessiontoken)) 
            {
                sessiontoken = Guid.NewGuid().ToString();
                context.Response.AddCookie(CookieName, sessiontoken, secure: SessionCookieSecure, httponly: true);
            }

            context.Request.SessionID = sessiontoken;
            await InMemorySession.GetOrCreateSessionAsync(context, ExpirationSeconds, OnCreateAsync, OnExpireAsync);

			return false;
		}

        /// <summary>
        /// Virtual method for custom handling of newly created sessions
        /// </summary>
        /// <param name="request">The request that caused the session to be created</param>
        /// <param name="sessionID">The session ID used</param>
        /// <param name="values">The values in the session</param>
        /// <returns>An awaitable task</returns>
        protected virtual Task OnCreateAsync(IHttpRequest request, string sessionID, Dictionary<string, object> values)
            => Task.FromResult(true);

        /// <summary>
        /// Virtual method for custom handling of expired sessions
        /// </summary>
        /// <param name="id">The ID of the session that expires</param>
        /// <param name="values">The values in the session</param>
        /// <returns>An awaitable task</returns>
        protected virtual Task OnExpireAsync(string id, Dictionary<string, object> values)
            => Task.FromResult(true);
  	}

    /// <summary>
    /// Helper class for providing support for sessions that are only backed by memory,
    /// such that contents are lost on server restart
    /// </summary>
    public static class InMemorySession
    {
        /// <summary>
        /// Default delegate for callback after a session has expired
        /// </summary>
        /// <param name="sessionID">The session ID of the item</param>
        /// <param name="values">The values in the session</param>
        /// <returns>An awaitable task</returns>
        public delegate Task SessionExpireCallback(string sessionID, Dictionary<string, object> values);

        /// <summary>
        /// Default delegate for callback when a new session is created
        /// </summary>
        /// <param name="request">The request that caused the session to start</param>
        /// <param name="sessionID">The session ID</param>
        /// <param name="values">The values in the session</param>
        /// <returns>An awaitable task</returns>
        public delegate Task SessionCreateCallback(IHttpRequest request, string sessionID, Dictionary<string, object> values);

        /// <summary>
        /// The session item, keeping track of the session state
        /// </summary>
        private class SessionItem
        {
            /// <summary>
            /// The session ID
            /// </summary>
            public readonly string ID;
            /// <summary>
            /// The expiration time for this item
            /// </summary>
            public DateTime Expires;
            /// <summary>
            /// The duration for this item
            /// </summary>
            public readonly TimeSpan Duration;
            /// <summary>
            /// The session values
            /// </summary>
            public Dictionary<string, object> Values = new Dictionary<string, object>();
            /// <summary>
            /// The callback method to invoke on expiration
            /// </summary>
            public SessionExpireCallback ExpireCallback;

            /// <summary>
            /// Creates a new session item
            /// </summary>
            /// <param name="id">The session ID</param>
            /// <param name="duration">The session duration</param>
            public SessionItem(string id, TimeSpan duration) 
            { 
                ID = id; 
                Duration = duration;
            }
        }

        /// <summary>
        /// The lock that guards the lookup tables
        /// </summary>
        private static readonly object _lock = new object();
        
        /// <summary>
        /// The list of active sessions
        /// </summary>
        private static Dictionary<string, SessionItem> _sessions = new Dictionary<string, SessionItem>();

        /// <summary>
        /// The current expiration task
        /// </summary>
        private static Task ExpireTask;

        /// <summary>
        /// The time a session is kept alive without activity
        /// </summary>
        public static TimeSpan SessionDuration = TimeSpan.FromMinutes(15);

        /// <summary>
        /// A callback that is invoked when an item has expired
        /// </summary>
        public static SessionExpireCallback SessionExpired;

        /// <summary>
        /// A callback that is invoked when an item is created
        /// </summary>
        public static SessionCreateCallback SessionStarted;

        /// <summary>
        /// Checks if a session exists
        /// </summary>
        /// <param name="id">The session ID</param>
        /// <returns><c>true</c> if the session exists, <c>false</c> otherwise</returns>
        public static bool SessionExists(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            lock(_lock)
                return _sessions.ContainsKey(id);
        }

        /// <summary>
        /// Gets the session values for the request, or null if no session exists.
        /// This method assumes that the SessionID value of the requests has been set
        /// </summary>
        /// <param name="self">The context to get the session values for</param>
        /// <returns>The values for the session, or null</returns>
        public static Dictionary<string, object> CurrentSession(this IHttpContext self)
            => CurrentSession(self.Request.SessionID);
        /// <summary>
        /// Gets the session values for the request, or null if no session exists.
        /// This method assumes that the SessionID value of the requests has been set
        /// </summary>
        /// <param name="self">The request to get the session values for</param>
        /// <returns>The values for the session, or null</returns>
        public static Dictionary<string, object> CurrentSession(this IHttpRequest self)
            => CurrentSession(self.SessionID);

        /// <summary>
        /// Gets the session with the give ID, or null if no such session exists
        /// </summary>
        /// <param name="id">The ID of the session to find</param>
        /// <returns>The values for the session, or null</returns>
        public static Dictionary<string, object> CurrentSession(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            lock(_lock)
            {
                _sessions.TryGetValue(id, out var session);
                return session?.Values;
            }
        }

        /// <summary>
        /// Gets or creates a session for the current context
        /// </summary>
        /// <param name="self">The context to use</param>
        /// <returns>The values for the session</returns>
        public static Task<Dictionary<string, object>> GetOrCreateSessionAsync(this IHttpContext self, TimeSpan duration = default(TimeSpan), SessionCreateCallback createCallback = null, SessionExpireCallback expireCallback = null)
            => GetOrCreateSessionAsync(self.Request.SessionID, duration, self.Request, createCallback, expireCallback);

        /// <summary>
        /// Gets or creates a session for the current request
        /// </summary>
        /// <param name="self">The request to use</param>
        /// <returns>The values for the session</returns>
        public static Task<Dictionary<string, object>> GetOrCreateSessionAsync(this IHttpRequest self, TimeSpan duration = default(TimeSpan), SessionCreateCallback createCallback = null, SessionExpireCallback expireCallback = null)
            => GetOrCreateSessionAsync(self.SessionID, duration, self, createCallback, expireCallback);

        /// <summary>
        /// Gets or creates a session for the session ID
        /// </summary>
        /// <param name="self">The session ID to use</param>
        /// <param name="request">The request to report to the creation callback</param>
        /// <returns>The values for the session</returns>
        public static async Task<Dictionary<string, object>> GetOrCreateSessionAsync(string id, TimeSpan duration = default(TimeSpan), IHttpRequest request = null, SessionCreateCallback createCallback = null, SessionExpireCallback expireCallback = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));
            if (duration.Ticks < 0)
                throw new ArgumentOutOfRangeException(nameof(duration));

            SessionItem res;
            bool created = false;

            lock(_lock)
            {
                _sessions.TryGetValue(id, out res);
                if (res == null) 
                {
                    _sessions[id] = res = new SessionItem(id, duration.Ticks == 0 ? SessionDuration: duration) {
                        ExpireCallback = expireCallback
                    };
                    created = true;
                }

                res.Expires = DateTime.Now.Add(res.Duration);
            }

            if (created)
            {
                if (createCallback != null)
                    await createCallback(request, res.ID, res.Values);
                else if (SessionStarted != null)
                    await SessionStarted(request, res.ID, res.Values);
            }

            StartExpireTimer();

            return res.Values;
        }

        /// <summary>
        /// Force an expiration of the session
        /// </summary>
        /// <param name="self">The context to find the session in</param>
        /// <returns>An awaitable task</returns>
        public static Task ExpireSession(this IHttpContext self)
            => ExpireSession(self.Request.SessionID);

        /// <summary>
        /// Force an expiration of the session
        /// </summary>
        /// <param name="self">The request to find the session in</param>
        /// <returns>An awaitable task</returns>
        public static Task ExpireSession(this IHttpRequest self)
            => ExpireSession(self.SessionID);

        /// <summary>
        /// Force an expiration of the session
        /// </summary>
        /// <param name="id">The session ID to expire</param>
        /// <returns>An awaitable task</returns>
        public static async Task ExpireSession(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            SessionItem res;
            lock(_lock)
            {
                if (_sessions.TryGetValue(id, out res))
                    _sessions.Remove(id);                
            }

            StartExpireTimer();

            if (res != null)
            {
                if (res.ExpireCallback != null)
                    await res.ExpireCallback(res.ID, res.Values);
                else if (SessionExpired != null)
                    await SessionExpired(res.ID, res.Values);
            }
        }

        /// <summary>
        /// Starts the expire timer task, if not already running
        /// </summary>
        private static void StartExpireTimer()
        {
            lock(_lock)
            {
                // If there are no more tasks to run, do nothing
                if (_sessions.Count == 0)
                    return;

                // If we are already running an expiration task,
                // do not start a new one
                if (ExpireTask != null && !ExpireTask.IsCompleted)
                    return;

                ExpireTask = Task.Run(TimerRunnerAsync);
            }
        }

        /// <summary>
        /// The method performing periodic invocation of expiration
        /// </summary>
        /// <returns>An awaitable task</returns>
        private static async Task TimerRunnerAsync()
        {
            while(true) 
            {
                var next = await ExpireItemsAsync();
                var waittime = next - DateTime.Now;
                // Avoid repeated callbacks at the expense of expiration accuracy
                if (waittime < TimeSpan.FromSeconds(5))
                    waittime = TimeSpan.FromSeconds(5);

                // Do not wait more than a single period
                if (waittime > SessionDuration)
                    waittime = SessionDuration.Add(TimeSpan.FromSeconds(1));

                // Stop running if there are no more sessions
                if (_sessions.Count == 0)
                {
                    lock(_lock)
                    {
                        if (_sessions.Count == 0)
                        {
                            ExpireTask = null;
                            return;
                        }
                    }
                }

                await Task.Delay(waittime);
            }
        }

        /// <summary>
        /// Finds all sessions that needs to expire, and expires them
        /// </summary>
        /// <returns>The time the next item will expire</returns>
        private static async Task<DateTime> ExpireItemsAsync()
        {
            var expired = new Queue<SessionItem>();            
            var now = DateTime.Now;
            var nextExpire = now.AddDays(1);

            lock(_lock)
            {
                foreach(var s in _sessions)
                {
                    if (s.Value.Expires < now)
                        expired.Enqueue(s.Value);
                    else if (s.Value.Expires < nextExpire)
                        nextExpire = s.Value.Expires;
                }

                foreach(var s in expired)
                    _sessions.Remove(s.ID);
            }

            while(expired.Count > 0) 
            {
                var s = expired.Dequeue();
                try 
                {
                    if (s.ExpireCallback != null)
                        await s.ExpireCallback(s.ID, s.Values);
                    else if (SessionExpired != null)
                        await SessionExpired(s.ID, s.Values);
                } 
                catch (Exception ex) 
                {
                    Console.WriteLine("Error while expiring item: {0}", ex);
                }
            }

            return nextExpire;
        }
    }
}
