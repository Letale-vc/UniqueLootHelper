using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace UniqueLootHelper
{
    public class Settings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(false);
        [Menu("Color outline")]
        public ColorNode Color { get; set; } = new ColorNode(SharpDX.Color.Purple);
        public RangeNode<int> FrameThickness { get; set; } = new RangeNode<int>(2, 1, 5);
        public RangeNode<uint> CacheIntervall { get; set; } = new RangeNode<uint>(2, 1, 5);

        [Menu("Position box X")]
        public RangeNode<float> PositionX { get; set; } = new RangeNode<float>(576.0f, 0.0f, 3000.0f);

        [Menu("Position box Y")]
        public RangeNode<float> PositionY { get; set; } = new RangeNode<float>(576.0f, 0.0f, 3000.0f);

        [Menu("Show Outline Box")]
        public ToggleNode BoxOutline { get; set; } = new ToggleNode(false);
        public ButtonNode RefreshUniquesFile { get; set; } = new ButtonNode();
    }
}
