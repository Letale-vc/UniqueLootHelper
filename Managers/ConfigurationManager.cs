using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UniqueLootHelper.Managers
{
    /// <summary>
    /// Manages loading, saving, and caching of unique item artwork configurations
    /// </summary>
    public class ConfigurationManager
    {
        private const string FileArtName = "UniquesArtworks.json";

        private readonly string _artFilePath;
        private Dictionary<string, UniqueItemSettings> _cacheUniqueArtWork = [];
        private readonly Action<string> _logMessage;
        private readonly Action<string> _logError;

        public IReadOnlyDictionary<string, UniqueItemSettings> UniqueArtWork => _cacheUniqueArtWork;

        public ConfigurationManager(string configDirectory, Action<string> logMessage, Action<string> logError)
        {
            _artFilePath = Path.Combine(configDirectory, FileArtName);
            _logMessage = logMessage;
            _logError = logError;
            _cacheUniqueArtWork = LoadUniqueArtFromFile();
        }

        /// <summary>
        /// Creates an empty unique art file if it doesn't exist
        /// </summary>
        private void CreateUniqueArtFile()
        {
            if (File.Exists(_artFilePath))
            {
                return;
            }

            try
            {
                File.WriteAllText(_artFilePath, JsonConvert.SerializeObject(new Dictionary<string, UniqueItemSettings>(), Formatting.Indented));
                _logMessage("UniqueLootHelper: Created new file for unique art");
            }
            catch (Exception ex)
            {
                _logError($"UniqueLootHelper: Failed to create unique art file: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads unique artwork configurations from file
        /// </summary>
        private Dictionary<string, UniqueItemSettings> LoadUniqueArtFromFile()
        {
            if (!File.Exists(_artFilePath))
            {
                CreateUniqueArtFile();
            }

            try
            {
                string json = File.ReadAllText(_artFilePath);
                Dictionary<string, UniqueItemSettings> loadedItems = 
                    JsonConvert.DeserializeObject<Dictionary<string, UniqueItemSettings>>(json);
                
                if (loadedItems == null || loadedItems.Count == 0)
                {
                    return [];
                }

                // Normalize all paths to have .dds extension
                var normalizedItems = new Dictionary<string, UniqueItemSettings>();
                
                foreach (var kvp in loadedItems)
                {
                    string normalizedPath = kvp.Key.Replace(".dds", "") + ".dds";
                    normalizedItems[normalizedPath] = kvp.Value;
                }

                return normalizedItems;
            }
            catch (Exception ex)
            {
                _logError($"UniqueLootHelper: Failed to load unique art file: {ex.Message}");
                try
                {
                    File.Move(_artFilePath, _artFilePath + ".bak");
                    CreateUniqueArtFile();
                }
                catch (Exception backupEx)
                {
                    _logError($"UniqueLootHelper: Failed to backup corrupted file: {backupEx.Message}");
                }
                return [];
            }
        }

        /// <summary>
        /// Saves unique artwork configurations to file
        /// </summary>
        public void SaveUniqueArtToFile()
        {
            try
            {
                if (!File.Exists(_artFilePath))
                {
                    CreateUniqueArtFile();
                }

                string json = JsonConvert.SerializeObject(_cacheUniqueArtWork, Formatting.Indented);
                File.WriteAllText(_artFilePath, json);
                _logMessage("UniqueLootHelper: Saved unique art to file");
            }
            catch (Exception ex)
            {
                _logError($"UniqueLootHelper: Failed to save unique art file: {ex.Message}");
            }
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
        public bool TryGetUniqueSettings(string resourcePath, out UniqueItemSettings settings, out string matchedKey)
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
        /// Reloads configuration from file
        /// </summary>
        public void ReloadConfiguration()
        {
            _cacheUniqueArtWork = LoadUniqueArtFromFile();
            _logMessage("UniqueLootHelper: Reloaded configuration from file");
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
        /// Gets all unique artwork configurations
        /// </summary>
        public Dictionary<string, UniqueItemSettings> GetAllConfigurations()
        {
            return new Dictionary<string, UniqueItemSettings>(_cacheUniqueArtWork);
        }
    }
}
