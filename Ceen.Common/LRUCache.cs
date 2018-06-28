using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ceen
{
    /// <summary>
    /// Implements a basic least-recently-used cache
    /// </summary>
    public class LRUCache<T>
    {
        /// <summary>
        /// The lookup table giving O(1) access to the values
        /// </summary>
        private readonly Dictionary<string, KeyValuePair<T, long>> m_lookup = new Dictionary<string, KeyValuePair<T, long>>();
        /// <summary>
        /// The most-recent-used list that is updated in O(n)
        /// </summary>
        private readonly List<string> m_mru = new List<string>();

        /// <summary>
        /// The handler method that is used to return the size of each element
        /// </summary>
        private readonly Func<T, Task<long>> m_sizecalculator;
        /// <summary>
        /// The handler invoked whne an item is expired
        /// </summary>
        private readonly Func<T, Task> m_expirehandler;

        /// <summary>
        /// The lock guarding the cache
        /// </summary>
        private readonly AsyncLock m_lock = new AsyncLock();

        /// <summary>
        /// The size of the elements in the cache
        /// </summary>
        private long m_size;
        /// <summary>
        /// The maximum allowed size of the cache
        /// </summary>
        private readonly long m_sizelimit;
        /// <summary>
        /// The maximum number of elements in the cache
        /// </summary>
        private readonly long m_countlimit;

        /// <summary>
        /// Creates a new least-recent-used cache
        /// </summary>
        /// <param name="sizelimit">The limit for the size of the cache.</param>
        /// <param name="countlimit">The limit for the number of items in the cache.</param>
        /// <param name="expirationHandler">A callback method invoked when items are expired from the cache.</param>
        /// <param name="sizeHandler">A callback handler used to compute the size of elements add and removed from the queue.</param>
        public LRUCache(long sizelimit = long.MaxValue, long countlimit = long.MaxValue, Func<T, Task> expirationHandler = null, Func<T, Task<long>> sizeHandler = null)
        {
            if (sizelimit != long.MaxValue && sizeHandler == null)
                throw new Exception("Must supply a size handler to enforce the cache size");

            m_sizelimit = sizelimit;
            m_countlimit = countlimit;
            m_expirehandler = expirationHandler;
            m_sizecalculator = sizeHandler ?? (_ => Task.FromResult(0L));
        }

        /// <summary>
        /// Adds or replaces a cache element
        /// </summary>
        /// <returns><c>true</c> if the value was new, <c>false</c> otherwise</returns>
        /// <param name="key">The element key.</param>
        /// <param name="value">The element value.</param>
        public async Task<bool> AddOrReplace(string key, T value)
        {
            using (await m_lock.LockAsync())
            {
                var p = m_lookup.TryGetValue(key, out var vt);
                if (p)
                {
                    m_size -= vt.Value;
                    m_mru.Remove(key);
                    await m_expirehandler.Invoke(vt.Key);
                }

                var s = await m_sizecalculator(value);
                m_size += s;
                m_lookup[key] = new KeyValuePair<T, long>(value, s);
                m_mru.Add(key);

                await ExpireOverLimit();

                return !p;
            }
        }

        /// <summary>
        /// Expires items that are outside the limits
        /// </summary>
        /// <returns>An awaitable task.</returns>
        private async Task ExpireOverLimit()
        {
            while (m_mru.Count > 0 && m_size >= m_sizelimit || m_mru.Count >= m_countlimit)
            {
                var k = m_mru[0];
                m_mru.RemoveAt(0);
                var v = m_lookup[k];
                m_size -= v.Value;
                m_lookup.Remove(k);
                await m_expirehandler?.Invoke(v.Key);
            }
        }

        /// <summary>
        /// Tries to get the value from the cache
        /// </summary>
        /// <returns><c>true</c>, if the value was found, <c>false</c> otherwise.</returns>
        /// <param name="key">The key to look for.</param>
        /// <param name="value">The value for the key, or the default.</param>
        public bool TryGetValue(string key, out T value)
        {
            if (m_lookup.TryGetValue(key, out var n))
            {
                value = n.Key;
                return true;
            }

            value = default(T);
            return false;
        }

        /// <summary>
        /// Gets the element from the cache with the specified key, or the default value
        /// </summary>
        /// <param name="key">The key to look for.</param>
        public T this[string key]
        {
            get
            {
                TryGetValue(key, out var n);
                return n;
            }
        }

    }
}
