using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace UniqueLootHelper.Managers;

public class StatisticsData
{
    public DateTime SessionStartTime { get; set; } = DateTime.Now;
    public TimeSpan SessionDuration { get; set; }
    public Dictionary<string, int> ItemsByTier { get; set; } = [];
    public Dictionary<string, int> ItemsByRarity { get; set; } = [];
    public Dictionary<string, int> ItemsByType { get; set; } = [];
    public int TotalItems { get; set; }
    public int TotalUniques { get; set; }
    public Dictionary<string, TierStatistics> UniqueTierStats { get; set; } = [];
    public Dictionary<string, int> TopUniqueDrops { get; set; } = [];

    // Currency and valuable items statistics
    public int TotalCurrency { get; set; }
    public int TotalDivinationCards { get; set; }
    public int TotalFragments { get; set; }
    public int TotalEssences { get; set; }
    public int TotalScarabs { get; set; }
    public Dictionary<string, int> CurrencyDrops { get; set; } = [];
    public Dictionary<string, int> DivinationCardDrops { get; set; } = [];
    public Dictionary<string, int> ValueableItemsByType { get; set; } = [];

    // Map-based statistics
    public int TotalMapsRun { get; set; }
    public int TotalUniquesInMaps { get; set; }
    public double AverageUniquesPerMap { get; set; }
    public double AverageMapDurationMinutes { get; set; }
    public TimeSpan TotalMapTime { get; set; }
    public MapSessionData BestMapSession { get; set; } = new();
    public MapSessionData FastestMapSession { get; set; } = new();
    public List<MapSessionData> RecentMapSessions { get; set; } = [];
    public Dictionary<string, double> AverageUniquesByTier { get; set; } = [];

    public class TierStatistics
    {
        public int Count { get; set; }
        public double Percentage { get; set; }
        public List<string> ItemNames { get; set; } = [];
    }

    public class MapSessionData
    {
        public string MapName { get; set; } = string.Empty;
        public int UniqueCount { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public TimeSpan Duration { get; set; }
        public Dictionary<string, int> TierBreakdown { get; set; } = [];
        public List<string> UniqueNames { get; set; } = [];
        public double UniquesPerMinute { get; set; }
    }

    // Helper method to get percentage of uniques vs other base rarities
    public double GetUniquePercentageVsBaseRarities()
    {
        // Count base rarities: Normal, Magic, Rare, Unique
        int normalCount = ItemsByRarity.TryGetValue("Normal", out int n) ? n : 0;
        int magicCount = ItemsByRarity.TryGetValue("Magic", out int m) ? m : 0;
        int rareCount = ItemsByRarity.TryGetValue("Rare", out int r) ? r : 0;
        int uniqueCount = ItemsByRarity.TryGetValue("Unique", out int u) ? u : 0;

        int totalBaseItems = normalCount + magicCount + rareCount + uniqueCount;

        if (totalBaseItems == 0)
            return 0;

        return (double)uniqueCount / totalBaseItems * 100.0;
    }

    // Helper method to get total base rarity items count
    public int GetTotalBaseRarityItems()
    {
        int normalCount = ItemsByRarity.TryGetValue("Normal", out int n) ? n : 0;
        int magicCount = ItemsByRarity.TryGetValue("Magic", out int m) ? m : 0;
        int rareCount = ItemsByRarity.TryGetValue("Rare", out int r) ? r : 0;
        int uniqueCount = ItemsByRarity.TryGetValue("Unique", out int u) ? u : 0;

        return normalCount + magicCount + rareCount + uniqueCount;
    }
}

public class StatisticsManager
{
    private readonly string _statisticsFilePath;
    private readonly Action<string> _logMessage;
    private readonly Action<string> _logError;
    private readonly Stopwatch _sessionTimer;
    private readonly UniqueItemsListManager _uniqueItemsListManager;

    private StatisticsData _currentSessionStats = new();
    private StatisticsData _lifetimeStats = new();

    private readonly Dictionary<long, bool> _trackedEntityIds = [];

    // Map session tracking
    private StatisticsData.MapSessionData _currentMapSession;
    private uint _currentMapHash;
    private int _currentInstanceId;
    private bool _isInValidMapArea;
    private readonly Stopwatch _mapSessionTimer = new();

    // Previous area tracking for smart map detection
    private uint _previousAreaHash;
    private int _previousInstanceId;
    private bool _previousWasHideoutOrTown;

    public StatisticsData CurrentSession => _currentSessionStats;
    public StatisticsData Lifetime => _lifetimeStats;
    public StatisticsData.MapSessionData CurrentMapSession => _currentMapSession;
    public bool IsInMap => _isInValidMapArea;

    public int GetCurrentMapUniqueCount() => _currentMapSession?.UniqueCount ?? 0;
    public int GetCurrentSessionUniqueCount() => _currentSessionStats?.TotalUniques ?? 0;

    public StatisticsManager(
        string configDirectory,
        Action<string> logMessage,
        Action<string> logError,
        UniqueItemsListManager uniqueItemsListManager)
    {
        _logMessage = logMessage;
        _logError = logError;
        _uniqueItemsListManager = uniqueItemsListManager;
        _statisticsFilePath = Path.Combine(configDirectory, "statistics.json");
        _sessionTimer = Stopwatch.StartNew();
        _currentMapSession = new StatisticsData.MapSessionData();
        _isInValidMapArea = false;
        _previousWasHideoutOrTown = true;

        LoadStatistics();
    }

    public void ResetSession()
    {
        _currentSessionStats = new StatisticsData
        {
            SessionStartTime = DateTime.Now
        };
        _sessionTimer.Restart();
        _trackedEntityIds.Clear();
        _currentMapSession = new StatisticsData.MapSessionData();
        _currentMapHash = 0;
        _isInValidMapArea = false;
        _mapSessionTimer.Reset();
        _logMessage("UniqueLootHelper: Statistics session reset");
    }

    public void TrackItemDrop(long entityId, Entity itemEntity)
    {
        if (_trackedEntityIds.ContainsKey(entityId))
            return;

        itemEntity.TryGetComponent<RenderItem>(out var render);
        string resourcePath = NormalizeResourcePath(render?.ResourcePath ?? string.Empty);

        itemEntity.TryGetComponent(out Base itemBase);
        string itemName = itemBase?.Info?.Name ?? string.Empty;

        ItemRarity rarity;
        if (itemEntity.TryGetComponent<Mods>(out var mods) && mods.ItemRarity != ItemRarity.Unknown)
            rarity = mods.ItemRarity;
        else
            rarity = ItemRarity.Normal;

        // Path-based currency detection — more reliable than Mods.ItemRarity for currency items
        string entityPath = itemEntity.Path ?? string.Empty;
        if (rarity != ItemRarity.Unique && entityPath.Contains("/Items/Currency"))
            rarity = ItemRarity.Currency;

        if (rarity == ItemRarity.Unknown)
            return;

        UniqueItemInfo? uniqueInfo = rarity == ItemRarity.Unique && !string.IsNullOrEmpty(resourcePath)
            ? GetUniqueInfo(resourcePath)
            : null;

        _trackedEntityIds[entityId] = true;

        UpdateStats(_currentSessionStats, rarity, itemName, resourcePath, uniqueInfo);
        UpdateStats(_lifetimeStats, rarity, itemName, resourcePath, uniqueInfo);

        if (_isInValidMapArea && rarity == ItemRarity.Unique)
        {
            UniqueTrackMapDrop(uniqueInfo, resourcePath);
        }
    }

    private void UpdateStats(StatisticsData stats, ItemRarity rarity, string itemName, string artPath, UniqueItemInfo? uniqueInfo = null)
    {
        stats.TotalItems++;

        string rarityKey = rarity.ToString();
        stats.ItemsByRarity.TryGetValue(rarityKey, out int rarityCount);
        stats.ItemsByRarity[rarityKey] = rarityCount + 1;

        if (rarity == ItemRarity.Currency)
        {
            stats.TotalCurrency++;
            if (!string.IsNullOrEmpty(itemName))
            {
                stats.CurrencyDrops.TryGetValue(itemName, out int cd);
                stats.CurrencyDrops[itemName] = cd + 1;
            }
        }

        if (rarity == ItemRarity.Unique && !string.IsNullOrEmpty(artPath))
        {
            stats.TotalUniques++;

            string tier = uniqueInfo?.Grouping ?? string.Empty;
            string displayName = uniqueInfo?.Name ?? string.Empty;

            if (!string.IsNullOrEmpty(tier))
            {
                stats.ItemsByTier.TryGetValue(tier, out int tierCount);
                stats.ItemsByTier[tier] = tierCount + 1;

                if (!stats.UniqueTierStats.TryGetValue(tier, out var tierStats))
                {
                    tierStats = new StatisticsData.TierStatistics();
                    stats.UniqueTierStats[tier] = tierStats;
                }

                tierStats.Count++;

                if (tier == "T0" && !string.IsNullOrEmpty(displayName) && !tierStats.ItemNames.Contains(displayName))
                    tierStats.ItemNames.Add(displayName);
            }

            if (tier == "T0" && !string.IsNullOrEmpty(displayName))
            {
                stats.TopUniqueDrops.TryGetValue(displayName, out int dropCount);
                stats.TopUniqueDrops[displayName] = dropCount + 1;
            }
        }
    }

    private UniqueItemInfo? GetUniqueInfo(string artPath)
    {
        if (string.IsNullOrEmpty(artPath))
            return null;

        return _uniqueItemsListManager.UniqueItemsList
            .FirstOrDefault(item => item.Art.Equals(artPath, StringComparison.OrdinalIgnoreCase));
    }

    public void UpdateAverages()
    {
        UpdateStatsAverages(_currentSessionStats, _sessionTimer.Elapsed);
        UpdateStatsAverages(_lifetimeStats, _lifetimeStats.SessionDuration);
    }

    private void UpdateStatsAverages(StatisticsData stats, TimeSpan duration)
    {
        stats.SessionDuration = duration;

        // Calculate tier percentages
        if (stats.TotalUniques > 0)
        {
            foreach (var tierStat in stats.UniqueTierStats.Values)
            {
                tierStat.Percentage = (double)tierStat.Count / stats.TotalUniques * 100.0;
            }
        }
    }

    public void SaveStatistics()
    {
        try
        {
            UpdateAverages();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var data = new
            {
                LastSession = _currentSessionStats,
                Lifetime = _lifetimeStats,
                LastSaved = DateTime.Now
            };

            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(_statisticsFilePath, json);

            _logMessage($"UniqueLootHelper: Statistics saved to {_statisticsFilePath}");
        }
        catch (Exception ex)
        {
            _logError($"UniqueLootHelper: Failed to save statistics: {ex.Message}");
        }
    }

    private void LoadStatistics()
    {
        try
        {
            if (!File.Exists(_statisticsFilePath))
            {
                _logMessage("UniqueLootHelper: No existing statistics file found, starting fresh");
                return;
            }

            string json = File.ReadAllText(_statisticsFilePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("Lifetime", out var lifetimeElement))
            {
                _lifetimeStats = JsonSerializer.Deserialize<StatisticsData>(lifetimeElement.GetRawText())
                    ?? new StatisticsData();
            }

            _logMessage("UniqueLootHelper: Statistics loaded successfully");
        }
        catch (Exception ex)
        {
            _logError($"UniqueLootHelper: Failed to load statistics: {ex.Message}");
            _lifetimeStats = new StatisticsData();
        }
    }

    public void AreaChange(AreaInstance area)
    {
        _trackedEntityIds.Clear();

        // Determine if new area is a valid map area
        bool isValidMapArea = !area.IsTown && !area.IsHideout && !area.IsPeaceful;

        // Additional check: area name should contain "Map" for actual maps
        bool isActualMap = isValidMapArea && area.Area.IsUnique && area.Area.Id.StartsWith("Map");

        // Logic:
        // - Entering hideout/town FROM map -> finalize current map session
        // - Entering map FROM hideout/town:
        //   * Same InstanceId -> RETURN to same map (continue session)
        //   * Different InstanceId -> NEW map (start new session)
        // - Map -> Map (any hash/instance) -> sub-zone (continue current session)

        if (!isActualMap && (area.IsTown || area.IsHideout))
        {
            // Entering hideout/town - finalize current map if it was active
            if (_isInValidMapArea && _currentMapSession.UniqueCount > 0)
            {
                FinalizeMapSession();
                _logMessage($"UniqueLootHelper: Entered hideout/town, finalized map session");
            }

            _isInValidMapArea = false;
            _previousWasHideoutOrTown = true;
        }
        else if (isActualMap)
        {
            // Entering a map area
            if (_previousWasHideoutOrTown)
            {
                // Coming from hideout/town - check InstanceId
                if (area.InstanceId == _previousInstanceId)
                {
                    // SAME InstanceId = returning to the same map instance!
                    _isInValidMapArea = true;
                    _mapSessionTimer.Start();
                    _previousWasHideoutOrTown = false;
                    _logMessage($"UniqueLootHelper: Returned to same map instance: {area.Name} (continuing session)");
                }
                else
                {
                    // Different InstanceId = NEW map
                    _currentMapHash = area.Hash;
                    _currentInstanceId = area.InstanceId;
                    _isInValidMapArea = true;
                    _currentMapSession = new StatisticsData.MapSessionData
                    {
                        MapName = area.Name,
                        StartTime = DateTime.Now
                    };
                    _mapSessionTimer.Restart();
                    _logMessage($"UniqueLootHelper: Started new map session: {area.Name} (InstanceId: {area.InstanceId})");
                    _previousWasHideoutOrTown = false;
                }
            }
            else
            {
                // Map -> Map transition (any hash/instance) = sub-zone, continue current session
                if (_isInValidMapArea)
                {
                    _logMessage($"UniqueLootHelper: Sub-zone detected: {area.Name} (continuing current map session)");
                }
                else
                {
                    // Edge case: wasn't tracking before, start now
                    _currentMapHash = area.Hash;
                    _currentInstanceId = area.InstanceId;
                    _isInValidMapArea = true;
                    _currentMapSession = new StatisticsData.MapSessionData
                    {
                        MapName = area.Name,
                        StartTime = DateTime.Now
                    };
                    _mapSessionTimer.Restart();
                    _logMessage($"UniqueLootHelper: Started map tracking: {area.Name}");
                }
            }
        }

        // Update previous area state
        _previousAreaHash = area.Hash;
        _previousInstanceId = area.InstanceId;

        SaveStatistics();
    }

    public void HandleLogout()
    {
        // Finalize current map session if active
        if (_isInValidMapArea && _currentMapSession.UniqueCount > 0)
        {
            FinalizeMapSession();
            _logMessage("UniqueLootHelper: Logout detected, finalized map session");
        }

        // Save statistics
        SaveStatistics();
        _logMessage("UniqueLootHelper: Statistics saved on logout");

        // Reset state
        _isInValidMapArea = false;
        _previousWasHideoutOrTown = true;
    }

    public void ResetSessionStatistics()
    {
        _currentSessionStats = new StatisticsData
        {
            SessionStartTime = DateTime.Now
        };
        _sessionTimer.Restart();
        _trackedEntityIds.Clear();

        // Reset current map session
        _currentMapSession = new StatisticsData.MapSessionData();
        _currentMapHash = 0;
        _currentInstanceId = 0;
        _isInValidMapArea = false;
        _previousWasHideoutOrTown = true;
        _mapSessionTimer.Reset();

        SaveStatistics();
        _logMessage("UniqueLootHelper: Session statistics reset");
    }

    public void ResetLifetimeStatistics()
    {
        _lifetimeStats = new StatisticsData
        {
            SessionStartTime = DateTime.Now
        };

        SaveStatistics();
        _logMessage("UniqueLootHelper: Lifetime statistics reset");
    }

    private void UniqueTrackMapDrop(UniqueItemInfo? uniqueInfo, string artPath)
    {
        _currentMapSession.UniqueCount++;

        string tier = uniqueInfo?.Grouping ?? string.Empty;
        if (!string.IsNullOrEmpty(tier))
        {
            _currentMapSession.TierBreakdown.TryGetValue(tier, out int count);
            _currentMapSession.TierBreakdown[tier] = count + 1;
        }

        string displayName = uniqueInfo?.Name ?? string.Empty;
        if (tier == "T0" && !string.IsNullOrEmpty(displayName) && !_currentMapSession.UniqueNames.Contains(displayName))
            _currentMapSession.UniqueNames.Add(displayName);
    }

    private void FinalizeMapSession()
    {
        _mapSessionTimer.Stop();
        _currentMapSession.Duration = _mapSessionTimer.Elapsed;

        // Calculate uniques per minute
        if (_currentMapSession.Duration.TotalMinutes > 0)
        {
            _currentMapSession.UniquesPerMinute = _currentMapSession.UniqueCount / _currentMapSession.Duration.TotalMinutes;
        }

        // Update session stats
        UpdateMapSessionStats(_currentSessionStats, _currentMapSession);

        // Update lifetime stats
        UpdateMapSessionStats(_lifetimeStats, _currentMapSession);

        _logMessage($"UniqueLootHelper: Completed map session - {_currentMapSession.MapName}: {_currentMapSession.UniqueCount} uniques in {FormatTimeSpan(_currentMapSession.Duration)} ({_currentMapSession.UniquesPerMinute:F2}/min)");
    }

    private void UpdateMapSessionStats(StatisticsData stats, StatisticsData.MapSessionData mapSession)
    {
        stats.TotalMapsRun++;
        stats.TotalUniquesInMaps += mapSession.UniqueCount;
        stats.TotalMapTime += mapSession.Duration;

        // Calculate averages
        if (stats.TotalMapsRun > 0)
        {
            stats.AverageUniquesPerMap = (double)stats.TotalUniquesInMaps / stats.TotalMapsRun;
            stats.AverageMapDurationMinutes = stats.TotalMapTime.TotalMinutes / stats.TotalMapsRun;
        }

        // Track tier averages
        foreach (var tierCount in mapSession.TierBreakdown)
        {
            stats.AverageUniquesByTier.TryGetValue(tierCount.Key, out double currentAvg);
            double totalForTier = currentAvg * (stats.TotalMapsRun - 1) + tierCount.Value;
            stats.AverageUniquesByTier[tierCount.Key] = totalForTier / stats.TotalMapsRun;
        }

        // Update best map session (most uniques)
        if (mapSession.UniqueCount > stats.BestMapSession.UniqueCount)
        {
            stats.BestMapSession = CopyMapSession(mapSession);
        }

        // Update fastest map session (shortest duration with at least 1 unique)
        if (mapSession.UniqueCount > 0 &&
            (stats.FastestMapSession.UniqueCount == 0 || mapSession.Duration < stats.FastestMapSession.Duration))
        {
            stats.FastestMapSession = CopyMapSession(mapSession);
        }

        // Keep recent map sessions (last 10)
        stats.RecentMapSessions.Add(CopyMapSession(mapSession));

        if (stats.RecentMapSessions.Count > 10)
        {
            stats.RecentMapSessions.RemoveAt(0);
        }
    }

    private static StatisticsData.MapSessionData CopyMapSession(StatisticsData.MapSessionData source)
    {
        return new StatisticsData.MapSessionData
        {
            MapName = source.MapName,
            UniqueCount = source.UniqueCount,
            StartTime = source.StartTime,
            Duration = source.Duration,
            UniquesPerMinute = source.UniquesPerMinute,
            TierBreakdown = new Dictionary<string, int>(source.TierBreakdown),
            UniqueNames = new List<string>(source.UniqueNames)
        };
    }

    private static string NormalizeResourcePath(string path) =>
        string.IsNullOrEmpty(path) ? string.Empty
            : path.Replace(".dds", string.Empty, StringComparison.OrdinalIgnoreCase) + ".dds";

    private static string FormatTimeSpan(TimeSpan span)
    {
        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        return $"{(int)span.TotalSeconds}s";
    }
}
