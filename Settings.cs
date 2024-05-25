using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace UniqueLootHelper
{
    public class Settings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(false);
        [Menu("Color label outline")]
        public ColorNode OutlineLabelColor { get; set; } = new ColorNode(SharpDX.Color.Purple);
        public RangeNode<int> FrameThickness { get; set; } = new RangeNode<int>(2, 1, 5);
        public RangeNode<uint> CacheIntervall { get; set; } = new RangeNode<uint>(2, 1, 5);

        [Menu("Position box X")]
        public RangeNode<float> PositionX { get; set; } = new RangeNode<float>(576.0f, 0f, 3000f);

        [Menu("Position box Y")]
        public RangeNode<float> PositionY { get; set; } = new RangeNode<float>(576.0f, 0f, 3000f);

        [Menu("Show Outline Box")]
        public ToggleNode BoxOutline { get; set; } = new ToggleNode(false);


        public ToggleNode EnableMapDrawing { get; set; } = new(true);
        public ColorNode MapLineColor { get; set; } = new(new Color(214, 0, 255, 255));
        public RangeNode<float> MapLineThickness { get; set; } = new(2.317f, 1f, 10f);

    }
}
