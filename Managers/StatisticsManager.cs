using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace UniqueLootHelper.Managers
{
    /// <summary>
    ///     Manages item statistics tracking, loading, and saving
    /// </summary>
    public class StatisticsManager
    {
        private readonly Action<string> _logError;
        private readonly Action<string> _logMessage;
        private readonly HashSet<uint> _statisticsCache = [];
        private readonly string _statisticsFilePath;

        public StatisticsManager(
            string configDirectory,
            Action<string> logMessage,
            Action<string> logError
        )
        {
            _statisticsFilePath = Path.Combine(configDirectory, "Statistics.json");
            _logMessage = logMessage;
            _logError = logError;
            Statistics = LoadStatistics();
        }

        public ItemStatistics Statistics { get; private set; }

        public int TotalItemsFoundInSession => Statistics.TotalItemsFoundInSession;

        /// <summary>
        ///     Loads statistics from file
        /// </summary>
        private ItemStatistics LoadStatistics()
        {
            if (!File.Exists(_statisticsFilePath))
            {
                return new ItemStatistics();
            }

            try
            {
                string json = File.ReadAllText(_statisticsFilePath);
                ItemStatistics statistics = JsonConvert.DeserializeObject<ItemStatistics>(json);
                if (statistics != null)
                {
                    statistics.ResetSessionStatistics();
                    return statistics;
                }
                return new ItemStatistics();
            }
            catch (Exception ex)
            {
                _logError($"UniqueLootHelper: Failed to load statistics: {ex.Message}");
                return new ItemStatistics();
            }
        }

        /// <summary>
        ///     Saves statistics to file
        /// </summary>
        public void SaveStatistics()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Statistics, Formatting.Indented);
                File.WriteAllText(_statisticsFilePath, json);
                _logMessage("UniqueLootHelper: Saved statistics to file");
            }
            catch (Exception ex)
            {
                _logError($"UniqueLootHelper: Failed to save statistics: {ex.Message}");
            }
        }

        /// <summary>
        ///     Records that an item was found if it hasn't been recorded this session
        /// </summary>
        /// <param name="itemId">The unique ID of the item entity</param>
        /// <param name="itemKey">The art path key for the item</param>
        /// <returns>True if the item was newly recorded, false if already cached</returns>
        public bool TryRecordItemFound(uint itemId, string itemKey)
        {
            if (_statisticsCache.Contains(itemId))
            {
                return false;
            }

            _statisticsCache.Add(itemId);
            Statistics.RecordItemFound(itemKey);
            return true;
        }

        /// <summary>
        ///     Resets session statistics
        /// </summary>
        public void ResetSessionStatistics()
        {
            Statistics.ResetSessionStatistics();
            _logMessage("UniqueLootHelper: Reset session statistics");
        }

        /// <summary>
        ///     Resets all statistics
        /// </summary>
        public void ResetAllStatistics()
        {
            Statistics = new ItemStatistics();
            SaveStatistics();
            _logMessage("UniqueLootHelper: Reset all statistics");
        }

        /// <summary>
        ///     Clears the session cache (call on area change)
        /// </summary>
        public void ClearSessionCache()
        {
            _statisticsCache.Clear();
        }

        /// <summary>
        ///     Removes items from cache that are no longer on the ground
        /// </summary>
        /// <param name="currentItemIds">IDs of items currently on the ground</param>
        public void CleanupCache(HashSet<uint> currentItemIds)
        {
            // Optimization: accept HashSet directly, avoid creating intermediate collection
            if (_statisticsCache.Count == 0)
            {
                return;
            }

            // RemoveWhere is more efficient than creating a list and iterating
            _statisticsCache.RemoveWhere(id => !currentItemIds.Contains(id));
        }
    }
}
