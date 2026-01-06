using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Uno.Core.Imaging;

namespace PixlPunkt.Uno.Core.Document.Layer
{
    /// <summary>
    /// Represents a layer mask for non-destructive editing.
    /// The mask is a grayscale image where:
    /// - White (255) = fully visible (layer content shows through)
    /// - Black (0) = fully hidden (layer content is masked out)
    /// - Gray values = partial transparency
    /// </summary>
    /// <remarks>
    /// <para>
    /// Layer masks allow non-destructive hiding of portions of a layer.
    /// Instead of erasing pixels, you can paint on the mask to show/hide areas.
    /// This is reversible - painting white on the mask reveals the original content.
    /// </para>
    /// <para>
    /// The mask is stored as a single-channel (grayscale) image but internally uses
    /// a PixelSurface (BGRA) for consistency with the rendering pipeline. Only the
    /// alpha channel is used; RGB channels are set to the same value for preview display.
    /// </para>
    /// <para>
    /// <strong>Mask Editing:</strong>
    /// - Painting white reveals the layer
    /// - Painting black hides the layer
    /// - Soft brushes create smooth transitions
    /// - The mask is editable just like the layer content
    /// </para>
    /// </remarks>
    public sealed class LayerMask : INotifyPropertyChanged
    {
        // ====================================================================
        // FIELDS
        // ====================================================================

        private WriteableBitmap _previewBitmap;
        private bool _updatingPreview;
        private bool _isEnabled = true;
        private bool _isInverted;
        private bool _isLinked = true;
        private byte _density = 255;
        private int _featherRadius;

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets the mask surface containing the mask data.
        /// Uses BGRA format but only the grayscale value is meaningful.
        /// RGB = grayscale value, A = 255 (fully opaque for preview).
        /// </summary>
        public PixelSurface Surface { get; }

        /// <summary>
        /// Gets the width of the mask in pixels.
        /// </summary>
        public int Width => Surface.Width;

        /// <summary>
        /// Gets the height of the mask in pixels.
        /// </summary>
        public int Height => Surface.Height;

        /// <summary>
        /// Gets or sets whether the mask is currently enabled (applied during compositing).
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    MaskChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to invert the mask during application.
        /// When true, white areas hide and black areas reveal.
        /// </summary>
        public bool IsInverted
        {
            get => _isInverted;
            set
            {
                if (_isInverted != value)
                {
                    _isInverted = value;
                    OnPropertyChanged();
                    MaskChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the mask is linked to the layer.
        /// When linked, moving the layer also moves the mask.
        /// </summary>
        public bool IsLinked
        {
            get => _isLinked;
            set
            {
                if (_isLinked != value)
                {
                    _isLinked = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the mask density (opacity multiplier, 0-255).
        /// Reduces the overall effect of the mask.
        /// </summary>
        public byte Density
        {
            get => _density;
            set
            {
                if (_density != value)
                {
                    _density = value;
                    OnPropertyChanged();
                    MaskChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Gets or sets the feather radius for mask edge softening.
        /// 0 = sharp edges, higher values = softer transitions.
        /// </summary>
        public int FeatherRadius
        {
            get => _featherRadius;
            set
            {
                if (_featherRadius != value)
                {
                    _featherRadius = value;
                    OnPropertyChanged();
                    MaskChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Gets the preview image for the mask (grayscale representation).
        /// </summary>
        public ImageSource Preview => _previewBitmap;

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raised when mask pixel data or settings change.
        /// </summary>
        public event Action? MaskChanged;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        /// <summary>
        /// Creates a new layer mask with the specified dimensions.
        /// The mask is initialized to white (fully visible).
        /// </summary>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        public LayerMask(int width, int height)
        {
            Surface = new PixelSurface(width, height);
            _previewBitmap = new WriteableBitmap(width, height);

            // Initialize to white (fully visible)
            Fill(255);

            Surface.PixelsChanged += OnSurfacePixelsChanged;
            UpdatePreview();
        }

        /// <summary>
        /// Creates a new layer mask from existing pixel data.
        /// </summary>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="maskData">Grayscale mask data (one byte per pixel).</param>
        public LayerMask(int width, int height, byte[] maskData)
        {
            if (maskData.Length != width * height)
                throw new ArgumentException("Mask data length doesn't match dimensions", nameof(maskData));

            Surface = new PixelSurface(width, height);
            _previewBitmap = new WriteableBitmap(width, height);

            // Convert grayscale to BGRA
            var pixels = Surface.Pixels;
            for (int i = 0; i < maskData.Length; i++)
            {
                byte v = maskData[i];
                int idx = i * 4;
                pixels[idx + 0] = v; // B
                pixels[idx + 1] = v; // G
                pixels[idx + 2] = v; // R
                pixels[idx + 3] = 255; // A (always opaque for preview)
            }

            Surface.PixelsChanged += OnSurfacePixelsChanged;
            UpdatePreview();
        }

        // ====================================================================
        // PIXEL OPERATIONS
        // ====================================================================

        /// <summary>
        /// Gets the mask value at a specific pixel location.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>Mask value (0-255), or 255 if out of bounds.</returns>
        public byte GetMaskValue(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return 255; // Outside mask = fully visible

            int idx = (y * Width + x) * 4;
            return Surface.Pixels[idx]; // Return blue channel (grayscale)
        }

        /// <summary>
        /// Sets the mask value at a specific pixel location.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="value">Mask value (0-255).</param>
        public void SetMaskValue(int x, int y, byte value)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return;

            int idx = (y * Width + x) * 4;
            var pixels = Surface.Pixels;
            pixels[idx + 0] = value; // B
            pixels[idx + 1] = value; // G
            pixels[idx + 2] = value; // R
            // A stays 255
        }

        /// <summary>
        /// Gets the effective mask value after applying inversion and density.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>Effective mask value (0-255).</returns>
        public byte GetEffectiveMaskValue(int x, int y)
        {
            if (!IsEnabled)
                return 255; // Disabled = fully visible

            byte raw = GetMaskValue(x, y);

            // Apply inversion
            if (IsInverted)
                raw = (byte)(255 - raw);

            // Apply density
            if (Density < 255)
            {
                // Lerp from 255 (no mask effect) towards raw based on density
                float t = Density / 255f;
                raw = (byte)(255 + (raw - 255) * t);
            }

            return raw;
        }

        /// <summary>
        /// Fills the entire mask with a single value.
        /// </summary>
        /// <param name="value">Value to fill (0 = fully hidden, 255 = fully visible).</param>
        public void Fill(byte value)
        {
            var pixels = Surface.Pixels;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 0] = value; // B
                pixels[i + 1] = value; // G
                pixels[i + 2] = value; // R
                pixels[i + 3] = 255;   // A
            }
            Surface.NotifyChanged();
        }

        /// <summary>
        /// Inverts all mask values (pixel data inversion, not IsInverted toggle).
        /// </summary>
        public void InvertPixels()
        {
            var pixels = Surface.Pixels;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte v = (byte)(255 - pixels[i]);
                pixels[i + 0] = v; // B
                pixels[i + 1] = v; // G
                pixels[i + 2] = v; // R
            }
            Surface.NotifyChanged();
        }

        /// <summary>
        /// Inverts all mask values.
        /// </summary>
        [Obsolete("Use InvertPixels() for pixel inversion or toggle IsInverted property for display inversion")]
        public void Invert() => InvertPixels();

        /// <summary>
        /// Exports the mask as a grayscale byte array (one byte per pixel).
        /// </summary>
        /// <returns>Grayscale mask data.</returns>
        public byte[] ExportGrayscale()
        {
            var result = new byte[Width * Height];
            var pixels = Surface.Pixels;
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = pixels[i * 4]; // Blue channel = grayscale
            }
            return result;
        }

        // ====================================================================
        // PREVIEW
        // ====================================================================

        private void OnSurfacePixelsChanged()
        {
            UpdatePreview();
            MaskChanged?.Invoke();
        }

        /// <summary>
        /// Updates the preview bitmap from the current mask data.
        /// </summary>
        public void UpdatePreview()
        {
            if (_updatingPreview) return;
            _updatingPreview = true;
            try
            {
                int w = Width;
                int h = Height;
                var src = Surface.Pixels;

                // Recreate preview bitmap if dimensions changed
                if (_previewBitmap.PixelWidth != w || _previewBitmap.PixelHeight != h)
                {
                    _previewBitmap = new WriteableBitmap(w, h);
                }

                using var stream = _previewBitmap.PixelBuffer.AsStream();
                stream.Seek(0, SeekOrigin.Begin);

                // Copy surface pixels directly (already in correct format)
                stream.Write(src, 0, src.Length);
            }
            finally
            {
                _updatingPreview = false;
            }

            OnPropertyChanged(nameof(Preview));
        }

        /// <summary>
        /// Resizes the mask to new dimensions.
        /// </summary>
        /// <param name="newWidth">New width.</param>
        /// <param name="newHeight">New height.</param>
        /// <param name="offsetX">X offset for content placement.</param>
        /// <param name="offsetY">Y offset for content placement.</param>
        public void Resize(int newWidth, int newHeight, int offsetX = 0, int offsetY = 0)
        {
            var oldPixels = (byte[])Surface.Pixels.Clone();
            int oldW = Width;
            int oldH = Height;

            // Create new pixel buffer
            var newPixels = new byte[newWidth * newHeight * 4];
            Surface.Resize(newWidth, newHeight, newPixels);
            _previewBitmap = new WriteableBitmap(newWidth, newHeight);

            // Fill with white (visible) first
            Fill(255);

            // Copy old content with offset
            for (int y = 0; y < oldH; y++)
            {
                int dstY = y + offsetY;
                if (dstY < 0 || dstY >= newHeight) continue;

                for (int x = 0; x < oldW; x++)
                {
                    int dstX = x + offsetX;
                    if (dstX < 0 || dstX >= newWidth) continue;

                    int srcIdx = (y * oldW + x) * 4;
                    int dstIdx = (dstY * newWidth + dstX) * 4;

                    Surface.Pixels[dstIdx + 0] = oldPixels[srcIdx + 0];
                    Surface.Pixels[dstIdx + 1] = oldPixels[srcIdx + 1];
                    Surface.Pixels[dstIdx + 2] = oldPixels[srcIdx + 2];
                    Surface.Pixels[dstIdx + 3] = oldPixels[srcIdx + 3];
                }
            }

            Surface.NotifyChanged();
        }

        // ====================================================================
        // INOTIFYPROPERTYCHANGED
        // ====================================================================

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
