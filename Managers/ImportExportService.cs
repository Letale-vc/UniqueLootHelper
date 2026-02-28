using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace UniqueLootHelper.Managers;

/// <summary>
///     Manages import and export of unique item configurations
/// </summary>
public class ImportExportService
{
    private readonly Action<string> _logError;
    private readonly Action<string> _logMessage;

    public ImportExportService(Action<string> logMessage, Action<string> logError)
    {
        _logMessage = logMessage;
        _logError = logError;
    }

    /// <summary>
    ///     Exports configuration to base64-encoded JSON
    /// </summary>
    /// <param name="configuration">The configuration to export</param>
    /// <param name="copyToClipboard">Whether to copy the result to clipboard</param>
    /// <returns>Base64-encoded JSON string</returns>
    public string Export(Dictionary<string, UniqueItemSettings> configuration, bool copyToClipboard = true)
    {
        string jsonStr = JsonConvert.SerializeObject(configuration, Formatting.None);
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonStr));

        if (copyToClipboard)
        {
            Clipboard.SetClipboardText(base64);
            _logMessage(
                $"UniqueLootHelper: Exported {configuration.Count} items to clipboard"
            );
        }

        return base64;
    }


    /// <summary>
    ///     Imports configuration from base64-encoded JSON
    /// </summary>
    /// <param name="base64Data">Base64-encoded JSON string</param>
    /// <returns>Imported configuration dictionary, or null if import failed</returns>
    public Dictionary<string, UniqueItemSettings>? Import(string base64Data)
    {
        if (string.IsNullOrEmpty(base64Data))
        {
            _logError("UniqueLootHelper: Import data is empty");
            return null;
        }

        try
        {
            string jsonStr = Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
            var imported = JsonConvert.DeserializeObject<Dictionary<string, UniqueItemSettings>>(jsonStr);

            if (imported == null || imported.Count == 0)
            {
                _logError("UniqueLootHelper: Imported data is empty or invalid");
                return null;
            }

            _logMessage($"UniqueLootHelper: Imported {imported.Count} unique items");
            return imported;
        }
        catch (FormatException ex)
        {
            _logError($"UniqueLootHelper: Invalid base64 format: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logError($"UniqueLootHelper: Invalid JSON format: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            _logError($"UniqueLootHelper: Failed to import configuration: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Validates imported configuration
    /// </summary>
    public bool ValidateConfiguration(Dictionary<string, UniqueItemSettings> configuration)
    {
        if (configuration == null)
        {
            return false;
        }

        foreach (var kvp in configuration)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                _logError("UniqueLootHelper: Configuration contains empty key");
                return false;
            }

            if (kvp.Value == null)
            {
                _logError($"UniqueLootHelper: Configuration for '{kvp.Key}' is null");
                return false;
            }
        }

        return true;
    }

}
