using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;
using SharpDX;
using System.Windows.Forms;
using UniqueLootHelper.Managers;
using Vector4 = System.Numerics.Vector4;

namespace UniqueLootHelper
{
    public class Settings : ISettings
    {
        public HotkeyNodeV2 CopyToClipboardHoverItemPath { get; set; } = new(Keys.F7);
        public ToggleNode IgnoreFullscreenPanels { get; set; } = new(false);
        public ToggleNode IgnoreRightPanels { get; set; } = new(false);
        public LabelDrawingSettings LabelDrawingSettings { get; set; } = new();
        public BoxSettings BoxSettings { get; set; } = new();
        public MapDrawingSettings MapDrawingSettings { get; set; } = new();
        public ToggleNode UseCorruptedFilter { get; set; } = new(false);
        public SoundNotificationSettings SoundNotificationSettings { get; set; } = new();
        public ProfilerSettings ProfilerSettings { get; set; } = new();
        public ToggleNode Enable { get; set; } = new(false);
    }

    [Submenu(CollapsedByDefault = true)]
    public class LabelDrawingSettings
    {
        public ToggleNode Enabled { get; set; } = new(true);

        public ColorNode OutlineLabelColor { get; set; } = new(Color.Purple);
        public ToggleNode EnableOutlineLebel { get; set; } = new(true);
        public RangeNode<int> LabelFrameThickness { get; set; } = new(2, 1, 10);
        public ToggleNode EnableLabelName { get; set; } = new(true);
        public ColorNode BackgroundLabel { get; set; } = new(Color.White);
        public ColorNode LabelTextColor { get; set; } = new(Color.Red);

        [Menu("Text Outline Enabled", "Draw black outline around text (Diablo/PoE style)")]
        public ToggleNode TextOutlineEnabled { get; set; } = new(true);

        [Menu("Text Outline Thickness", "Thickness of the text outline")]
        public RangeNode<float> TextOutlineThickness { get; set; } = new(1.5f, 0.5f, 3.0f);

        [Menu("Text Outline Color", "Color of the text outline (usually black)")]
        public ColorNode TextOutlineColor { get; set; } = new(new Color(0, 0, 0, 255));
    }

    [Submenu(CollapsedByDefault = true)]
    public class MapDrawingSettings
    {
        public ToggleNode Enabled { get; set; } = new(true);
        public ToggleNode EnableMapDrawing { get; set; } = new(true);
        public ColorNode MapLineColor { get; set; } = new(new Color(214, 0, 255, 255));
        public ToggleNode WorldMapDrawing { get; set; } = new(true);
        public RangeNode<int> WorldMapLineThickness { get; set; } = new(2, 1, 10);
        public ColorNode WorldMapLineColor { get; set; } = new(new Color(214, 0, 255, 255));
        public RangeNode<int> MapLineThickness { get; set; } = new(2, 1, 10);
    }

    [Submenu(CollapsedByDefault = true)]
    public class BoxSettings
    {
        public ToggleNode Enabled { get; set; } = new(true);
        public ToggleNode EnableBoxCountDrawing { get; set; } = new(true);
        public ColorNode BoxBackgroundColor { get; set; } = new(new Color(0, 0, 0, 200));
        public ColorNode BoxOutlineColor { get; set; } = new(new Color(255, 255, 255, 255));
        public ColorNode BoxTextColor { get; set; } = new(new Color(255, 255, 255, 255));
        public ToggleNode BoxOutline { get; set; } = new(false);
        public RangeNode<float> BoxPositionX { get; set; } = new(576.0f, 0f, 3000f);
        public RangeNode<float> BoxPositionY { get; set; } = new(576.0f, 0f, 3000f);
    }

    [Submenu(CollapsedByDefault = true)]
    public class SoundNotificationSettings
    {
        public ToggleNode Enabled { get; set; } = new(true);

        [JsonIgnore]
        public CustomNode Info { get; set; } =
            new(() =>
            {
                ImGui.Text(
                    $"By default, plays {SoundManager.DefaultWavFile} in the plugin's config directory.\nTo customize sounds per unique, create UniqueName.wav in the same directory"
                );
            });

        [JsonIgnore]
        public ButtonNode OpenConfigDirectory { get; set; } = new();

        [JsonIgnore]
        public ButtonNode ReloadSoundList { get; set; } = new();

        [JsonIgnore]
        [Menu(null, "For debugging your alerts")]
        public ButtonNode ResetEntityNotificationFlags { get; set; } = new();

        public RangeNode<float> Volume { get; set; } = new(1, 0, 2);
    }

    [Submenu(CollapsedByDefault = true)]
    public class ProfilerSettings
    {
        [Menu("Enable Profiler", "Enable performance profiling")]
        public ToggleNode Enabled { get; set; } = new(false);

        [JsonIgnore]
        [Menu("Show Profiler Window", "Open profiler window to view performance metrics")]
        public ButtonNode ShowProfilerWindow { get; set; } = new();

        [JsonIgnore]
        [Menu(null, "Profiler measures: Ground Items Processing, Filtering, Drawing, Statistics")]
        public CustomNode Info { get; set; } =
            new(() =>
            {
                ImGui.Text("Performance metrics are displayed in a separate window.");
                ImGui.Text("Lower values (ms) mean better performance.");
                ImGui.Text("ExileAPI renders at 60 FPS (16.67ms per frame)");
                ImGui.TextColored(new Vector4(0, 1, 0, 1), "Green: < 10ms (good)");
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "Yellow: 10-16ms (acceptable)");
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Red: > 16ms (bad, drops FPS)");
            });
    }
}
