using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Encoding = System.Text.Encoding;
using RectangleF = SharpDX.RectangleF;
using Vector2 = System.Numerics.Vector2;

namespace UniqueLootHelper
{

    public class CustomItemData
    {
        public readonly uint Id;
        public readonly Element Element;
        public readonly Entity Entity;
        public readonly bool IsCorrupted;
        public readonly bool IsIdentified;
        public readonly string ResourcePath = string.Empty;
        public RectangleF ClientRect;
        public Vector2 Location;
        public CustomItemData(Entity entity, Element element, Vector2 location)
        {
            Id = entity.Id;
            Entity = entity;
            Element = element;
            Location = location;

            if (entity.TryGetComponent<RenderItem>(out RenderItem renderItem))
            {
                ResourcePath = renderItem.ResourcePath;
            }
            if (entity.TryGetComponent<Base>(out Base @base))
            {
                IsCorrupted = @base.isCorrupted;
            }
            if (entity.TryGetComponent<Mods>(out Mods mods))
            {
                IsIdentified = mods.Identified;
            }

        }
    }
    public class UniqueItemSettings
    {
        public string ArtPath = "", Label = "";
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
        private const string FileArtName = "UniquesArtworks.json";
        private const string FileStatisticsName = "Statistics.json";
        public const string DefaultWav = "default.wav";
        public static Graphics _graphics;
        private readonly CachedValue<List<CustomItemData>> _groundItems;
        private readonly Dictionary<uint, bool> _soundCache = [];
        private readonly HashSet<uint> _statisticsCache = [];
        private Dictionary<string, UniqueItemSettings> _cacheUniqueArtWork = [];
        private ItemStatistics _itemStatistics = new();
        private string _importExportText = string.Empty;
        private Dictionary<string, string> _soundFiles = [];
        private UniqueItemSettings _tempUniqueItemSettings = new();
        private bool _showStatisticsWindow = false;

        public UniqueLootHelperCore()
        {
            _groundItems = new FrameCache<List<CustomItemData>>(CacheUtils.RememberLastValue(GetItemsOnGround, new List<CustomItemData>()));
        }
        private string PathArtFile => Path.Combine(ConfigDirectory, FileArtName);
        private string PathStatisticsFile => Path.Combine(ConfigDirectory, FileStatisticsName);

        public override bool Initialise()
        {
            Name = "UniqueLootHelper";
            _cacheUniqueArtWork = GetUniqueArtFromFile();
            _itemStatistics = LoadStatistics();

            Settings.SoundNotificationSettings.ResetEntityNotificationFlags.OnPressed += () =>
            {
                _soundCache.Clear();
            };
            Settings.SoundNotificationSettings.OpenConfigDirectory.OnPressed += () =>
            {
                Process.Start("explorer.exe", ConfigDirectory);
            };
            Settings.SoundNotificationSettings.ReloadSoundList.OnPressed += ReloadSoundList;

            ReloadSoundList();
            return base.Initialise();
        }
        private void CreateUniqueArtFile()
        {
            if (File.Exists(PathArtFile))
            {
                return;
            }
            File.WriteAllText(PathArtFile, JsonConvert.SerializeObject(new Dictionary<string, UniqueItemSettings>(), Formatting.Indented));
            LogMessage("UniqueLootHelper: Created new file for unique art");
        }
        private void ReloadSoundList()
        {
            string defaultFilePath = Path.Join(ConfigDirectory, DefaultWav);
            if (!File.Exists(defaultFilePath))
            {
                using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DefaultWav);
                using FileStream file = File.OpenWrite(defaultFilePath);
                if (stream != null)
                {
                    stream.CopyTo(file);
                }
            }

            _soundFiles = Directory.EnumerateFiles(ConfigDirectory, "*.wav")
                .Select(x => (Path.GetFileNameWithoutExtension(x), x))
                .DistinctBy(x => x.Item1, StringComparer.InvariantCultureIgnoreCase)
                .ToDictionary(x => x.Item1, x => x.x, StringComparer.InvariantCultureIgnoreCase);

            foreach (var soundFile in _soundFiles)
            {
                LogMessage($"UniqueLootHelper: Loaded sound file {soundFile.Value}, key: {soundFile.Key}");
            }
        }

        private Dictionary<string, UniqueItemSettings> GetUniqueArtFromFile()
        {

            if (!File.Exists(PathArtFile))
            {
                CreateUniqueArtFile();
            }
            try
            {
                Dictionary<string, UniqueItemSettings> uniqueArtItemList = JsonConvert.DeserializeObject<Dictionary<string, UniqueItemSettings>>(File.ReadAllText(PathArtFile));
                return uniqueArtItemList;
            }
            catch (Exception)
            {
                File.Move(PathArtFile, PathArtFile + ".bak");
                CreateUniqueArtFile();
                return [];
            }
        }

        private ItemStatistics LoadStatistics()
        {
            if (!File.Exists(PathStatisticsFile))
            {
                return new ItemStatistics();
            }

            try
            {
                string json = File.ReadAllText(PathStatisticsFile);
                var statistics = JsonConvert.DeserializeObject<ItemStatistics>(json);
                if (statistics != null)
                {
                    statistics.ResetSessionStatistics();
                    return statistics;
                }
                return new ItemStatistics();
            }
            catch (Exception ex)
            {
                LogError($"UniqueLootHelper: Failed to load statistics: {ex.Message}");
                return new ItemStatistics();
            }
        }

        private void SaveStatistics()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_itemStatistics, Formatting.Indented);
                File.WriteAllText(PathStatisticsFile, json);
                LogMessage("UniqueLootHelper: Saved statistics to file");
            }
            catch (Exception ex)
            {
                LogError($"UniqueLootHelper: Failed to save statistics: {ex.Message}");
            }
        }

        public override void OnUnload()
        {
            SaveUniquesArtToFile();
            SaveStatistics();
            base.OnUnload();
        }
        public override void OnClose()
        {
            SaveUniquesArtToFile();
            SaveStatistics();
            base.OnClose();
        }
        private void SaveUniquesArtToFile()
        {
            if (!File.Exists(PathArtFile))
            {
                CreateUniqueArtFile();
            }
            File.WriteAllText(PathArtFile, JsonConvert.SerializeObject(_cacheUniqueArtWork, Formatting.Indented));
            LogMessage("UniqueLootHelper: Saved unique art to file");
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
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.InputText("Import/export##ImportExportText", ref _importExportText, 10240);
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
            ImGui.InputText("Unique art path", ref _tempUniqueItemSettings.ArtPath, 1024, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.InputText("Unique label", ref _tempUniqueItemSettings.Label, 1024, ImGuiInputTextFlags.EnterReturnsTrue);
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
                if (!string.IsNullOrEmpty(_tempUniqueItemSettings.ArtPath) && !string.IsNullOrEmpty(_tempUniqueItemSettings.Label))
                {
                    string key = _tempUniqueItemSettings.ArtPath;
                    if (_cacheUniqueArtWork.TryGetValue(key, out _))
                    {
                        // Key exists, update the value
                        _cacheUniqueArtWork[key] = _tempUniqueItemSettings;
                        LogMessage($"UniqueLootHelper: Updated {key} in unique list");
                    }
                    else
                    {
                        // Key does not exist, add new key-value pair
                        _cacheUniqueArtWork.Add(key, _tempUniqueItemSettings);
                        LogMessage($"UniqueLootHelper: Added {key} to unique list");
                    }

                    _tempUniqueItemSettings = new UniqueItemSettings();

                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Text("Uniques list:");
            foreach (KeyValuePair<string, UniqueItemSettings> uniqueArtItem in _cacheUniqueArtWork)
            {
                ImGui.Text($"{uniqueArtItem.Key} - {uniqueArtItem.Value.Label}");
                ImGui.SameLine();
                if (ImGui.Button($"Edit##{uniqueArtItem.Key}"))
                {
                    _tempUniqueItemSettings = uniqueArtItem.Value;

                    LogMessage($"UniqueLootHelper: Editing {uniqueArtItem.Key} from unique list");
                }

                ImGui.SameLine();

                if (ImGui.Button($"Delete##{uniqueArtItem.Key}"))
                {
                    _cacheUniqueArtWork.Remove(uniqueArtItem.Key);

                    LogMessage($"UniqueLootHelper: Removed {uniqueArtItem.Key} from unique list");
                }
            }


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
                ImGui.Text($"Session Start: {_itemStatistics.SessionStartTime:yyyy-MM-dd HH:mm:ss}");
                ImGui.Text($"Total Items Found This Session: {_itemStatistics.TotalItemsFoundInSession}");
                ImGui.Text($"Session Duration: {DateTime.Now - _itemStatistics.SessionStartTime:hh\\:mm\\:ss}");

                if (ImGui.Button("Reset Session Statistics"))
                {
                    _itemStatistics.ResetSessionStatistics();
                    LogMessage("UniqueLootHelper: Reset session statistics");
                }
                ImGui.SameLine();
                if (ImGui.Button("Reset All Statistics"))
                {
                    _itemStatistics = new ItemStatistics();
                    SaveStatistics();
                    LogMessage("UniqueLootHelper: Reset all statistics");
                }
                ImGui.SameLine();
                if (ImGui.Button("Save Statistics"))
                {
                    SaveStatistics();
                }

                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.BeginTable("StatisticsTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY))
                {
                    ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 200);
                    ImGui.TableSetupColumn("ArtPath", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Total Found", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableSetupColumn("This Session", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableSetupColumn("First Discovery", ImGuiTableColumnFlags.WidthFixed, 140);
                    ImGui.TableSetupColumn("Last Discovery", ImGuiTableColumnFlags.WidthFixed, 140);
                    ImGui.TableHeadersRow();

                    var sortedStats = _itemStatistics.Statistics
                        .OrderByDescending(x => x.Value.TotalFound)
                        .ToList();

                    foreach (var stat in sortedStats)
                    {
                        // stat.Key is ArtPath
                        string artPath = stat.Key;
                        string label = _cacheUniqueArtWork.TryGetValue(artPath, out var settings) && !string.IsNullOrEmpty(settings.Label)
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
                        ImGui.Text(stat.Value.FirstDiscoveryTime?.ToString("yyyy-MM-dd HH:mm") ?? "N/A");

                        ImGui.TableNextColumn();
                        ImGui.Text(stat.Value.LastDiscoveryTime?.ToString("yyyy-MM-dd HH:mm") ?? "N/A");
                    }

                    ImGui.EndTable();
                }
            }
            ImGui.End();
        }

        private void Import()
        {
            if (string.IsNullOrEmpty(_importExportText))
            {
                LogError("UniqueLootHelper: Import text is empty.");
                return;
            }
            string jsonStr = Encoding.UTF8.GetString(Convert.FromBase64String(_importExportText));

            Dictionary<string, UniqueItemSettings> import = JsonConvert.DeserializeObject<Dictionary<string, UniqueItemSettings>>(jsonStr);
            _cacheUniqueArtWork = _cacheUniqueArtWork.Concat(import).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Value);
            LogMessage($"UniqueLootHelper: Imported {import.Count} unique items from clipboard.");
        }

        public void Export()
        {
            string jsonStr = JsonConvert.SerializeObject(_cacheUniqueArtWork);
            _importExportText = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonStr));
            Clipboard.SetClipboardText(_importExportText);
            LogMsg($"Copy to clipboard: {_importExportText}");
        }

        public override void Render()
        {
            DrawStatisticsWindow();

            if (Input.IsKeyDown(Keys.F7))
            {

                HoverItemIcon hoverItem = GameController.Game.IngameState.UIHover.AsObject<HoverItemIcon>();
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

            if (!Settings.IgnoreFullscreenPanels && inGameUi.FullscreenPanels.Any(x => x.IsVisible))
            {
                return;
            }

            if (!Settings.IgnoreRightPanels && inGameUi.OpenRightPanel.IsVisible)
            {
                return;
            }

            Entity player = GameController?.Player;
            ImGui.Begin("lmao",
                ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav);
            ImDrawListPtr drawList = ImGui.GetBackgroundDrawList();
            List<string> countList = new();

            foreach (CustomItemData item in _groundItems.Value)
            {
                string[] pathArray = [item.ResourcePath, item.ResourcePath + ".dds", item.ResourcePath.Replace(".dds", "")];

                if (!pathArray.Any(_cacheUniqueArtWork.ContainsKey))
                {
                    continue;
                }

                string matchedKey = pathArray.First(_cacheUniqueArtWork.ContainsKey);
                UniqueItemSettings uniqueSettings = _cacheUniqueArtWork[matchedKey];

                if (!uniqueSettings.DrawIsCorrupted && item.IsCorrupted)
                {
                    continue;
                }

                // Record statistics
                if (!_statisticsCache.Contains(item.Id))
                {
                    _statisticsCache.Add(item.Id);
                    _itemStatistics.RecordItemFound(matchedKey);
                }

                if (uniqueSettings.DrawLabelInBox)
                {
                    if (string.IsNullOrEmpty(uniqueSettings.Label))
                    {
                        countList.Add(item.Entity.RenderName ?? "Unknown Item");

                    }
                    else
                    {
                        countList.Add(uniqueSettings.Label);
                    }

                }

                if (Settings.SoundNotificationSettings.Enabled && uniqueSettings.PlayValuableSound)
                {

                    if (!_soundCache.ContainsKey(item.Id))
                    {
                        if (_soundCache.TryAdd(item.Id, true))
                        {
                            string defaultFile = Path.Join(ConfigDirectory, DefaultWav);
                            string soundFilePath = _soundFiles.GetValueOrDefault(uniqueSettings.Label, defaultFile);

                            if (File.Exists(soundFilePath))
                            {
                                GameController.SoundController.PlaySound(soundFilePath, Settings.SoundNotificationSettings.Volume);
                                LogMessage($"UniqueLootHelper: Playing sound for {uniqueSettings.Label} from {soundFilePath}");
                            }
                            else
                            {
                                LogError($"UniqueLootHelper: Sound file {soundFilePath} not found for {uniqueSettings.Label}");
                            }
                        }
                    }
                }

                if (uniqueSettings.LineDrawMap && Settings.MapDrawingSettings.EnableMapDrawing && GameController.IngameState.IngameUi.Map.LargeMap.IsVisible)
                {
                    Vector2 itemMapPost = GameController.IngameState.Data.GetGridMapScreenPosition(item.Location);
                    Vector2 playerMapPost = GameController.IngameState.Data.GetGridMapScreenPosition(player.GridPosNum);
                    Graphics.DrawLine(
                        itemMapPost,
                        playerMapPost,
                        Settings.MapDrawingSettings.MapLineThickness,
                        Settings.MapDrawingSettings.MapLineColor
                    );
                }

                if (Settings.MapDrawingSettings.WorldMapDrawing && uniqueSettings.LineDrawWorld)
                {
                    Vector2 itemWorldPos = GameController.IngameState.Data.GetGridScreenPosition(item.Location);
                    Vector2 playerWorldPos = GameController.IngameState.Data.GetGridScreenPosition(player.GridPosNum);
                    Graphics.DrawLine(
                        playerWorldPos,
                        itemWorldPos,
                        Settings.MapDrawingSettings.WorldMapLineThickness,
                        Settings.MapDrawingSettings.WorldMapLineColor);
                }

                RectangleF labelFrame = item.Element.GetClientRect();

                if (Settings.LabelDrawingSettings.EnableOutlineLebel && uniqueSettings.DrawLabelOutline)
                {

                    Graphics.DrawFrame(labelFrame, Settings.LabelDrawingSettings.OutlineLabelColor, Settings.LabelDrawingSettings.LabelFrameThickness);
                }

                if (Settings.LabelDrawingSettings.EnableLabelName && uniqueSettings.DrawLabelName && !item.IsIdentified)
                {
                    string text = uniqueSettings.Label ?? item.Entity.RenderName ?? "Unknown Item";
                    Vector2 textSize = Graphics.MeasureText(text);
                    float scale = Math.Min(labelFrame.Width / textSize.X, (labelFrame.Height - 2) / textSize.Y) - 0.2f;
                    ImGui.SetWindowFontScale(scale);
                    Vector2 newTextSize = ImGui.CalcTextSize(text);
                    Vector2 textPosition = labelFrame.Center.ToVector2Num() - newTextSize / 2;
                    Vector2 rectPosition = new(textPosition.X, labelFrame.Top + 1);
                    drawList.AddRectFilled(labelFrame.TopLeft.ToVector2Num(), labelFrame.BottomRight.ToVector2Num(), Settings.LabelDrawingSettings.BackgroundLabel.Value.ToImgui());
                    drawList.AddText(textPosition, Settings.LabelDrawingSettings.LabelTextColor.Value.ToImgui(), text);
                    ImGui.SetWindowFontScale(1);
                }
            }

            ImGui.End();

            if (Settings.BoxSettings.EnableBoxCountDrawing)
            {
                DrawItemCountInfo(countList);
            }
        }

        private void DrawItemCountInfo(List<string> countList)
        {
            if (countList.Count == 0)
            {
                return;
            }
            Dictionary<string, int> labelCount = countList.GroupBy(x => x).ToDictionary(group => group.Key, group => group.Count());
            float posX = Settings.BoxSettings.BoxPositionX.Value;
            float posY = Settings.BoxSettings.BoxPositionY.Value;
            int hight = labelCount.Count * 20 + 20;
            RectangleF rect = new(posX, posY, 230, hight);
            Graphics.DrawBox(rect, Settings.BoxSettings.BoxBackgroundColor);

            if (Settings.BoxSettings.BoxOutline.Value)
            {
                Graphics.DrawFrame(rect, Settings.BoxSettings.BoxOutlineColor, 2);
            }

            posX += 10;
            posY += 10;

            foreach (KeyValuePair<string, int> item in labelCount)
            {
                Graphics.DrawText($"{item.Key}: {item.Value}", new Vector2(posX, posY), Settings.BoxSettings.BoxTextColor);
                posY += 20;
            }
        }
        public override void AreaChange(AreaInstance area)
        {
            _soundCache.Clear();
            _statisticsCache.Clear();
            SaveStatistics();
        }

        private List<CustomItemData> GetItemsOnGround(List<CustomItemData> previousValue)
        {
            Dictionary<(long?, long?), CustomItemData> prevDict = previousValue
                .DistinctBy(x => (x.Entity?.Address, x.Element?.Address))
                .ToDictionary(x => (x.Element?.Address, x.Entity?.Address));
            List<ItemsOnGroundLabelElement.VisibleGroundItemDescription> labelsOnGround = GameController.IngameState.IngameUi.ItemsOnGroundLabelElement.VisibleGroundItemLabels;
            List<CustomItemData> result = new();

            foreach (ItemsOnGroundLabelElement.VisibleGroundItemDescription description in labelsOnGround)
            {
                if (description.Entity.TryGetComponent<WorldItem>(out WorldItem worldItem) &&
                    worldItem.ItemEntity is { IsValid: true } groundItemEntity)
                {

                    CustomItemData customItem = prevDict.GetValueOrDefault((description.Label?.Address, groundItemEntity.Address));

                    if (customItem == null)
                    {
                        customItem = new CustomItemData(groundItemEntity, description.Label, description.Entity.GridPosNum);
                    }

                    result.Add(customItem);
                }
            }

            return result;
        }
    }

}
