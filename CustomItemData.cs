using System;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using Vector2 = System.Numerics.Vector2;

namespace UniqueLootHelper
{
    /// <summary>
    ///     Represents an item on the ground with its properties and location.
    ///     Designed for use with Object Pool pattern for performance optimization.
    /// </summary>
    public class CustomItemData
    {
        public CustomItemData()
        {
            Entity = new();
            WorldItem = new();
            Element = new();
            Location = new();
            ResourcePath = string.Empty;
            NormalizedResourcePath = string.Empty;
            IsCorrupted = false;
            IsIdentified = false;
            ClientRect = RectangleF.Empty;
        }
        /// <summary>
        ///     Constructor with parameters for compatibility
        /// </summary>
        public CustomItemData(WorldItem worldItem, Element element, Vector2 location, RectangleF clientRect)
        {
            var entity = worldItem.ItemEntity;
            Entity = entity;
            WorldItem = worldItem;
            Element = element;
            Location = location;
            ResourcePath = entity.TryGetComponent(out RenderItem renderItem) ? renderItem.ResourcePath : string.Empty;
            NormalizedResourcePath = NormalizeResourcePath(ResourcePath);
            IsCorrupted = entity.TryGetComponent(out Base @base) && @base.isCorrupted;
            IsIdentified = entity.TryGetComponent(out Mods mods) && mods.Identified;
            ClientRect = clientRect;
        }

        public Element Element { get; set; }
        public WorldItem WorldItem { get; set; }
        public Entity Entity { get; set; }
        public bool IsCorrupted { get; set; }
        public bool IsIdentified { get; set; }
        public string ResourcePath { get; set; } = string.Empty;
        public string NormalizedResourcePath { get; private set; } = string.Empty;
        public RectangleF ClientRect { get; set; }
        public Vector2 Location { get; set; }

        /// <summary>
        ///     Initialize method for object pool - sets all properties from entity data
        /// </summary>
        public void Initialize(WorldItem worldItem, Element element, Vector2 location, RectangleF clientRect)
        {
            var entity = worldItem.ItemEntity;
            Entity = entity;
            WorldItem = worldItem;
            Element = element;
            Location = location;
            ResourcePath = entity.TryGetComponent(out RenderItem renderItem) ? renderItem.ResourcePath : string.Empty;
            NormalizedResourcePath = NormalizeResourcePath(ResourcePath);
            IsCorrupted = entity.TryGetComponent(out Base @base) && @base.isCorrupted;
            IsIdentified = entity.TryGetComponent(out Mods mods) && mods.Identified;
            ClientRect = clientRect;
        }

        private static string NormalizeResourcePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace(".dds", string.Empty, StringComparison.OrdinalIgnoreCase) + ".dds";
        }

        /// <summary>
        ///     Reset method for object pool - clears all properties before returning to pool
        /// </summary>
        public void Reset()
        {
            Entity = new();
            Element = new();
            IsCorrupted = false;
            IsIdentified = false;
            ResourcePath = string.Empty;
            NormalizedResourcePath = string.Empty;
            ClientRect = RectangleF.Empty;
            Location = Vector2.Zero;
        }
    }
}
