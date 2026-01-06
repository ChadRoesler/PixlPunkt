using Windows.Graphics;

namespace PixlPunkt.Uno.Core.Helpers
{
    /// <summary>
    /// Helper methods for creating Windows.Graphics struct types.
    /// Uno Platform uses object initializer syntax instead of constructors for these structs.
    /// </summary>
    public static class GraphicsStructHelper
    {
        /// <summary>
        /// Creates a new SizeInt32 with the specified dimensions.
        /// </summary>
        public static SizeInt32 CreateSize(int width, int height) => new SizeInt32 { Width = width, Height = height };

        /// <summary>
        /// Creates a new PointInt32 with the specified coordinates.
        /// </summary>
        public static PointInt32 CreatePoint(int x, int y) => new PointInt32 { X = x, Y = y };

        /// <summary>
        /// Creates a new RectInt32 with the specified bounds.
        /// </summary>
        public static RectInt32 CreateRect(int x, int y, int width, int height) => new RectInt32 { X = x, Y = y, Width = width, Height = height };
    }
}
