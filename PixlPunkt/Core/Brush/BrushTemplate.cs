using PixlPunkt.Constants;

namespace PixlPunkt.Core.Brush
{
    /// <summary>
    /// Defines a custom brush with a single 16x16 mask for pixel-perfect painting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="BrushTemplate"/> contains a single 16x16 brush mask that is scaled
    /// at runtime to match the requested brush size. This provides a simple, consistent
    /// brush system where the 16x16 mask serves as the "master" definition.
    /// </para>
    /// <para><strong>Brush Identification:</strong></para>
    /// <para>
    /// Brushes are identified by their full name in the format "author.brushname" to avoid
    /// conflicts between different creators. For example: "PixlPunkt.Star", "JohnDoe.Splatter".
    /// </para>
    /// <para><strong>Scaling:</strong></para>
    /// <para>
    /// The 16x16 mask is scaled using nearest-neighbor sampling:
    /// <br/>• Size 1-16: Scale down from 16x16
    /// <br/>• Size 17-128: Scale up from 16x16
    /// </para>
    /// <para><strong>Pivot Point:</strong></para>
    /// <para>
    /// The pivot (PivotX, PivotY) defines the brush's anchor point as normalized coordinates (0.0-1.0):
    /// <br/>• (0.0, 0.0) = Top-Left
    /// <br/>• (0.5, 0.5) = Center (default)
    /// <br/>• (1.0, 1.0) = Bottom-Right
    /// </para>
    /// </remarks>
    public sealed class BrushTemplate
    {
        /// <summary>
        /// Gets or sets the brush author/creator name.
        /// </summary>
        public string Author { get; set; } = "Custom";

        /// <summary>
        /// Gets or sets the user-visible brush name (without author prefix).
        /// </summary>
        public string Name { get; set; } = "Brush";

        /// <summary>
        /// Gets the full brush identifier in "author.brushname" format.
        /// </summary>
        public string FullName => $"{Author}.{Name}";

        /// <summary>
        /// Gets or sets the horizontal pivot position (0.0 = left, 0.5 = center, 1.0 = right).
        /// </summary>
        public float PivotX { get; set; } = 0.5f;

        /// <summary>
        /// Gets or sets the vertical pivot position (0.0 = top, 0.5 = center, 1.0 = bottom).
        /// </summary>
        public float PivotY { get; set; } = 0.5f;

        /// <summary>
        /// Gets or sets the 16x16 brush mask (1-bit packed, 32 bytes).
        /// </summary>
        /// <remarks>
        /// The mask is stored as 1-bit per pixel, packed into bytes (8 pixels per byte).
        /// Total size: 16 * 16 / 8 = 32 bytes.
        /// A set bit (1) indicates a filled pixel, unset (0) indicates empty.
        /// </remarks>
        public byte[] Mask { get; set; } = new byte[32];

        /// <summary>
        /// Gets or sets the icon image data (32x32 BGRA outline) for UI display.
        /// </summary>
        public byte[] IconData { get; set; } = [];

        /// <summary>
        /// Gets or sets the icon width (default 32).
        /// </summary>
        public int IconWidth { get; set; } = 32;

        /// <summary>
        /// Gets or sets the icon height (default 32).
        /// </summary>
        public int IconHeight { get; set; } = 32;

        /// <summary>
        /// Creates an empty brush template.
        /// </summary>
        public BrushTemplate() { }

        /// <summary>
        /// Creates a brush template with the specified author and name.
        /// </summary>
        public BrushTemplate(string author, string name)
        {
            Author = author;
            Name = name;
        }

        /// <summary>
        /// Creates a brush template with the specified name (uses "Custom" as author).
        /// </summary>
        public BrushTemplate(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Parses a full name ("author.brushname") into author and name components.
        /// </summary>
        public static (string author, string name) ParseFullName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return ("Custom", "Brush");

            int dotIndex = fullName.IndexOf('.');
            if (dotIndex <= 0 || dotIndex >= fullName.Length - 1)
                return ("Custom", fullName);

            return (fullName[..dotIndex], fullName[(dotIndex + 1)..]);
        }

        /// <summary>
        /// Gets whether a specific pixel in the mask is set (filled).
        /// </summary>
        /// <param name="x">X coordinate (0 to 15).</param>
        /// <param name="y">Y coordinate (0 to 15).</param>
        /// <returns>True if the pixel is filled, false otherwise.</returns>
        public bool GetPixel(int x, int y)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16)
                return false;

            int bitIndex = y * 16 + x;
            int byteIndex = bitIndex / 8;
            int bitOffset = 7 - (bitIndex % 8); // MSB first

            if (byteIndex >= Mask.Length)
                return false;

            return (Mask[byteIndex] & (1 << bitOffset)) != 0;
        }

        /// <summary>
        /// Sets a specific pixel in the mask.
        /// </summary>
        /// <param name="x">X coordinate (0 to 15).</param>
        /// <param name="y">Y coordinate (0 to 15).</param>
        /// <param name="value">True to fill, false to clear.</param>
        public void SetPixel(int x, int y, bool value)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16)
                return;

            int bitIndex = y * 16 + x;
            int byteIndex = bitIndex / 8;
            int bitOffset = 7 - (bitIndex % 8); // MSB first

            if (byteIndex >= Mask.Length)
                return;

            if (value)
                Mask[byteIndex] |= (byte)(1 << bitOffset);
            else
                Mask[byteIndex] &= (byte)~(1 << bitOffset);
        }

        /// <summary>
        /// Creates a mask from BGRA pixel data, treating any non-transparent pixel as filled.
        /// </summary>
        /// <param name="bgra">Source BGRA pixels (16 * 16 * 4 = 1024 bytes).</param>
        /// <returns>Packed 1-bit mask (32 bytes).</returns>
        public static byte[] CreateMaskFromBgra(byte[] bgra)
        {
            const int size = 16;
            var mask = new byte[32]; // 16 * 16 / 8 = 32 bytes

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int pixelIndex = (y * size + x) * 4;
                    byte alpha = bgra[pixelIndex + 3];

                    if (alpha > 0) // Any non-transparent pixel is "filled"
                    {
                        int bitIndex = y * size + x;
                        int byteIndex = bitIndex / 8;
                        int bitOffset = 7 - (bitIndex % 8);
                        mask[byteIndex] |= (byte)(1 << bitOffset);
                    }
                }
            }

            return mask;
        }

        /// <summary>
        /// Gets whether the brush has any filled pixels.
        /// </summary>
        public bool HasContent
        {
            get
            {
                foreach (var b in Mask)
                    if (b != 0) return true;
                return false;
            }
        }

        /// <summary>
        /// Gets the icon name for this brush in "Brush_Author_BrushName" format.
        /// </summary>
        public string IconName => BrushExportConstants.GetIconName(Author, Name);

        /// <summary>
        /// Gets the display name for UI (shows "Name" with author in tooltip).
        /// </summary>
        public string DisplayName => Name;
    }
}
