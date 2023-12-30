using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
namespace UniqueLootHelper
{
    public class UniqueLootHelperCore : BaseSettingsPlugin<Settings>
    {
        private const string UNIQUESARTWORK_FILE = "UniquesArtworks.txt";
        private HashSet<string> UniquesHashSet;
        private Dictionary<string, int> itemNamesCount = new Dictionary<string, int>();
        public List<SharpDX.RectangleF> drawingList = new List<SharpDX.RectangleF>();

        public override bool Initialise()
        {
            Name = "UniqueLootHelper";
            Settings.RefreshUniquesFile.OnPressed += () => { ReadUniquesArtworkFile(); };
            ReadUniquesArtworkFile();
            return base.Initialise();
        }


        public override void DrawSettings()
        {
            base.DrawSettings();
            if (ImGui.Button("Open Config Folder"))
            {
                Process.Start("explorer.exe", ConfigDirectory);
            }

        }

        private void ReadUniquesArtworkFile()
        {
            var path = Path.Combine(ConfigDirectory, UNIQUESARTWORK_FILE);

            if (File.Exists(path))
            {
                UniquesHashSet = File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#")).ToList().Select(x => x + ".dds").ToHashSet();
            }
            else
                CreateUniquesArtworkFile();
        }

        private void CreateUniquesArtworkFile()
        {

            var path = Path.Combine(ConfigDirectory, UNIQUESARTWORK_FILE);
            if (File.Exists(path)) return;
            using (var streamWriter = new StreamWriter(path, true))
            {
                streamWriter.Write("");
                streamWriter.Close();
            }
        }
        public override void Render()
        {
            foreach (var frame in drawingList)
            {
                Graphics.DrawFrame(frame, Settings.Color, Settings.FrameThickness);
            }

            DrawItemCountInfo();
        }
        private void DrawItemCountInfo()
        {
            if (itemNamesCount.Count == 0) return;
            var posX = Settings.PositionX.Value;
            var posY = Settings.PositionY.Value;
            var hight = itemNamesCount.Count * 20 + 20;
            var rect = new RectangleF(posX, posY, 230, hight);
            Graphics.DrawBox(rect, new Color(0, 0, 0, 200));
            if (Settings.BoxOutline.Value == true)
            {
                Graphics.DrawFrame(rect, Color.White, 2);
            }

            posX += 10;
            posY += 10;
            foreach (var itemNameCount in itemNamesCount)
            {
                Graphics.DrawText($"{itemNameCount.Key}: {itemNameCount.Value}", new Vector2(posX, posY), Color.White, 15);
                posY += 20;
            }
        }
        public override Job Tick()
        {
            drawingList.Clear();
            if (GameController.Area.CurrentArea.IsHideout || GameController.Area.CurrentArea.IsTown)
            {
                itemNamesCount.Clear();
                return null;
            }

            var worldItems = GameController.IngameState.IngameUi.ItemsOnGroundLabels?.AsParallel().Where(x => x.IsVisible).ToList() ?? [];

            if (worldItems != null)
            {
                var localItemNamesCount = new Dictionary<string, int>();

                foreach (var entity in worldItems)
                {
                    var worldItem = entity.ItemOnGround?.GetComponent<WorldItem>();
                    if (worldItem == null || worldItem.ItemEntity.Type != ExileCore.Shared.Enums.EntityType.Item)
                        continue;

                    var renderItem = worldItem.ItemEntity.GetComponent<RenderItem>();
                    if (renderItem == null || renderItem.ResourcePath == null)
                        continue;


                    var modelPath = renderItem.ResourcePath;

                    if (UniquesHashSet.Contains(modelPath))
                    {
                        var itemName = worldItem.ItemEntity.GetComponent<Base>().Name;
                        if (itemName != null)
                        {
                            if (localItemNamesCount.ContainsKey(itemName))
                            {
                                localItemNamesCount[itemName]++;
                            }
                            else
                            {
                                localItemNamesCount[itemName] = 1;
                            }
                        }
                        drawingList.Add(entity.Label.GetClientRectCache);
                    }
                }

                if (localItemNamesCount.Count != 0)
                {
                    itemNamesCount = localItemNamesCount;
                }
                else
                {
                    itemNamesCount.Clear();
                }
            }
            return null;
        }
    }
}