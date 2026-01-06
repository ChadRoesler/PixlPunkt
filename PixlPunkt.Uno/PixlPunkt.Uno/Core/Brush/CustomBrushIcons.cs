using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace PixlPunkt.Uno.Core.Brush
{
    /// <summary>
    /// Manages custom brush icons, generating outline icons from 16x16 brush masks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CustomBrushIcons generates 32x32 outline icons from 16x16 brush masks.
    /// These icons follow the naming convention "Brush_Author_BrushName".
    /// </para>
    /// <para><strong>Icon Generation:</strong></para>
    /// <para>
    /// The 16x16 mask is scaled 2x to 32x32, then the outline is traced as white pixels.
    /// </para>
    /// </remarks>
    public sealed class CustomBrushIcons
    {
        private static readonly Lazy<CustomBrushIcons> _instance = new(() => new CustomBrushIcons());

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static CustomBrushIcons Instance => _instance.Value;

        // Keyed by full name (author.brushname)
        private readonly Dictionary<string, byte[]> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        private CustomBrushIcons() { }

        /// <summary>
        /// Gets or generates an outline icon for a brush.
        /// </summary>
        /// <param name="brush">The brush template.</param>
        /// <returns>BGRA pixel data for a 32x32 icon.</returns>
        public byte[] GetIcon(BrushTemplate brush)
        {
            if (brush == null)
                return CreateEmptyIcon();

            var fullName = brush.FullName;

            // Check cache first
            if (_iconCache.TryGetValue(fullName, out var cached))
                return cached;

            // Generate from brush data or stored icon
            byte[] icon;
            if (brush.IconData != null && brush.IconData.Length > 0)
            {
                icon = brush.IconData;
            }
            else
            {
                icon = GenerateOutlineIcon(brush);
            }

            _iconCache[fullName] = icon;
            return icon;
        }

        /// <summary>
        /// Gets an icon by full brush name from cache.
        /// </summary>
        public byte[]? GetIconByFullName(string fullName)
        {
            return _iconCache.TryGetValue(fullName, out var icon) ? icon : null;
        }

        /// <summary>
        /// Registers an icon for a brush.
        /// </summary>
        public void RegisterIcon(string fullName, byte[] iconData)
        {
            _iconCache[fullName] = iconData;
        }

        /// <summary>
        /// Clears the icon cache.
        /// </summary>
        public void ClearCache()
        {
            _iconCache.Clear();
        }

        /// <summary>
        /// Generates a 32x32 outline icon from a brush's 16x16 mask.
        /// </summary>
        /// <param name="brush">The brush template.</param>
        /// <returns>32x32 BGRA outline icon with white outline.</returns>
        public static byte[] GenerateOutlineIcon(BrushTemplate brush)
        {
            const int iconSize = 32;
            var icon = new byte[iconSize * iconSize * 4];

            if (brush?.Mask == null || brush.Mask.Length == 0)
                return icon;

            // Scale 16x16 mask to 32x32 (2x scale)
            var filled = new bool[iconSize, iconSize];
            for (int y = 0; y < iconSize; y++)
            {
                int maskY = y / 2; // 32 -> 16 mapping
                for (int x = 0; x < iconSize; x++)
                {
                    int maskX = x / 2; // 32 -> 16 mapping
                    if (brush.GetPixel(maskX, maskY))
                    {
                        filled[x, y] = true;
                    }
                }
            }

            // Find outline pixels (edges) - USE WHITE (255,255,255)
            for (int y = 0; y < iconSize; y++)
            {
                for (int x = 0; x < iconSize; x++)
                {
                    if (!filled[x, y])
                        continue;

                    // Check if this is an edge pixel (has at least one empty neighbor)
                    bool isEdge = false;
                    for (int dy = -1; dy <= 1 && !isEdge; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !isEdge; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || nx >= iconSize || ny < 0 || ny >= iconSize || !filled[nx, ny])
                                isEdge = true;
                        }
                    }

                    if (isEdge)
                    {
                        int idx = (y * iconSize + x) * 4;
                        icon[idx + 0] = 255; // B - WHITE
                        icon[idx + 1] = 255; // G - WHITE
                        icon[idx + 2] = 255; // R - WHITE
                        icon[idx + 3] = 255; // A
                    }
                }
            }

            return icon;
        }

        /// <summary>
        /// Generates an outline icon with customizable color.
        /// </summary>
        public static byte[] GenerateOutlineIconColored(BrushTemplate brush, byte r, byte g, byte b)
        {
            var icon = GenerateOutlineIcon(brush);

            // Replace white with specified color
            for (int i = 0; i < icon.Length; i += 4)
            {
                if (icon[i + 3] > 0) // Non-transparent
                {
                    icon[i + 0] = b;
                    icon[i + 1] = g;
                    icon[i + 2] = r;
                }
            }

            return icon;
        }

        /// <summary>
        /// Creates an empty 32x32 icon.
        /// </summary>
        private static byte[] CreateEmptyIcon()
        {
            return new byte[32 * 32 * 4];
        }

        /// <summary>
        /// Converts icon BGRA data to an ImageSource for WinUI display.
        /// </summary>
        public static async System.Threading.Tasks.Task<ImageSource?> ToImageSourceAsync(byte[] iconData)
        {
            if (iconData == null || iconData.Length != 32 * 32 * 4)
                return null;

            try
            {
                using var stream = new InMemoryRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                    Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);

                encoder.SetPixelData(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    32, 32, 96, 96, iconData);

                await encoder.FlushAsync();
                stream.Seek(0);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
