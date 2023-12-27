using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ImGuiNET;
using Newtonsoft.Json;
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
        private void ReadUniquesArtworkFile()
        {
            var path = $"{DirectoryFullName}\\{UNIQUESARTWORK_FILE}";
            if (File.Exists(path))
            {
                UniquesHashSet = File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#")).ToList().Select(x => x + ".dds").ToHashSet();
            }
            else
                CreateUniquesArtworkFile();
        }

        private void CreateUniquesArtworkFile()
        {
            var path = $"{DirectoryFullName}\\{UNIQUESARTWORK_FILE}";
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
            var posX = 0;
            var posY = 0;
            var hight = itemNamesCount.Count * 45;
            var rect = new RectangleF(posX, posY, 230, hight);
            Graphics.DrawBox(rect, new Color(0, 0, 0, 200));
            Graphics.DrawFrame(rect, Color.White, 2);

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
            itemNamesCount.Clear();
            if (GameController.Area.CurrentArea.IsHideout ||
                GameController.Area.CurrentArea.IsTown)
            {
                return null;
            }

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount };

            var worldItems = GameController.IngameState.IngameUi.ItemsOnGroundLabels?.AsParallel().Where(x => x.IsVisible).ToList() ?? [];

            if (worldItems != null)
            {
                Parallel.ForEach(worldItems, parallelOptions, entity =>
                {
                    var worldItem = entity.ItemOnGround?.GetComponent<WorldItem>();
                    if (worldItem == null || worldItem.ItemEntity.Type != ExileCore.Shared.Enums.EntityType.Item)
                        return;

                    var renderItem = worldItem.ItemEntity.GetComponent<RenderItem>();
                    if (renderItem == null || renderItem.ResourcePath == null)
                        return;
                    var itemName = worldItem.ItemEntity.GetComponent<Base>().Name;
                    var modelPath = renderItem.ResourcePath;

                    if (UniquesHashSet.Contains(modelPath))
                    {
                        if (itemName != null)
                        {
                            if (itemNamesCount.ContainsKey(itemName))
                            {
                                itemNamesCount[itemName]++;
                            }
                            else
                            {
                                itemNamesCount[itemName] = 1;
                            }
                        }
                        drawingList.Add(entity.Label.GetClientRectCache);
                    }
                });
            }
            return null;
        }
    }
}