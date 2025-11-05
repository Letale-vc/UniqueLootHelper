using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ImGuiNET;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using UniqueLootHelper.Managers;
using Vector2 = System.Numerics.Vector2;

namespace UniqueLootHelper
{
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
        private readonly ObjectPool<CustomItemData> _itemDataPool;
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
        private StatisticsManager _statisticsManager;
        private UniqueItemsListManager _uniqueItemsListManager;
        private UniqueItemSettings _tempUniqueItemSettings = new();

        // UI state for item selection
        private string _searchTerm = string.Empty;
        private string _selectedItemName = string.Empty;
        private bool _usePresetMode = false;

        public UniqueLootHelperCore()
        {
            // Initialize object pool
            DefaultObjectPoolProvider poolProvider = new();
            _itemDataPool = poolProvider.Create(new CustomItemDataPoolPolicy());

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

            // Warm-up object pool: pre-create 20 objects
            List<CustomItemData> warmUpItems = new(20);
            for (int i = 0; i < 20; i++)
            {
                warmUpItems.Add(_itemDataPool.Get());
            }
            foreach (CustomItemData item in warmUpItems)
            {
                _itemDataPool.Return(item);
            }
            // Initialize managers
            _configurationManager = new ConfigurationManager(ConfigDirectory, LogMessage, LogError);
            _statisticsManager = new StatisticsManager(ConfigDirectory, LogMessage, LogError);
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
            _statisticsManager.SaveStatistics();
            base.OnUnload();
        }

        public override void OnClose()
        {
            _configurationManager.SaveUniqueArtToFile();
            _statisticsManager.SaveStatistics();
            base.OnClose();
        }

        public override void DrawSettings()
        {
            if (ImGui.Button("Open Config Folder"))
            {
                Process.Start("explorer.exe", ConfigDirectory);
            }
            ImGui.SameLine();
            if (ImGui.Button("Show Statistics"))
            {
                _showStatisticsWindow = !_showStatisticsWindow;
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

            ImGui.InputText("Import/export##ImportExportText", ref _importExportText, 100000);
            if (ImGui.Button("Import##ImportState"))
            {
                Import();
            }
            ImGui.SameLine();
            if (ImGui.Button("Export##ExportState"))
            {
                Export();
            }

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

        private void DrawStatisticsWindow()
        {
            if (!_showStatisticsWindow)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(900, 450), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Unique Items Statistics", ref _showStatisticsWindow))
            {
                ImGui.Text(
                    $"Session Start: {_statisticsManager.Statistics.SessionStartTime:yyyy-MM-dd HH:mm:ss}"
                );
                ImGui.Text(
                    $"Total Items Found This Session: {_statisticsManager.TotalItemsFoundInSession}"
                );
                ImGui.Text(
                    $"Session Duration: {DateTime.Now - _statisticsManager.Statistics.SessionStartTime:hh\\:mm\\:ss}"
                );

                if (ImGui.Button("Reset Session Statistics"))
                {
                    _statisticsManager.ResetSessionStatistics();
                }
                ImGui.SameLine();
                if (ImGui.Button("Reset All Statistics"))
                {
                    _statisticsManager.ResetAllStatistics();
                }
                ImGui.SameLine();
                if (ImGui.Button("Save Statistics"))
                {
                    _statisticsManager.SaveStatistics();
                }

                ImGui.Separator();
                ImGui.Spacing();

                if (
                    ImGui.BeginTable(
                        "StatisticsTable",
                        6,
                        ImGuiTableFlags.Borders
                            | ImGuiTableFlags.RowBg
                            | ImGuiTableFlags.Resizable
                            | ImGuiTableFlags.Sortable
                            | ImGuiTableFlags.ScrollY
                    )
                )
                {
                    ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 200);
                    ImGui.TableSetupColumn("ArtPath", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Total Found", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableSetupColumn("This Session", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableSetupColumn(
                        "First Discovery",
                        ImGuiTableColumnFlags.WidthFixed,
                        140
                    );
                    ImGui.TableSetupColumn("Last Discovery", ImGuiTableColumnFlags.WidthFixed, 140);
                    ImGui.TableHeadersRow();

                    // Optimization: avoid ToList() - iterate directly over IEnumerable
                    IOrderedEnumerable<KeyValuePair<string, ItemStatisticsEntry>> sortedStats =
                        _statisticsManager.Statistics.Statistics.OrderByDescending(x =>
                            x.Value.TotalFound
                        );

                    foreach (KeyValuePair<string, ItemStatisticsEntry> stat in sortedStats)
                    {
                        string artPath = stat.Key;
                        string label =
                            _configurationManager.UniqueArtWork.TryGetValue(
                                artPath,
                                out UniqueItemSettings settings
                            ) && !string.IsNullOrEmpty(settings.Label)
                                ? settings.Label
                                : "N/A";

                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        ImGui.Text(label);

                        ImGui.TableNextColumn();
                        ImGui.Text(artPath);

                        ImGui.TableNextColumn();
                        ImGui.Text(stat.Value.TotalFound.ToString());

                        ImGui.TableNextColumn();
                        ImGui.Text(stat.Value.FoundInCurrentSession.ToString());

                        ImGui.TableNextColumn();
                        ImGui.Text(
                            stat.Value.FirstDiscoveryTime?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"
                        );

                        ImGui.TableNextColumn();
                        ImGui.Text(
                            stat.Value.LastDiscoveryTime?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"
                        );
                    }

                    ImGui.EndTable();
                }
            }
            ImGui.End();
        }

        private void DrawProfilerWindow()
        {
            if (!Settings.ProfilerSettings.Enabled)
            {
                return;
            }

            Profiler.ShowProfilerWindow(ref _showProfilerWindow);
        }

        private void Import()
        {
            Dictionary<string, UniqueItemSettings> imported = _importExportService.Import(
                _importExportText
            );
            if (imported != null && _importExportService.ValidateConfiguration(imported))
            {
                _configurationManager.MergeConfiguration(imported);
            }
        }

        public void Export()
        {
            _importExportText = _importExportService.Export(
                _configurationManager.GetAllConfigurations()
            );
        }

        public override void Render()
        {
            // Start total profiler at the VERY beginning to measure everything
            if (Settings.ProfilerSettings.Enabled)
            {
                _profilerTotal.Restart();
            }

            try
            {
                // Profile: UI operations (windows, hotkeys, panel checks)
                if (Settings.ProfilerSettings.Enabled)
                {
                    _profilerUI.Restart();
                }

                DrawStatisticsWindow();
                DrawProfilerWindow();

                if (Input.IsKeyDown(Keys.F7))
                {
                    HoverItemIcon hoverItem =
                        GameController.Game.IngameState.UIHover.AsObject<HoverItemIcon>();
                    if (hoverItem == null)
                    {
                        return;
                    }
                    RenderItem renderItem = hoverItem.Item.GetComponent<RenderItem>();
                    if (renderItem == null)
                    {
                        return;
                    }
                    ImGui.SetClipboardText(renderItem.ResourcePath);
                    LogMessage($"UniqueLootHelper: Copied {renderItem.ResourcePath} to clipboard");
                }

                IngameUIElements inGameUi = GameController.Game.IngameState.IngameUi;

                // Optimization: avoid Any() with predicate - use direct iteration to avoid delegate allocation
                if (!Settings.IgnoreFullscreenPanels)
                {
                    bool hasVisiblePanel = false;
                    foreach (Element panel in inGameUi.FullscreenPanels)
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

                Entity player = GameController?.Player;
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

                List<CustomItemData> groundItems = _groundItems.Value;

                if (Settings.ProfilerSettings.Enabled)
                {
                    _profilerGetItems.Stop();
                    _profilerFiltering.Restart();
                }

                List<string> countList = new(groundItems.Count / 2);

                // Optimization: Cache dictionary reference to avoid repeated property access
                IReadOnlyDictionary<string, UniqueItemSettings> uniqueArtWork =
                    _configurationManager.UniqueArtWork;

                foreach (CustomItemData item in groundItems)
                {
                    // Normalize resource path to .dds for lookup
                    string resourcePath = item.ResourcePath;
                    string normalizedPath = resourcePath.Replace(".dds", "") + ".dds";

                    if (
                        !uniqueArtWork.TryGetValue(
                            normalizedPath,
                            out UniqueItemSettings uniqueSettings
                        )
                    )
                    {
                        continue;
                    }

                    if (!uniqueSettings.DrawIsCorrupted && item.IsCorrupted)
                    {
                        continue;
                    }

                    // Use normalizedPath as matchedKey
                    string matchedKey = normalizedPath;

                    // Profile: Statistics recording
                    if (Settings.ProfilerSettings.Enabled)
                    {
                        _profilerStatistics.Start();
                    }

                    _statisticsManager.TryRecordItemFound(item.Id, matchedKey);

                    if (Settings.ProfilerSettings.Enabled)
                    {
                        _profilerStatistics.Stop();
                    }

                    if (uniqueSettings.DrawLabelInBox)
                    {
                        string label = string.IsNullOrEmpty(uniqueSettings.Label)
                            ? item.Entity.RenderName ?? "Unknown Item"
                            : uniqueSettings.Label;
                        countList.Add(label);
                    }

                    if (
                        Settings.SoundNotificationSettings.Enabled
                        && uniqueSettings.PlayValuableSound
                    )
                    {
                        _soundManager.TryPlaySound(
                            item.Id,
                            uniqueSettings.Label,
                            Settings.SoundNotificationSettings.Volume
                        );
                    }

                    // Profile: Drawing operations
                    if (Settings.ProfilerSettings.Enabled)
                    {
                        _profilerDrawing.Start();
                    }

                    if (
                        uniqueSettings.LineDrawMap
                        && Settings.MapDrawingSettings.EnableMapDrawing
                        && GameController.IngameState.IngameUi.Map.LargeMap.IsVisible
                    )
                    {
                        Vector2 itemMapPos =
                            GameController.IngameState.Data.GetGridMapScreenPosition(item.Location);
                        Vector2 playerMapPos =
                            GameController.IngameState.Data.GetGridMapScreenPosition(
                                player.GridPosNum
                            );
                        _itemDrawingManager.DrawMapLine(itemMapPos, playerMapPos);
                    }

                    if (Settings.MapDrawingSettings.WorldMapDrawing && uniqueSettings.LineDrawWorld)
                    {
                        Vector2 itemWorldPos =
                            GameController.IngameState.Data.GetGridScreenPosition(item.Location);
                        Vector2 playerWorldPos =
                            GameController.IngameState.Data.GetGridScreenPosition(
                                player.GridPosNum
                            );
                        _itemDrawingManager.DrawWorldLine(itemWorldPos, playerWorldPos);
                    }

                    _itemDrawingManager.DrawLabelOutline(item.Element, uniqueSettings);

                    if (!item.IsIdentified)
                    {
                        string labelText =
                            uniqueSettings.Label ?? item.Entity.RenderName ?? "Unknown Item";
                        _itemDrawingManager.DrawLabelName(
                            item.Element,
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

                if (Settings.ProfilerSettings.Enabled)
                {
                    _profilerFiltering.Stop();
                }

                ImGui.End();

                if (Settings.BoxSettings.EnableBoxCountDrawing)
                {
                    _itemDrawingManager.DrawItemCountBox(countList);
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
            _statisticsManager.ClearSessionCache();
            _statisticsManager.SaveStatistics();
        }

        private List<CustomItemData> GetItemsOnGround(List<CustomItemData> previousValue)
        {
            List<ItemsOnGroundLabelElement.VisibleGroundItemDescription> labelsOnGround =
                GameController
                    .IngameState
                    .IngameUi
                    .ItemsOnGroundLabelElement
                    .VisibleGroundItemLabels;

            // Optimization 1: Pre-allocate with exact capacity
            List<CustomItemData> result = new(labelsOnGround.Count);

            // Optimization 2: Build dictionary only if we have previous data, avoid DistinctBy
            Dictionary<(long?, long?), CustomItemData> prevDict = null;
            if (previousValue.Count > 0)
            {
                prevDict = new Dictionary<(long?, long?), CustomItemData>(previousValue.Count);

                foreach (CustomItemData item in previousValue)
                {
                    (long?, long?) key = (item.Element?.Address, item.Entity?.Address);
                    // Skip duplicates without using DistinctBy (just overwrite)
                    prevDict[key] = item;
                }
            }

            // Optimization 3: Process items
            foreach (
                ItemsOnGroundLabelElement.VisibleGroundItemDescription description in labelsOnGround
            )
            {
                if (!description.Entity.TryGetComponent(out WorldItem worldItem))
                {
                    continue;
                }

                if (worldItem.ItemEntity is not { IsValid: true } groundItemEntity)
                {
                    continue;
                }

                CustomItemData customItem = null;

                // Try to reuse existing item data
                if (prevDict != null)
                {
                    prevDict.TryGetValue(
                        (description.Label?.Address, groundItemEntity.Address),
                        out customItem
                    );
                }

                // Create new item if not found in cache
                if (customItem == null)
                {
                    customItem = new CustomItemData(
                        groundItemEntity,
                        description.Label,
                        description.Entity.GridPosNum
                    );
                }

                result.Add(customItem);
            }

            return result;
        }
    }
}
