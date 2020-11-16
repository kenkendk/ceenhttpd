using System;
using System.Collections.Generic;
using System.Linq;

namespace Ceen.Extras
{
    /// <summary>
    /// Helper for computing a rate limit
    /// </summary>
    public class RateLimit
    {
        /// <summary>
        /// The recorded events
        /// </summary>
        private readonly List<KeyValuePair<DateTime, int>> m_rates = new List<KeyValuePair<DateTime, int>>();

        /// <summary>
        /// The lock guarding the rate list
        /// </summary>
        private readonly object m_lock = new object();

        /// <summary>
        /// The maximum rate to allow
        /// </summary>
        private readonly int m_maxrate;

        /// <summary>
        /// The sample duration
        /// </summary>
        private readonly TimeSpan m_duration;

        /// <summary>
        /// Creates a new rate-limit helper
        /// </summary>
        /// <param name="maxrate">The maximum number of events in the duration</param>
        /// <param name="duration">The duration to keep track of</param>
        public RateLimit(int maxrate, TimeSpan duration)
        {
            m_maxrate = maxrate;
            m_duration = duration;
            if (m_maxrate <= 0)
                throw new ArgumentException("Cannot set a negative max", nameof(maxrate));
            if (m_duration.Ticks <= 0)
                throw new ArgumentException("Cannot set a negative duration", nameof(duration));
        }

        /// <summary>
        /// Registers a new event
        /// </summary>
        /// <param name="count">The count this event contributes</param>
        public void AddEvent(int count)
        {
            if (count <= 0)
                throw new ArgumentException("Cannot record negative events", nameof(count));

            lock(m_lock)
            {
                RemoveOldEvents();
                m_rates.Add(new KeyValuePair<DateTime, int>(DateTime.Now, count));
            }
        }

        /// <summary>
        /// Removes events outside the duration, must hold the lock before callong
        /// </summary>
        private void RemoveOldEvents()
        {
            var end = DateTime.Now - m_duration;
            while(m_rates.Count > 0 && m_rates[0].Key < end)
                m_rates.RemoveAt(0);
        }

        /// <summary>
        /// Gets the current event count
        /// </summary>
        /// <returns>The current event count</returns>
        public int EventCount        
        {
            get 
            {
                lock(m_lock)
                {
                    RemoveOldEvents();
                    return m_rates.Select(x => x.Value).Sum();
                }
            }
        }

        /// <summary>
        /// Returns the time to wait before the next event can be scheduled
        /// </summary>
        public TimeSpan WaitTime
        {
            get
            {
                lock (m_lock)
                {
                    var now = DateTime.Now;
                    RemoveOldEvents();
                    if (m_rates.Count == 0)
                        return new TimeSpan(0);

                    var current = m_rates.Select(x => x.Value).Sum();
                    if (current < m_maxrate)
                        return new TimeSpan(0);

                    for(var i = 0; i < m_rates.Count; i++)
                    {
                        current -= m_rates[i].Value;
                        if (current < m_maxrate)
                            return m_duration - (now - m_rates[i].Key);
                    }

                    // Should not get here, so just-in-case
                    return m_duration;
                }

            }
        }
    }
}
