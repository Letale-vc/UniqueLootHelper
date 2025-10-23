using System;
using System.Collections.Generic;
using System.Numerics;
using ExileCore;

namespace UniqueLootHelper.Managers
{
    /// <summary>
    /// Provides caching for Graphics.MeasureText() results to improve rendering performance
    /// </summary>
    public class TextMeasurementCache
    {
        private readonly Dictionary<string, CacheEntry> _cache;
        private readonly TimeSpan _cacheLifetime;
        private DateTime _lastCleanup;
        private readonly TimeSpan _cleanupInterval;
        private readonly int _maxCacheSize;

        private struct CacheEntry
        {
            public Vector2 Size;
            public DateTime Timestamp;

            public CacheEntry(Vector2 size, DateTime timestamp)
            {
                Size = size;
                Timestamp = timestamp;
            }
        }

        /// <summary>
        /// Initializes a new instance of TextMeasurementCache
        /// </summary>
        /// <param name="cacheLifetimeSeconds">How long cache entries remain valid (default: 300 seconds)</param>
        /// <param name="maxCacheSize">Maximum number of cached entries (default: 500)</param>
        public TextMeasurementCache(int cacheLifetimeSeconds = 300, int maxCacheSize = 500)
        {
            _cache = new Dictionary<string, CacheEntry>(maxCacheSize);
            _cacheLifetime = TimeSpan.FromSeconds(cacheLifetimeSeconds);
            _cleanupInterval = TimeSpan.FromSeconds(60);
            _lastCleanup = DateTime.Now;
            _maxCacheSize = maxCacheSize;
        }

        /// <summary>
        /// Gets text size from cache or measures it if not cached
        /// </summary>
        /// <param name="graphics">Graphics instance to use for measurement</param>
        /// <param name="text">Text to measure</param>
        /// <returns>Size of the text</returns>
        public Vector2 GetOrMeasure(Graphics graphics, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            // Perform periodic cleanup
            PerformCleanupIfNeeded();

            // Check if we have a valid cached entry
            if (_cache.TryGetValue(text, out var entry))
            {
                var age = DateTime.Now - entry.Timestamp;
                if (age < _cacheLifetime)
                {
                    return entry.Size;
                }

                // Entry expired, remove it
                _cache.Remove(text);
            }

            // Measure and cache the result
            Vector2 size = graphics.MeasureText(text);

            // Don't exceed max cache size
            if (_cache.Count >= _maxCacheSize)
            {
                // Remove oldest entries (simple cleanup)
                CleanExpiredEntries(forceCleanup: true);
            }

            _cache[text] = new CacheEntry(size, DateTime.Now);
            return size;
        }

        /// <summary>
        /// Clears all cached measurements
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _lastCleanup = DateTime.Now;
        }

        /// <summary>
        /// Gets the current number of cached entries
        /// </summary>
        public int CacheSize => _cache.Count;

        /// <summary>
        /// Performs cleanup if enough time has passed since last cleanup
        /// </summary>
        private void PerformCleanupIfNeeded()
        {
            var timeSinceLastCleanup = DateTime.Now - _lastCleanup;
            if (timeSinceLastCleanup >= _cleanupInterval)
            {
                CleanExpiredEntries(forceCleanup: false);
                _lastCleanup = DateTime.Now;
            }
        }

        /// <summary>
        /// Removes expired entries from cache
        /// </summary>
        /// <param name="forceCleanup">If true, removes half of entries regardless of expiration</param>
        private void CleanExpiredEntries(bool forceCleanup)
        {
            var now = DateTime.Now;
            var keysToRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                var age = now - kvp.Value.Timestamp;
                if (age >= _cacheLifetime)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            // If forced cleanup and we didn't find enough expired entries
            if (forceCleanup && keysToRemove.Count < _maxCacheSize / 2)
            {
                // Remove oldest half of entries
                var sortedEntries = new List<KeyValuePair<string, CacheEntry>>(_cache);
                sortedEntries.Sort((a, b) => a.Value.Timestamp.CompareTo(b.Value.Timestamp));

                int entriesToRemove = _maxCacheSize / 2;
                for (int i = 0; i < entriesToRemove && i < sortedEntries.Count; i++)
                {
                    if (!keysToRemove.Contains(sortedEntries[i].Key))
                    {
                        keysToRemove.Add(sortedEntries[i].Key);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }
    }
}
