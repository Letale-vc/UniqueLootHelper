using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UniqueLootHelper;

namespace UniqueLootHelper.Managers;

/// <summary>
/// Manages loading and parsing the unique items list from UniqueItemsInfo.json
/// </summary>
public class UniqueItemsListManager
{
    private const string UniqueItemsFileName = "UniqueItemsInfo.json";

    private readonly string _uniqueItemsFilePath;
    private readonly Action<string> _logMessage;
    private readonly Action<string> _logError;
    private readonly List<UniqueItemInfo> _uniqueItemsList = [];
    private readonly Dictionary<string, int> _baseCounts = [];
    public IReadOnlyList<UniqueItemInfo> UniqueItemsList => _uniqueItemsList;
    public IReadOnlyDictionary<string, int> BaseCounts => _baseCounts;

    public UniqueItemsListManager(string pluginDirectory, Action<string> logMessage, Action<string> logError)
    {
        _uniqueItemsFilePath = Path.Combine(pluginDirectory, UniqueItemsFileName);
        _logMessage = logMessage;
        _logError = logError;
        LoadUniqueItemsList();
    }

    /// <summary>
    /// Loads the unique items list from file
    /// </summary>
    public void LoadUniqueItemsList()
    {
        _uniqueItemsList.Clear();
        _baseCounts.Clear();

        if (!File.Exists(_uniqueItemsFilePath))
        {
            _logMessage(
                $"UniqueLootHelper: Unique items list file not found at {_uniqueItemsFilePath}"
            );
            return;
        }

        try
        {
            string jsonContent = File.ReadAllText(_uniqueItemsFilePath);
            var items = JsonSerializer.Deserialize<List<UniqueItemInfo>>(jsonContent);

            if (items == null)
            {
                _logError("UniqueLootHelper: Failed to deserialize unique items list");
                return;
            }

            _uniqueItemsList.AddRange(items);

            // Calculate base counts
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Base))
                {
                    _baseCounts.TryGetValue(item.Base, out int count);
                    _baseCounts[item.Base] = count + 1;
                }
            }

            _logMessage(
                $"UniqueLootHelper: Loaded {items.Count} unique items from list file"
            );
        }
        catch (Exception ex)
        {
            _logError($"UniqueLootHelper: Failed to load unique items list: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches for items matching the search term using regex
    /// </summary>
    public List<UniqueItemInfo> SearchItems(string searchPattern)
    {
        if (string.IsNullOrWhiteSpace(searchPattern))
            return [];

        try
        {
            var regex = new System.Text.RegularExpressions.Regex(searchPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return [.. _uniqueItemsList
                .Where(item =>
                    regex.IsMatch(item.Name ?? "") ||
                    regex.IsMatch(item.Base ?? "") ||
                    regex.IsMatch(item.Tier ?? "") ||
                    regex.IsMatch(item.Grouping ?? "") ||
                    regex.IsMatch(item.League ?? "")
                )
                .Take(50)
                .OrderBy(item => item.Name)];
        }
        catch (ArgumentException)
        {
            // Invalid regex pattern
            return [];
        }
    }
}
