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
        /// <summary>
        ///     Default constructor for Object Pool
        /// </summary>
        public CustomItemData() { }

        /// <summary>
        ///     Constructor with parameters for compatibility
        /// </summary>
        public CustomItemData(Entity entity, Element element, Vector2 location)
        {
            Initialize(entity, element, location);
        }

        public uint Id { get; set; }
        public Element Element { get; set; }
        public Entity Entity { get; set; }
        public bool IsCorrupted { get; set; }
        public bool IsIdentified { get; set; }
        public string ResourcePath { get; set; } = string.Empty;
        public RectangleF ClientRect { get; set; }
        public Vector2 Location { get; set; }

        /// <summary>
        ///     Initialize method for object pool - sets all properties from entity data
        /// </summary>
        public void Initialize(Entity entity, Element element, Vector2 location)
        {
            Id = entity.Id;
            Entity = entity;
            Element = element;
            Location = location;

            if (entity.TryGetComponent(out RenderItem renderItem))
            {
                ResourcePath = renderItem.ResourcePath;
            }
            else
            {
                ResourcePath = string.Empty;
            }

            if (entity.TryGetComponent(out Base @base))
            {
                IsCorrupted = @base.isCorrupted;
            }
            else
            {
                IsCorrupted = false;
            }

            if (entity.TryGetComponent(out Mods mods))
            {
                IsIdentified = mods.Identified;
            }
            else
            {
                IsIdentified = false;
            }
        }

        /// <summary>
        ///     Reset method for object pool - clears all properties before returning to pool
        /// </summary>
        public void Reset()
        {
            Entity = null;
            Element = null;
            Id = 0;
            IsCorrupted = false;
            IsIdentified = false;
            ResourcePath = string.Empty;
            ClientRect = RectangleF.Empty;
            Location = Vector2.Zero;
        }
    }
}
