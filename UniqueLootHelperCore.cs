using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RectangleF = SharpDX.RectangleF;
using Vector2 = System.Numerics.Vector2;

namespace UniqueLootHelper
{

    public class CustomItemData(Entity worldEntity, Element label, UniqueItemSettings uniqueItemSettings)
    {

        public UniqueItemSettings UniqueItemSettings = uniqueItemSettings;

        public Element Label = label;

        public long LabelAddress { get; set; } = label.Address;

        public Vector2 Location { get; set; } = worldEntity.GridPosNum;


    }
    public class UniqueItemSettings
    {
        public string ArtPath;
        public bool LineDrawMap;
        public string Label;
        public UniqueItemSettings()
        {
            ArtPath = "";
            Label = "";
            LineDrawMap = false;
        }
    }

    public class UniqueLootHelperCore : BaseSettingsPlugin<Settings>
    {
        public static Graphics _graphics;
        private UniqueItemSettings _tempUniqueItemSettings = new();
        private const string FILE_ART_NAME = "UniquesArtworks.json";
        private string _pathArtFile
        {
            get { return Path.Combine(ConfigDirectory, FILE_ART_NAME); }
        }
        private HashSet<CustomItemData> _drawingList = [];

        private Dictionary<string, UniqueItemSettings> _cashUniqueArtWork = [];
        private Element _largeMap;


        public override bool Initialise()
        {
            Name = "UniqueLootHelper";
            _cashUniqueArtWork = GetUniqueArtFromFile();

            return base.Initialise();
        }
        private void CreateUniqueArtFile()
        {
            if (File.Exists(_pathArtFile)) return;
            File.WriteAllText(_pathArtFile, JsonConvert.SerializeObject(new Dictionary<string, UniqueItemSettings>(), Formatting.Indented));
            LogMessage("UniqueLootHelper: Created new file for unique art");
        }

        private Dictionary<string, UniqueItemSettings> GetUniqueArtFromFile()
        {

            if (!File.Exists(_pathArtFile)) CreateUniqueArtFile();
            try
            {
                var uniqueArtItemList = JsonConvert.DeserializeObject<Dictionary<string, UniqueItemSettings>>(File.ReadAllText(_pathArtFile));
                return uniqueArtItemList;
            }
            catch (Exception)
            {
                File.Move(_pathArtFile, _pathArtFile + ".bak");
                CreateUniqueArtFile();
                return [];
            }


        }
        private void SaveUniquesArtToFile()
        {
            if (!File.Exists(_pathArtFile))
                CreateUniqueArtFile();
            File.WriteAllText(_pathArtFile, JsonConvert.SerializeObject(_cashUniqueArtWork, Formatting.Indented));
            LogMessage("UniqueLootHelper: Saved unique art to file");
        }


        public override void DrawSettings()
        {
            if (ImGui.Button("Open Config Folder"))
            {
                Process.Start("explorer.exe", ConfigDirectory);
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            base.DrawSettings();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Text("Add new unique to list");
            ImGui.InputText("Unique art path", ref _tempUniqueItemSettings.ArtPath, 1024, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.InputText("Unique label", ref _tempUniqueItemSettings.Label, 1024, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.Checkbox("Draw line on map", ref _tempUniqueItemSettings.LineDrawMap);
            if (ImGui.Button("Add Unique"))
            {
                if (!string.IsNullOrEmpty(_tempUniqueItemSettings.ArtPath) && !string.IsNullOrEmpty(_tempUniqueItemSettings.Label))
                {
                    string key = _tempUniqueItemSettings.ArtPath;
                    if (_cashUniqueArtWork.TryGetValue(key, out _))
                    {
                        // Key exists, update the value
                        _cashUniqueArtWork[key] = _tempUniqueItemSettings;
                        LogMessage($"UniqueLootHelper: Updated {key} in unique list");
                    }
                    else
                    {
                        // Key does not exist, add new key-value pair
                        _cashUniqueArtWork.Add(key, _tempUniqueItemSettings);
                        LogMessage($"UniqueLootHelper: Added {key} to unique list");
                    }
                    SaveUniquesArtToFile();
                    _tempUniqueItemSettings = new();

                }
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Text("Uniques list:");
            foreach (var uniqueArtItem in _cashUniqueArtWork)
            {
                ImGui.Text($"{uniqueArtItem.Key} - {uniqueArtItem.Value.Label}");
                ImGui.SameLine();
                if (ImGui.Button($"Edit##{uniqueArtItem.Key}"))
                {
                    _tempUniqueItemSettings = uniqueArtItem.Value;
                    _cashUniqueArtWork.Remove(uniqueArtItem.Key);
                    SaveUniquesArtToFile();
                    LogMessage($"UniqueLootHelper: Removed {uniqueArtItem.Key} from unique list");

                }
                ImGui.SameLine();
                if (ImGui.Button($"Delete##{uniqueArtItem.Key}"))
                {
                    _cashUniqueArtWork.Remove(uniqueArtItem.Key);
                    SaveUniquesArtToFile();
                    LogMessage($"UniqueLootHelper: Removed {uniqueArtItem.Key} from unique list");
                }
            }


        }


        public override void Render()
        {
            if (Input.IsKeyDown(Keys.F7))
            {

                var hoverItem = GameController.Game.IngameState.UIHover.AsObject<HoverItemIcon>();
                if (hoverItem == null) return;
                var modelPath = hoverItem.Item.Path;
                ImGui.SetClipboardText(modelPath);
                LogMessage($"UniqueLootHelper: Copied {modelPath} to clipboard");

            }

            var inGameUi = GameController.Game.IngameState.IngameUi;
            if (!Settings.IgnoreFullscreenPanels && inGameUi.FullscreenPanels.Any(x => x.IsVisible))
            {
                return;
            }

            if (!Settings.IgnoreRightPanels && inGameUi.OpenRightPanel.IsVisible)
            {
                return;
            }
            if (_drawingList.Count != 0)
            {

                foreach (var item in _drawingList)
                {
                    var lab = item.Label.GetClientRect();
                    Graphics.DrawFrame(lab, Settings.OutlineLabelColor, Settings.LabelFrameThickness);

                }
            }

            if (Settings.EnableBoxCountDrawing)
            {
                DrawItemCountInfo();
            }

            if (Settings.EnableMapDrawing || _largeMap.IsVisible)
            {
                DrawLinesMap();
            }


        }
        private void DrawLinesMap()
        {
            var filterList = _drawingList.Where(x => x.UniqueItemSettings.LineDrawMap == true);
            foreach (var item in filterList)
                Graphics.DrawLine(
                    GameController.IngameState.Data.GetGridMapScreenPosition(item.Location),
                    GameController.IngameState.Data.GetGridMapScreenPosition(GameController.Player.GridPosNum),
                    Settings.MapLineThickness,
                    Settings.MapLineColor
                );
        }


        private void DrawItemCountInfo()
        {
            if (_drawingList.Count == 0) return;
            var labelCount = _drawingList.GroupBy(item => item.UniqueItemSettings.Label).ToDictionary(group => group.Key, group => group.Count());

            var posX = Settings.BoxPositionX.Value;
            var posY = Settings.BoxPositionY.Value;
            var hight = labelCount.Count * 20 + 20;
            var rect = new RectangleF(posX, posY, 230, hight);
            Graphics.DrawBox(rect, Settings.BoxBackgroundColor);
            if (Settings.BoxOutline.Value == true)
            {
                Graphics.DrawFrame(rect, Settings.BoxOutlineColor, 2);
            }

            posX += 10;
            posY += 10;


            foreach (var item in labelCount)
            {
                Graphics.DrawText($"{item.Key}: {item.Value}", new Vector2(posX, posY), Settings.BoxTextColor, 12);
                posY += 20;
            }
        }

        public override Job Tick()
        {
            _largeMap = GameController.IngameState.IngameUi.Map.LargeMap;
            if (GameController.Area.CurrentArea.IsHideout || GameController.Area.CurrentArea.IsTown)
            {
                _drawingList.Clear();
                return null;
            }


            var newWorldItems = GameController?.IngameState?.IngameUi?.ItemsOnGroundLabelElement.VisibleGroundItemLabels?.Select(x => new { x.Entity, x.Label })?.ToList() ?? [];

            if (newWorldItems != null)
            {
                var worldItemIds = new HashSet<long>(newWorldItems.Select(x => x.Label.Address));

                _drawingList.RemoveWhere(item => !worldItemIds.Contains(item.LabelAddress));
                var existingItemAddresses = new HashSet<long>(_drawingList.Select(item => item.LabelAddress));
                foreach (var itemInfo in newWorldItems)
                {
                    if (!existingItemAddresses.Contains(itemInfo.Label.Address) && itemInfo.Entity.TryGetComponent<WorldItem>(out var worldItem))
                    {
                        var renderItem = worldItem.ItemEntity.GetComponent<RenderItem>();
                        var renderArtPath = renderItem.ResourcePath;
                        var modelPath = worldItem.ItemEntity.Path;
                        string[] pathArray = [renderArtPath, modelPath, renderArtPath + ".dds"];
                        if (pathArray.Any(_cashUniqueArtWork.ContainsKey))
                        {
                            var item = new CustomItemData(itemInfo.Entity, itemInfo.Label, _cashUniqueArtWork[pathArray.First(_cashUniqueArtWork.ContainsKey)]);
                            _drawingList.Add(item);
                        }
                    }
                }
            }

            return null;
        }
    }
}