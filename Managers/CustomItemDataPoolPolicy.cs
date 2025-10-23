using Microsoft.Extensions.ObjectPool;

namespace UniqueLootHelper.Managers
{
    /// <summary>
    /// Policy for creating and resetting CustomItemData objects in the object pool
    /// </summary>
    public class CustomItemDataPoolPolicy : IPooledObjectPolicy<CustomItemData>
    {
        /// <summary>
        /// Creates a new CustomItemData object for the pool
        /// </summary>
        public CustomItemData Create()
        {
            return new CustomItemData();
        }

        /// <summary>
        /// Resets the object to initial state before returning to pool
        /// </summary>
        /// <param name="obj">Object to reset</param>
        /// <returns>True if object can be reused, false otherwise</returns>
        public bool Return(CustomItemData obj)
        {
            if (obj == null)
                return false;

            obj.Reset();
            return true;
        }
    }
}
