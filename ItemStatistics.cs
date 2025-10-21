using System;
using System.Collections.Generic;

namespace UniqueLootHelper
{
    public class ItemStatisticsEntry
    {
        public int TotalFound { get; set; }
        public int FoundInCurrentSession { get; set; }
        public DateTime? LastDiscoveryTime { get; set; }
        public DateTime? FirstDiscoveryTime { get; set; }
    }

    public class ItemStatistics
    {
        public Dictionary<string, ItemStatisticsEntry> Statistics { get; set; } = new();
        public DateTime SessionStartTime { get; set; } = DateTime.Now;
        public int TotalItemsFoundInSession { get; set; }

        public void RecordItemFound(string itemKey)
        {
            if (!Statistics.ContainsKey(itemKey))
            {
                Statistics[itemKey] = new ItemStatisticsEntry
                {
                    FirstDiscoveryTime = DateTime.Now
                };
            }

            var entry = Statistics[itemKey];
            entry.TotalFound++;
            entry.FoundInCurrentSession++;
            entry.LastDiscoveryTime = DateTime.Now;
            TotalItemsFoundInSession++;
        }

        public void ResetSessionStatistics()
        {
            SessionStartTime = DateTime.Now;
            TotalItemsFoundInSession = 0;
            foreach (var entry in Statistics.Values)
            {
                entry.FoundInCurrentSession = 0;
            }
        }

        public ItemStatisticsEntry GetStatistics(string itemKey)
        {
            return Statistics.GetValueOrDefault(itemKey, new ItemStatisticsEntry());
        }
    }
}
