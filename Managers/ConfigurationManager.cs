using System;
using System.Collections.Generic;
using System.IO;
using UniqueLootHelper.Utils;

namespace UniqueLootHelper.Managers;

/// <summary>
/// Manages loading, saving, and caching of unique item artwork configurations
/// </summary>
public class ConfigurationManager
{
    private static readonly string FileArtName = new("UniquesArtworks.json");

    private readonly FileInfo _artFileInfo;
    private readonly Dictionary<string, UniqueItemSettings> _cacheUniqueArtWork = [];
    private readonly Action<string> _logMessage;
    private readonly Action<string> _logError;

    public IReadOnlyDictionary<string, UniqueItemSettings> UniqueArtWork => _cacheUniqueArtWork;

    public ConfigurationManager(string configDirectory, Action<string> logMessage, Action<string> logError)
    {
        _artFileInfo = new(Path.Combine(configDirectory, FileArtName));
        _logMessage = logMessage;
        _logError = logError;
        _cacheUniqueArtWork = JsonFIlesHelper.CreateOrLoadJsonFile<Dictionary<string, UniqueItemSettings>>(_artFileInfo);
    }


    /// <summary>
    /// Saves unique artwork configurations to file
    /// </summary>
    public void SaveUniqueArtToFile()
    {
        JsonFIlesHelper.SaveJsonFile(_artFileInfo, _cacheUniqueArtWork);
    }

    /// <summary>
    /// Adds or updates a unique item configuration
    /// </summary>
    public bool AddOrUpdateUniqueItem(string artPath, UniqueItemSettings settings)
    {
        if (string.IsNullOrEmpty(artPath) || settings == null)
        {
            return false;
        }

        // Always ensure the path has .dds extension
        string normalizedPath = artPath.Replace(".dds", "") + ".dds";

        bool isUpdate = _cacheUniqueArtWork.ContainsKey(normalizedPath);
        _cacheUniqueArtWork[normalizedPath] = settings;

        string action = isUpdate ? "Updated" : "Added";
        _logMessage($"UniqueLootHelper: {action} {normalizedPath} in unique list");
        return true;
    }

    /// <summary>
    /// Removes a unique item configuration
    /// </summary>
    public bool RemoveUniqueItem(string artPath)
    {
        // Normalize to .dds path
        string normalizedPath = artPath.Replace(".dds", "") + ".dds";

        if (_cacheUniqueArtWork.Remove(normalizedPath))
        {
            _logMessage($"UniqueLootHelper: Removed {normalizedPath} from unique list");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get settings for an item by matching its resource path
    /// </summary>
    public bool TryGetUniqueSettings(string resourcePath, out UniqueItemSettings? settings, out string? matchedKey)
    {
        // Try with .dds extension (normalized)
        string normalizedPath = resourcePath.Replace(".dds", "") + ".dds";

        if (_cacheUniqueArtWork.TryGetValue(normalizedPath, out settings))
        {
            matchedKey = normalizedPath;
            return true;
        }

        settings = null;
        matchedKey = null;
        return false;
    }


    /// <summary>
    /// Merges imported configuration with existing configuration
    /// </summary>
    public void MergeConfiguration(Dictionary<string, UniqueItemSettings> importedConfig)
    {
        if (importedConfig == null || importedConfig.Count == 0)
        {
            return;
        }

        foreach (var kvp in importedConfig)
        {
            _cacheUniqueArtWork[kvp.Key] = kvp.Value;
        }

        _logMessage($"UniqueLootHelper: Merged {importedConfig.Count} unique items");
    }

    /// <summary>
    /// Replaces all configuration with imported configuration
    /// </summary>
    public void ReplaceConfiguration(Dictionary<string, UniqueItemSettings> importedConfig)
    {
        if (importedConfig == null)
        {
            _logError("UniqueLootHelper: Cannot replace with null configuration");
            return;
        }

        int oldCount = _cacheUniqueArtWork.Count;
        _cacheUniqueArtWork.Clear();

        foreach (var kvp in importedConfig)
        {
            _cacheUniqueArtWork[kvp.Key] = kvp.Value;
        }

        _logMessage($"UniqueLootHelper: Replaced {oldCount} items with {importedConfig.Count} unique items");
    }

    /// <summary>
    /// Gets all unique artwork configurations
    /// </summary>
    public Dictionary<string, UniqueItemSettings> GetAllConfigurations()
    {
        return new Dictionary<string, UniqueItemSettings>(_cacheUniqueArtWork);
    }
}
