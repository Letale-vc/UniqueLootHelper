using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UniqueLootHelper.Managers
{
    /// <summary>
    /// Manages loading and parsing the unique items list from unique_items_output.txt
    /// </summary>
    public class UniqueItemsListManager
    {
        private const string UniqueItemsFileName = "unique_items_output.txt";
        private const string GeneratorExecutableName = "UniqueArtGenerate-win-x64.exe";

        private readonly string _uniqueItemsFilePath;
        private readonly string _generatorExecutablePath;
        private readonly Action<string> _logMessage;
        private readonly Action<string> _logError;
        private readonly Dictionary<string, string> _uniqueItemsList = [];
        public IReadOnlyDictionary<string, string> UniqueItemsList => _uniqueItemsList;
        public bool IsRegenerating { get; private set; } = false;
        public UniqueItemsListManager(string pluginDirectory, Action<string> logMessage, Action<string> logError)
        {
            _uniqueItemsFilePath = Path.Combine(pluginDirectory, UniqueItemsFileName);
            _generatorExecutablePath = Path.Combine(pluginDirectory, GeneratorExecutableName);
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

            if (!File.Exists(_uniqueItemsFilePath))
            {
                _logMessage(
                    $"UniqueLootHelper: Unique items list file not found at {_uniqueItemsFilePath}"
                );
                return;
            }

            try
            {
                int loadedCount = 0;
                foreach (var line in File.ReadLines(_uniqueItemsFilePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var span = line.AsSpan();
                    int separatorIndex = span.IndexOf(';');
                    if (separatorIndex == -1) continue;
                    var nameSpan = span[..separatorIndex].Trim();
                    var artPathSpan = span[(separatorIndex + 1)..].Trim();
                    if (nameSpan.IsEmpty || artPathSpan.IsEmpty) continue;
                    _uniqueItemsList[nameSpan.ToString()] = artPathSpan.ToString();
                    loadedCount++;
                }

                _logMessage(
                    $"UniqueLootHelper: Loaded {loadedCount} unique items from list file"
                );
            }
            catch (Exception ex)
            {
                _logError($"UniqueLootHelper: Failed to load unique items list: {ex.Message}");
            }
        }

        /// <summary>
        /// Regenerates the unique items list by running the generator executable asynchronously
        /// </summary>
        /// <returns>True if the process was started successfully</returns>
        public bool RegenerateUniqueItemsList()
        {
            if (IsRegenerating)
            {
                _logMessage("UniqueLootHelper: Regeneration already in progress");
                return false;
            }

            if (!File.Exists(_generatorExecutablePath))
            {
                _logError(
                    $"UniqueLootHelper: Generator executable not found at {_generatorExecutablePath}"
                );
                return false;
            }

            try
            {
                IsRegenerating = true;

                ProcessStartInfo startInfo =
                    new()
                    {
                        FileName = _generatorExecutablePath,
                        WorkingDirectory = Path.GetDirectoryName(_generatorExecutablePath),
                        UseShellExecute = true,
                        CreateNoWindow = true
                    };

                var process = Process.Start(startInfo);

                if (process == null)
                {
                    _logError("UniqueLootHelper: Failed to start regeneration process");
                    IsRegenerating = false;
                    return false;
                }

                _logMessage(
                        "UniqueLootHelper: Started unique items list regeneration process"
                    );

                // Run asynchronously without blocking UI
                Task.Run(async () =>
                {
                    try
                    {
                        // Wait for process to complete with timeout
                        bool completed = await Task.Run(() => process.WaitForExit(60000)); // 60 second timeout

                        if (completed)
                        {
                            _logMessage(
                                $"UniqueLootHelper: Regeneration process completed with exit code {process.ExitCode}"
                            );

                            // Small delay to ensure file is written
                            await Task.Delay(500);

                            // Reload the list after generation
                            LoadUniqueItemsList();
                        }
                        else
                        {
                            _logError(
                                "UniqueLootHelper: Regeneration process timed out after 60 seconds"
                            );
                            try
                            {
                                process.Kill();
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logError(
                            $"UniqueLootHelper: Error waiting for regeneration process: {ex.Message}"
                        );
                    }
                    finally
                    {
                        IsRegenerating = false;
                        process.Dispose();
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                _logError(
                    $"UniqueLootHelper: Failed to regenerate unique items list: {ex.Message}"
                );
                IsRegenerating = false;
                return false;
            }
        }

        /// <summary>
        /// Gets a sorted list of unique item names for display
        /// </summary>
        public List<string> GetSortedItemNames()
        {
            return _uniqueItemsList.Keys.OrderBy(name => name).ToList();
        }

        /// <summary>
        /// Gets art path by item name
        /// </summary>
        public bool TryGetArtPathByName(string itemName, out string? artPath)
        {
            return _uniqueItemsList.TryGetValue(itemName, out artPath);
        }

        /// <summary>
        /// Gets item name by art path
        /// </summary>
        public bool TryGetItemNameByArtPath(string artPath, out string? itemName)
        {
            foreach (KeyValuePair<string, string> kvp in _uniqueItemsList)
            {
                if (kvp.Value.Equals(artPath, StringComparison.OrdinalIgnoreCase))
                {
                    itemName = kvp.Key;
                    return true;
                }
            }

            itemName = null;
            return false;
        }

        /// <summary>
        /// Searches for items matching the search term
        /// </summary>
        public List<KeyValuePair<string, string>> SearchItems(string searchTerm)
        {
            return string.IsNullOrWhiteSpace(searchTerm)
                ? []
                : [.. _uniqueItemsList
                .Where(kvp =>
                    kvp.Key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    || kvp.Value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                )
                .OrderBy(kvp => kvp.Key)];
        }
    }
}
