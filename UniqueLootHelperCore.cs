using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using UniqueLootHelper.Managers;
using Vector2 = System.Numerics.Vector2;

namespace UniqueLootHelper;

public class UniqueItemSettings
{
    public string ArtPath = "",
        Label = "";
    public bool LineDrawWorld,
        DrawLabelInBox = true,
        DrawLabelOutline = true,
        LineDrawMap,
        DrawLabelName = true,
        DrawIsCorrupted = true,
        PlayValuableSound;
}

public class UniqueLootHelperCore : BaseSettingsPlugin<Settings>
{
    private readonly CachedValue<List<CustomItemData>> _groundItems;
    private readonly Stopwatch _profilerDrawing;
    private readonly Stopwatch _profilerFiltering;

    // Profiler fields
    private readonly Stopwatch _profilerGetItems;
    private readonly Stopwatch _profilerStatistics;
    private readonly Stopwatch _profilerTotal;
    private readonly Stopwatch _profilerUI;

    private ConfigurationManager _configurationManager;
    private ImportExportService _importExportService;
    private string _importExportText = string.Empty;
    private ItemDrawingManager _itemDrawingManager;
    private bool _showProfilerWindow;
    private bool _showStatisticsWindow;
    private SoundManager _soundManager;
    private UniqueItemsListManager _uniqueItemsListManager;
    private UniqueItemSettings _tempUniqueItemSettings = new();

    // UI state for item selection
    private string _searchTerm = string.Empty;
    private string _selectedItemName = string.Empty;
    private bool _usePresetMode = false;
    private readonly List<CustomItemData> _result = [];
    private readonly Dictionary<(long, long), CustomItemData> _prevDict = [];
    private readonly List<string> _countList = [];
    private readonly HashSet<(long, long)> _currentItemIds = [];
    public UniqueLootHelperCore()
    {

        _groundItems = new FrameCache<List<CustomItemData>>(
            CacheUtils.RememberLastValue(GetItemsOnGround, new List<CustomItemData>())
        );

        // Initialize profiler stopwatches
        _profilerGetItems = new Stopwatch();
        _profilerFiltering = new Stopwatch();
        _profilerDrawing = new Stopwatch();
        _profilerStatistics = new Stopwatch();
        _profilerTotal = new Stopwatch();
        _profilerUI = new Stopwatch();
    }

    public override bool Initialise()
    {
        Name = "UniqueLootHelper";

        // Initialize managers
        _configurationManager = new ConfigurationManager(ConfigDirectory, LogMessage, LogError);
        _soundManager = new SoundManager(
            ConfigDirectory,
            LogMessage,
            LogError,
            (path, volume) =>
            {
                GameController.SoundController.PlaySound(path, volume);
                return true;
            }
        );
        _itemDrawingManager = new ItemDrawingManager(() => Graphics, () => Settings);
        _importExportService = new ImportExportService(LogMessage, LogError);
        _uniqueItemsListManager = new UniqueItemsListManager(
            DirectoryFullName,
            LogMessage,
            LogError
        );

        // Setup event handlers
        Settings.SoundNotificationSettings.ResetEntityNotificationFlags.OnPressed += () =>
        {
            _soundManager.ClearCache();
        };
        Settings.SoundNotificationSettings.OpenConfigDirectory.OnPressed += () =>
        {
            Process.Start("explorer.exe", ConfigDirectory);
        };
        Settings.SoundNotificationSettings.ReloadSoundList.OnPressed += () =>
        {
            _soundManager.ReloadSoundList();
        };
        Settings.ProfilerSettings.ShowProfilerWindow.OnPressed += () =>
        {
            _showProfilerWindow = !_showProfilerWindow;
        };

        return base.Initialise();
    }

    public override void OnUnload()
    {
        _configurationManager.SaveUniqueArtToFile();
        base.OnUnload();
    }

    public override void OnClose()
    {
        _configurationManager.SaveUniqueArtToFile();
        base.OnClose();
    }

    public override void DrawSettings()
    {
        if (ImGui.Button("Open Config Folder"))
        {
            Process.Start("explorer.exe", ConfigDirectory);
        }

        ImGui.SameLine();

        // Show regeneration status
        if (_uniqueItemsListManager.IsRegenerating)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
            ImGui.Button("Regenerating...");
            ImGui.PopStyleColor();
        }
        else
        {
            if (ImGui.Button("Regenerate Unique Items List"))
            {
                _uniqueItemsListManager.RegenerateUniqueItemsList();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Import/Export Configuration:");
        ImGui.InputText("##ImportExportText", ref _importExportText, 100000);
        if (ImGui.Button("Paste from Clipboard##PasteImport"))
        {
            try
            {
                string clipboardText = Clipboard.GetClipboardText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    _importExportText = clipboardText;
                    LogMessage("UniqueLootHelper: Pasted text from clipboard");
                }
            }
            catch (Exception ex)
            {
                LogError($"UniqueLootHelper: Failed to paste from clipboard: {ex.Message}");
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Import (Merge)##ImportMerge"))
        {
            Import(merge: true);
        }
        ImGui.SameLine();
        if (ImGui.Button("Import (Replace)##ImportReplace"))
        {
            Import(merge: false);
        }
        ImGui.SameLine();
        if (ImGui.Button("Export##ExportState"))
        {
            Export();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear##ClearImportExport"))
        {
            _importExportText = string.Empty;
        }

        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f),
            "Merge: Add to existing items | Replace: Clear all and import");

        ImGui.Dummy(new Vector2(0, 20));
        base.DrawSettings();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.Text("Add new unique to list");

        // Mode toggle
        if (ImGui.Checkbox("Use Preset Item", ref _usePresetMode))
        {
            _tempUniqueItemSettings = new UniqueItemSettings();
            _selectedItemName = string.Empty;
        }

        ImGui.Spacing();

        if (_usePresetMode)
        {
            DrawPresetItemSelection();
        }
        else
        {
            DrawCustomItemInput();
        }

        ImGui.Spacing();

        // Common settings
        ImGui.Checkbox("Draw line on map", ref _tempUniqueItemSettings.LineDrawMap);
        ImGui.SameLine();
        ImGui.Checkbox("Draw line on world", ref _tempUniqueItemSettings.LineDrawWorld);
        ImGui.SameLine();
        ImGui.Checkbox("Draw outline", ref _tempUniqueItemSettings.DrawLabelOutline);
        ImGui.SameLine();
        ImGui.Checkbox("Draw Label name", ref _tempUniqueItemSettings.DrawLabelName);
        ImGui.SameLine();
        ImGui.Checkbox("Draw label in box", ref _tempUniqueItemSettings.DrawLabelInBox);
        ImGui.SameLine();
        ImGui.Checkbox("Draw is corrupted", ref _tempUniqueItemSettings.DrawIsCorrupted);
        ImGui.SameLine();
        ImGui.Checkbox("Play valuable sound", ref _tempUniqueItemSettings.PlayValuableSound);

        if (ImGui.Button("Add Unique"))
        {
            if (
                _configurationManager.AddOrUpdateUniqueItem(
                    _tempUniqueItemSettings.ArtPath,
                    _tempUniqueItemSettings
                )
            )
            {
                _tempUniqueItemSettings = new UniqueItemSettings();
                _selectedItemName = string.Empty;
                _searchTerm = string.Empty;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("Uniques list:");

        foreach (
            KeyValuePair<
                string,
                UniqueItemSettings
            > uniqueArtItem in _configurationManager.UniqueArtWork
        )
        {
            ImGui.Text($"{uniqueArtItem.Key} - {uniqueArtItem.Value.Label}");
            ImGui.SameLine();
            if (ImGui.Button($"Edit##{uniqueArtItem.Key}"))
            {
                _tempUniqueItemSettings = uniqueArtItem.Value;
                _usePresetMode = false;
                LogMessage($"UniqueLootHelper: Editing {uniqueArtItem.Key} from unique list");
            }

            ImGui.SameLine();

            if (ImGui.Button($"Delete##{uniqueArtItem.Key}"))
            {
                _configurationManager.RemoveUniqueItem(uniqueArtItem.Key);
            }
        }
    }

    private void DrawPresetItemSelection()
    {
        ImGui.Text("Search for unique item:");
        if (ImGui.InputText("##SearchUniqueItem", ref _searchTerm, 256))
        {
            _selectedItemName = string.Empty;
        }

        ImGui.Spacing();

        // Show regeneration status
        if (_uniqueItemsListManager.IsRegenerating)
        {
            ImGui.TextColored(
                new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
                "Regenerating unique items list, please wait..."
            );
            ImGui.Spacing();
        }

        List<KeyValuePair<string, string>> searchResults;

        if (string.IsNullOrWhiteSpace(_searchTerm))
        {
            // Show all items if no search term
            searchResults = _uniqueItemsListManager.UniqueItemsList
                .Take(50)
                .OrderBy(x => x.Key)
                .ToList();
            ImGui.TextColored(
                new Vector4(0.7f, 0.7f, 0.7f, 1.0f),
                $"Showing first 50 of {_uniqueItemsListManager.UniqueItemsList.Count} items..."
            );
        }
        else
        {
            searchResults = _uniqueItemsListManager.SearchItems(_searchTerm);
            ImGui.TextColored(
                new Vector4(0.7f, 0.7f, 0.7f, 1.0f),
                $"Found {searchResults.Count} matching items"
            );
        }

        ImGui.Spacing();

        if (
            ImGui.BeginChild(
                "##UniqueItemsList",
                new Vector2(0, 200),
                ImGuiChildFlags.Border,
                ImGuiWindowFlags.None
            )
        )
        {
            foreach (KeyValuePair<string, string> item in searchResults)
            {
                // item.Key is itemName, item.Value is artPath
                bool isSelected = _selectedItemName == item.Key;
                if (ImGui.Selectable($"{item.Key}##{item.Value}", isSelected))
                {
                    _selectedItemName = item.Key;
                    _tempUniqueItemSettings.ArtPath = item.Value;
                    _tempUniqueItemSettings.Label = item.Key;
                }
            }

            ImGui.EndChild();
        }

        ImGui.Spacing();

        if (!string.IsNullOrEmpty(_selectedItemName))
        {
            ImGui.TextColored(
                new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                $"Selected: {_selectedItemName}"
            );
            ImGui.Text($"Art Path: {_tempUniqueItemSettings.ArtPath}");
        }

        ImGui.Spacing();
        ImGui.InputText(
            "Custom Label (optional)",
            ref _tempUniqueItemSettings.Label,
            1024,
            ImGuiInputTextFlags.EnterReturnsTrue
        );
    }

    private void DrawCustomItemInput()
    {
        ImGui.InputText(
            "Unique art path",
            ref _tempUniqueItemSettings.ArtPath,
            1024,
            ImGuiInputTextFlags.EnterReturnsTrue
        );
        ImGui.InputText(
            "Unique label",
            ref _tempUniqueItemSettings.Label,
            1024,
            ImGuiInputTextFlags.EnterReturnsTrue
        );
    }

    private void DrawProfilerWindow()
    {
        if (!Settings.ProfilerSettings.Enabled)
        {
            return;
        }

        Profiler.ShowProfilerWindow(ref _showProfilerWindow);
    }

    private void Import(bool merge)
    {
        Dictionary<string, UniqueItemSettings>? imported = _importExportService.Import(
            _importExportText
        );
        if (imported != null && _importExportService.ValidateConfiguration(imported))
        {
            if (merge)
            {
                _configurationManager.MergeConfiguration(imported);
                LogMessage($"UniqueLootHelper: Merged {imported.Count} items with existing configuration");
            }
            else
            {
                _configurationManager.ReplaceConfiguration(imported);
                LogMessage($"UniqueLootHelper: Replaced configuration with {imported.Count} items");
            }
        }
    }

    public void Export()
    {
        _importExportText = _importExportService.Export(
            _configurationManager.GetAllConfigurations()
        );

        // Also copy to system clipboard for convenience
        try
        {
            Clipboard.SetClipboardText(_importExportText);
            LogMessage("UniqueLootHelper: Configuration exported and copied to clipboard");
        }
        catch (Exception ex)
        {
            LogError($"UniqueLootHelper: Failed to copy to clipboard: {ex.Message}");
        }
    }

    public override void Render()
    {
        // Start total profiler at the VERY beginning to measure everything
        if (Settings.ProfilerSettings.Enabled)
        {
            _profilerTotal.Restart();
        }
        if (GameController == null || GameController.Game == null || GameController.IngameState == null)
        {
            return;
        }
        try
        {
            // Profile: UI operations (windows, hotkeys, panel checks)
            if (Settings.ProfilerSettings.Enabled)
            {
                _profilerUI.Restart();
            }

            DrawProfilerWindow();

            if (Input.IsKeyDown(Keys.F7))
            {
                var hoverItem =
                    GameController.Game.IngameState.UIHover.AsObject<HoverItemIcon>();
                if (hoverItem == null)
                {
                    return;
                }
                var renderItem = hoverItem.Item.GetComponent<RenderItem>();
                if (renderItem == null)
                {
                    return;
                }
                ImGui.SetClipboardText(renderItem.ResourcePath);
                LogMessage($"UniqueLootHelper: Copied {renderItem.ResourcePath} to clipboard");
            }

            var inGameUi = GameController.Game.IngameState.IngameUi;

            // Optimization: avoid Any() with predicate - use direct iteration to avoid delegate allocation
            if (!Settings.IgnoreFullscreenPanels)
            {
                var hasVisiblePanel = false;
                foreach (var panel in inGameUi.FullscreenPanels)
                {
                    if (panel.IsVisible)
                    {
                        hasVisiblePanel = true;
                        break;
                    }
                }
                if (hasVisiblePanel)
                {
                    return;
                }
            }

            if (!Settings.IgnoreRightPanels && inGameUi.OpenRightPanel.IsVisible)
            {
                return;
            }

            if (Settings.ProfilerSettings.Enabled)
            {
                _profilerUI.Stop();
            }

            var player = GameController?.Player;
            if (player is null) return;
            ImGui.Begin(
                "lmao",
                ImGuiWindowFlags.NoDecoration
                    | ImGuiWindowFlags.NoBackground
                    | ImGuiWindowFlags.NoInputs
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoNav
            );

            // Profile: Getting ground items
            if (Settings.ProfilerSettings.Enabled)
            {
                _profilerGetItems.Restart();
            }

            var groundItems = _groundItems.Value;

            if (Settings.ProfilerSettings.Enabled)
            {
                _profilerGetItems.Stop();
                _profilerFiltering.Restart();
            }
            _countList.Clear();
            _currentItemIds.Clear();
            // List<string> countList = new(groundItems.Count / 2);
            // var currentItemIds = new HashSet<uint>(groundItems.Count);

            // Optimization: Cache dictionary reference to avoid repeated property access
            var uniqueArtWork =
                _configurationManager.UniqueArtWork;

            var drawMapLines =
                Settings.MapDrawingSettings.EnableMapDrawing
                && GameController.IngameState.IngameUi.Map.LargeMap.IsVisible;

            bool drawWorldLines = Settings.MapDrawingSettings.WorldMapDrawing;
            var playerMapPos = Vector2.Zero;
            var playerWorldPos = Vector2.Zero;
            if (drawMapLines)
            {
                playerMapPos =
                    GameController.IngameState.Data.GetGridMapScreenPosition(
                        player.GridPosNum
                    );
            }
            if (drawWorldLines)
            {
                playerWorldPos =
                    GameController.IngameState.Data.GetGridScreenPosition(player.GridPosNum);
            }

            foreach (var item in groundItems)
            {
                _currentItemIds.Add((item.WorldItem.Address, item.Element.Address));

                var normalizedPath = item.NormalizedResourcePath;
                if (string.IsNullOrEmpty(normalizedPath)) continue;


                if (!uniqueArtWork.TryGetValue(normalizedPath, out var uniqueSettings))
                {
                    continue;
                }

                if (!uniqueSettings.DrawIsCorrupted && item.IsCorrupted)
                {
                    continue;
                }

                var rect = !item.ClientRect.IsEmpty ? item.ClientRect : item.Element.GetClientRect();
                // Use normalizedPath as matchedKey
                var matchedKey = normalizedPath;

                // Profile: Statistics recording
                if (Settings.ProfilerSettings.Enabled)
                {
                    _profilerStatistics.Start();
                }

                if (Settings.ProfilerSettings.Enabled)
                {
                    _profilerStatistics.Stop();
                }

                if (uniqueSettings.DrawLabelInBox)
                {
                    var label = string.IsNullOrEmpty(uniqueSettings.Label)
                        ? item.Entity?.RenderName ?? "Unknown Item"
                        : uniqueSettings.Label;
                    _countList.Add(label);
                }

                if (
                    Settings.SoundNotificationSettings.Enabled
                    && uniqueSettings.PlayValuableSound
                )
                {
                    _soundManager.TryPlaySound(
                        (item.WorldItem.Address, item.Element.Address),
                        uniqueSettings.Label,
                        Settings.SoundNotificationSettings.Volume
                    );
                }

                // Profile: Drawing operations
                if (Settings.ProfilerSettings.Enabled)
                {
                    _profilerDrawing.Start();
                }

                if (uniqueSettings.LineDrawMap && drawMapLines)
                {
                    var itemMapPos =
                        GameController?.IngameState.Data.GetGridMapScreenPosition(item.Location);
                    if (itemMapPos == null) continue;

                    _itemDrawingManager.DrawMapLine((Vector2)itemMapPos, playerMapPos);
                }

                if (drawWorldLines && uniqueSettings.LineDrawWorld)
                {
                    var itemWorldPos =
                        GameController?.IngameState.Data.GetGridScreenPosition(item.Location);
                    if (itemWorldPos == null) continue;

                    _itemDrawingManager.DrawWorldLine((Vector2)itemWorldPos, playerWorldPos);
                }

                _itemDrawingManager.DrawLabelOutline(rect, uniqueSettings);

                if (!item.IsIdentified)
                {
                    var labelText =
                        uniqueSettings.Label ?? item.Entity?.RenderName ?? "Unknown Item";
                    _itemDrawingManager.DrawLabelName(
                        rect,
                        labelText,
                            item.IsIdentified,
                            uniqueSettings
                    );
                }

                if (Settings.ProfilerSettings.Enabled)
                {
                    _profilerDrawing.Stop();
                }
            }

            // Cleanup caches using the IDs we saw this frame
            _soundManager.CleanupCache(_currentItemIds);

            if (Settings.ProfilerSettings.Enabled)
            {
                _profilerFiltering.Stop();
            }

            ImGui.End();

            if (Settings.BoxSettings.EnableBoxCountDrawing)
            {
                _itemDrawingManager.DrawItemCountBox(_countList);
            }

            // Record profiler metrics
            if (Settings.ProfilerSettings.Enabled)
            {
                _profilerTotal.Stop();

                // Record metrics for window display
                Profiler.RecordMetrics(
                    _profilerGetItems,
                    _profilerFiltering,
                    _profilerDrawing,
                    _profilerStatistics,
                    _profilerUI,
                    _profilerTotal
                );

                // Reset profilers for next frame
                _profilerGetItems.Reset();
                _profilerFiltering.Reset();
                _profilerDrawing.Reset();
                _profilerStatistics.Reset();
                _profilerUI.Reset();
                _profilerTotal.Reset();
            }
        }
        catch (Exception ex)
        {
            LogError($"UniqueLootHelper: Error in Render: {ex.Message}");
        }
    }

    public override void AreaChange(AreaInstance area)
    {
        _soundManager.ClearCache();
    }
    private List<CustomItemData> GetItemsOnGround(List<CustomItemData> previousValue)
    {
        var labelsOnGround =
            GameController
                .IngameState
                .IngameUi
                .ItemsOnGroundLabelElement
                .VisibleGroundItemLabels;

        _result.Clear();
        _prevDict.Clear();

        foreach (CustomItemData item in previousValue)
        {
            if (item.Element != null && item.Entity != null)
            {
                _prevDict[(item.WorldItem.Address, item.Element.Address)] = item;
            }
        }

        foreach (var description in labelsOnGround)
        {
            if (description.Entity == null) continue;
            if (!description.Entity.TryGetComponent(out WorldItem worldItem)) continue;

            var groundItemEntity = worldItem.ItemEntity;
            if (groundItemEntity == null || !groundItemEntity.IsValid) continue;

            var key = (worldItem.Address, description.Label.Address);
            LogMessage($"UniqueLootHelper: Processing item on ground - Address: {worldItem.Address}, label address: {description.Label.Address}");

            if (_prevDict.TryGetValue(key, out var cachedItem))
            {
                cachedItem.ClientRect = description.ClientRect;
                _result.Add(cachedItem);
            }
            else
            {
                _result.Add(new(worldItem, description.Label, description.Entity.GridPosNum, description.ClientRect));
            }

        }

        foreach (var unusedItem in _prevDict.Values)
        {
            _soundManager.ClearCacheEntry((unusedItem.WorldItem.Address, unusedItem.Element.Address));
        }

        return _result;
    }
}
