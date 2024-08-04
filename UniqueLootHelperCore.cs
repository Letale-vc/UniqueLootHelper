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

    public class CustomItemData
    {
        public Entity WorldEntity;
        public UniqueItemSettings UniqueItemSettings;

        public bool IsCorrupted;
        public Element Label;

        public long LabelAddress { get; set; }

        public Vector2 Location { get; set; }
        public CustomItemData(Entity worldEntity, Element label, UniqueItemSettings uniqueItemSettings)
        {
            WorldEntity = worldEntity;
            UniqueItemSettings = uniqueItemSettings;
            Label = label;
            LabelAddress = label.Address;
            Location = worldEntity.GridPosNum;
            if (worldEntity.TryGetComponent<WorldItem>(out var worldItem))
            {
                if (worldItem.ItemEntity.TryGetComponent<Base>(out var @base))
                {
                    IsCorrupted = @base.isCorrupted;
                }
            }
        }


    }
    public class UniqueItemSettings
    {
        public string ArtPath;
        public bool LineDrawMap;
        public bool LineDrawWorld;
        public string Label;
        public bool DrawIsCorrupted = true;
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
        private string PathArtFile
        {
            get { return Path.Combine(ConfigDirectory, FILE_ART_NAME); }
        }
        private readonly HashSet<CustomItemData> _drawingList = [];

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
            if (File.Exists(PathArtFile)) return;
            File.WriteAllText(PathArtFile, JsonConvert.SerializeObject(new Dictionary<string, UniqueItemSettings>(), Formatting.Indented));
            LogMessage("UniqueLootHelper: Created new file for unique art");
        }

        private Dictionary<string, UniqueItemSettings> GetUniqueArtFromFile()
        {

            if (!File.Exists(PathArtFile)) CreateUniqueArtFile();
            try
            {
                var uniqueArtItemList = JsonConvert.DeserializeObject<Dictionary<string, UniqueItemSettings>>(File.ReadAllText(PathArtFile));
                return uniqueArtItemList;
            }
            catch (Exception)
            {
                File.Move(PathArtFile, PathArtFile + ".bak");
                CreateUniqueArtFile();
                return [];
            }


        }
        public override void Dispose()
        {
            SaveUniquesArtToFile();
            base.Dispose();
        }
        private void SaveUniquesArtToFile()
        {
            if (!File.Exists(PathArtFile))
                CreateUniqueArtFile();
            File.WriteAllText(PathArtFile, JsonConvert.SerializeObject(_cashUniqueArtWork, Formatting.Indented));
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
            ImGui.SameLine();
            ImGui.Checkbox("Draw line on world", ref _tempUniqueItemSettings.LineDrawWorld);
            ImGui.SameLine();
            ImGui.Checkbox("Draw is corrupted", ref _tempUniqueItemSettings.DrawIsCorrupted);
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

                    LogMessage($"UniqueLootHelper: Removed {uniqueArtItem.Key} from unique list");

                }
                ImGui.SameLine();
                if (ImGui.Button($"Delete##{uniqueArtItem.Key}"))
                {
                    _cashUniqueArtWork.Remove(uniqueArtItem.Key);

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
                var renderItem = hoverItem.Item.GetComponent<RenderItem>();
                if (renderItem == null) return;
                ImGui.SetClipboardText(renderItem.ResourcePath);
                LogMessage($"UniqueLootHelper: Copied {renderItem.ResourcePath} to clipboard");

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
            if (Settings.WorldMapDrawing)
            {
                DrawLinesWorld();
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
        private void DrawLinesWorld()
        {
            Entity player = GameController?.Player;
            if (player == null) return;
            Vector2 playerPos = GameController.IngameState.Data.GetGridScreenPosition(player.GridPosNum);
            var filterList = _drawingList.Where(x => x.UniqueItemSettings.LineDrawWorld == true);
            foreach (var item in filterList)
            {
                Vector2 itemPos = GameController.IngameState.Data.GetGridScreenPosition(item.Location);
                Graphics.DrawLine(
                       playerPos,
                       itemPos,
                        Settings.WorldMapLineThickness,
                        Settings.WorldMapLineColor
                    );
            }

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
            _largeMap = GameController?.IngameState?.IngameUi?.Map?.LargeMap;
            if (GameController?.Area?.CurrentArea?.IsHideout == true || GameController?.Area?.CurrentArea?.IsTown == true)
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

                        if (renderItem == null) continue;

                        var renderArtPath = renderItem.ResourcePath;
                        string[] pathArray = [renderArtPath, renderArtPath + ".dds", renderItem.ResourcePath.Replace(".dds", "")];
                        if (pathArray.Any(_cashUniqueArtWork.ContainsKey))
                        {
                            var item = new CustomItemData(itemInfo.Entity, itemInfo.Label, _cashUniqueArtWork[pathArray.First(_cashUniqueArtWork.ContainsKey)]);
                            _drawingList.Add(item);
                        }
                    }
                }
            }
            if (Settings.UseCorruptedFilter)
            {
                var itemsToRemove = new List<CustomItemData>();
                foreach (var drawItem in _drawingList)
                {
                    LogMessage($"{drawItem.IsCorrupted}");
                    if (drawItem.UniqueItemSettings.DrawIsCorrupted == false && drawItem.IsCorrupted == true)
                    {
                        itemsToRemove.Add(drawItem);
                    }
                }
                foreach (var item in itemsToRemove)
                {
                    _drawingList.Remove(item);
                }
            }
            return null;
        }
    }
}