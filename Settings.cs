using System.Security.Cryptography.Xml;
using System.Windows.Forms;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace UniqueLootHelper
{
    public class Settings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(false);
        public HotkeyNode CopyToClipboardHoverItemPath { get; set; } = new HotkeyNode(Keys.F7);
        public ToggleNode IgnoreFullscreenPanels { get; set; } = new(false);
        public ToggleNode IgnoreRightPanels { get; set; } = new(false);
        public ColorNode OutlineLabelColor { get; set; } = new ColorNode(SharpDX.Color.Purple);
        public RangeNode<int> LabelFrameThickness { get; set; } = new RangeNode<int>(2, 1, 10);
        public ToggleNode EnableBoxCountDrawing { get; set; } = new(true);
        public ColorNode BoxBackgroundColor { get; set; } = new ColorNode(new Color(0, 0, 0, 200));
        public ColorNode BoxOutlineColor { get; set; } = new ColorNode(new Color(255, 255, 255, 255));
        public ColorNode BoxTextColor { get; set; } = new ColorNode(new Color(255, 255, 255, 255));
        public ToggleNode BoxOutline { get; set; } = new ToggleNode(false);
        public RangeNode<float> BoxPositionX { get; set; } = new RangeNode<float>(576.0f, 0f, 3000f);
        public RangeNode<float> BoxPositionY { get; set; } = new RangeNode<float>(576.0f, 0f, 3000f);

        public ToggleNode EnableMapDrawing { get; set; } = new(true);
        public ColorNode MapLineColor
        { get; set; } = new(new Color(214, 0, 255, 255));
        public RangeNode<int> MapLineThickness { get; set; } = new(2, 1, 10);
        public ToggleNode WorldMapDrawing { get; set; } = new(true);
        public ColorNode WorldMapLineColor { get; set; } = new(new Color(214, 0, 255, 255));
        public RangeNode<int> WorldMapLineThickness { get; set; } = new(2, 1, 10);
        public ToggleNode UseCorruptedFilter { get; set; } = new(false);

    }
}
