using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UniqueLootHelper.Managers
{
    /// <summary>
    ///     Manages sound files and sound playback for item notifications
    /// </summary>
    public class SoundManager
    {
        public const string DefaultWavFile = "default.wav";

        private readonly string _configDirectory;
        private readonly Action<string> _logError;
        private readonly Action<string> _logMessage;
        private readonly Func<string, float, bool> _playSound;
        private readonly Dictionary<long, bool> _soundCache = [];
        private Dictionary<string, string> _soundFiles = [];

        public SoundManager(
            string configDirectory,
            Action<string> logMessage,
            Action<string> logError,
            Func<string, float, bool> playSound
        )
        {
            _configDirectory = configDirectory;
            _logMessage = logMessage;
            _logError = logError;
            _playSound = playSound;
            InitializeDefaultSound();
            ReloadSoundList();
        }

        /// <summary>
        ///     Initializes the default sound file if it doesn't exist
        /// </summary>
        private void InitializeDefaultSound()
        {
            string defaultFilePath = Path.Join(_configDirectory, DefaultWavFile);
            if (!File.Exists(defaultFilePath))
            {
                try
                {
                    using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DefaultWavFile);
                    using FileStream file = File.OpenWrite(defaultFilePath);
                    stream?.CopyTo(file);
                }
                catch (Exception ex)
                {
                    _logError(
                        $"UniqueLootHelper: Failed to create default sound file: {ex.Message}"
                    );
                }
            }
        }

        /// <summary>
        ///     Reloads the list of available sound files from the config directory
        /// </summary>
        public void ReloadSoundList()
        {
            try
            {
                _soundFiles = Directory
                    .EnumerateFiles(_configDirectory, "*.wav")
                    .Select(x => (Path.GetFileNameWithoutExtension(x), x))
                    .DistinctBy(x => x.Item1, StringComparer.InvariantCultureIgnoreCase)
                    .ToDictionary(
                        x => x.Item1,
                        x => x.x,
                        StringComparer.InvariantCultureIgnoreCase
                    );

                foreach (KeyValuePair<string, string> soundFile in _soundFiles)
                {
                    _logMessage(
                        $"UniqueLootHelper: Loaded sound file {soundFile.Value}, key: {soundFile.Key}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logError($"UniqueLootHelper: Failed to reload sound list: {ex.Message}");
            }
        }

        /// <summary>
        ///     Attempts to play a sound for an item if it hasn't been played yet
        /// </summary>
        /// <param name="itemId">The unique ID of the item entity</param>
        /// <param name="label">The label/name of the item (used to find custom sound file)</param>
        /// <param name="volume">The volume to play at</param>
        /// <returns>True if sound was played, false if already cached or failed</returns>
        public bool TryPlaySound(long itemId, string label, float volume)
        {
            if (_soundCache.ContainsKey(itemId))
            {
                return false;
            }

            if (!_soundCache.TryAdd(itemId, true))
            {
                return false;
            }

            string defaultFile = Path.Join(_configDirectory, DefaultWavFile);
            string soundFilePath = _soundFiles.GetValueOrDefault(label, defaultFile);

            if (File.Exists(soundFilePath))
            {
                bool success = _playSound(soundFilePath, volume);
                if (success)
                {
                    _logMessage(
                        $"UniqueLootHelper: Playing sound for {label} from {soundFilePath}"
                    );
                }
                return success;
            }
            _logError($"UniqueLootHelper: Sound file {soundFilePath} not found for {label}");
            return false;
        }

        /// <summary>
        ///     Clears the sound cache (call on area change or manual reset)
        /// </summary>
        public void ClearCache()
        {
            _soundCache.Clear();
        }

        /// <summary>
        ///     Removes items from cache that are no longer on the ground
        /// </summary>
        /// <param name="currentItemIds">IDs of items currently on the ground</param>
        public void CleanupCache(IEnumerable<long> currentItemIds)
        {
            // Optimization: accept HashSet directly, avoid creating intermediate collection
            if (_soundCache.Count == 0)
            {
                return;
            }

            List<uint> keysToRemove = new(_soundCache.Count / 10); // Approximate estimate for capacity

            foreach (uint id in _soundCache.Keys)
            {
                if (!currentItemIds.Contains(id))
                {
                    keysToRemove.Add(id);
                }
            }

            foreach (uint key in keysToRemove)
            {
                _soundCache.Remove(key);
            }
        }

        /// <summary>
        ///     Gets the list of available sound files
        /// </summary>
        public IReadOnlyDictionary<string, string> GetAvailableSounds()
        {
            return _soundFiles;
        }
    }
}
