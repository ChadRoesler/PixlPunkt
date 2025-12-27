using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PixlPunkt.Core.Reference
{
    /// <summary>
    /// Represents a reference image overlay that can be displayed on the canvas
    /// for artistic reference while drawing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference images are non-destructive overlays that help artists trace, match colors,
    /// or maintain proportions while creating pixel art. They are not part of the document's
    /// layer stack and are not exported with the final image.
    /// </para>
    /// <para>
    /// Each reference image has its own transform properties (position, scale, rotation)
    /// and display settings (opacity, locked state) that can be adjusted independently.
    /// </para>
    /// <para>
    /// Unlike layers, reference images are rendered using smooth interpolation (not pixel-snapped)
    /// and can extend beyond the canvas boundaries without being clipped.
    /// </para>
    /// </remarks>
    public sealed class ReferenceImage : INotifyPropertyChanged, IDisposable
    {
        private string _name = "Reference";
        private string? _filePath;
        private byte[]? _pixels;
        private int _width;
        private int _height;
        private float _positionX;
        private float _positionY;
        private float _scale = 1.0f;
        private float _rotation;
        private float _opacity = 0.5f;
        private bool _isLocked;
        private bool _isVisible = true;
        private bool _renderBehind = true;
        private WriteableBitmap? _previewBitmap;

        /// <summary>
        /// Gets the unique identifier for this reference image.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the display name of this reference image.
        /// </summary>
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets the original file path of the image (for reloading).
        /// </summary>
        public string? FilePath
        {
            get => _filePath;
            set { if (_filePath != value) { _filePath = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets the pixel data (BGRA format).
        /// </summary>
        public byte[]? Pixels => _pixels;

        /// <summary>
        /// Gets the width of the reference image in pixels.
        /// </summary>
        public int Width => _width;

        /// <summary>
        /// Gets the height of the reference image in pixels.
        /// </summary>
        public int Height => _height;

        /// <summary>
        /// Gets or sets the X position of the reference image on the canvas (in document coordinates).
        /// </summary>
        public float PositionX
        {
            get => _positionX;
            set { if (_positionX != value) { _positionX = value; OnPropertyChanged(); TransformChanged?.Invoke(); } }
        }

        /// <summary>
        /// Gets or sets the Y position of the reference image on the canvas (in document coordinates).
        /// </summary>
        public float PositionY
        {
            get => _positionY;
            set { if (_positionY != value) { _positionY = value; OnPropertyChanged(); TransformChanged?.Invoke(); } }
        }

        /// <summary>
        /// Gets or sets the scale factor of the reference image (1.0 = 100%).
        /// </summary>
        public float Scale
        {
            get => _scale;
            set
            {
                float clamped = Math.Clamp(value, 0.01f, 100.0f);
                if (_scale != clamped) { _scale = clamped; OnPropertyChanged(); OnPropertyChanged(nameof(ScalePercent)); TransformChanged?.Invoke(); }
            }
        }

        /// <summary>
        /// Gets or sets the scale as a percentage (100 = 100%).
        /// </summary>
        public float ScalePercent
        {
            get => _scale * 100f;
            set => Scale = value / 100f;
        }

        /// <summary>
        /// Gets or sets the rotation of the reference image in degrees.
        /// </summary>
        public float Rotation
        {
            get => _rotation;
            set
            {
                // Normalize to -180 to 180 range
                float normalized = value;
                while (normalized > 180f) normalized -= 360f;
                while (normalized < -180f) normalized += 360f;
                if (_rotation != normalized) { _rotation = normalized; OnPropertyChanged(); TransformChanged?.Invoke(); }
            }
        }

        /// <summary>
        /// Gets or sets the opacity of the reference image (0.0 = invisible, 1.0 = fully visible).
        /// </summary>
        public float Opacity
        {
            get => _opacity;
            set
            {
                float clamped = Math.Clamp(value, 0.0f, 1.0f);
                if (_opacity != clamped) { _opacity = clamped; OnPropertyChanged(); OnPropertyChanged(nameof(OpacityPercent)); }
            }
        }

        /// <summary>
        /// Gets or sets the opacity as a percentage (0-100).
        /// </summary>
        public int OpacityPercent
        {
            get => (int)(_opacity * 100f);
            set => Opacity = value / 100f;
        }

        /// <summary>
        /// Gets or sets whether the reference image is locked (cannot be moved/transformed).
        /// </summary>
        public bool IsLocked
        {
            get => _isLocked;
            set { if (_isLocked != value) { _isLocked = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether the reference image is visible.
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether the reference image renders behind the document (true)
        /// or in front of it (false).
        /// </summary>
        public bool RenderBehind
        {
            get => _renderBehind;
            set { if (_renderBehind != value) { _renderBehind = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets the preview bitmap for UI display.
        /// </summary>
        public WriteableBitmap? PreviewBitmap
        {
            get => _previewBitmap;
            private set { if (_previewBitmap != value) { _previewBitmap = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets whether this reference image has loaded pixel data.
        /// </summary>
        public bool HasPixels => _pixels != null && _pixels.Length > 0;

        /// <summary>
        /// Raised when the transform (position, scale, rotation) changes.
        /// </summary>
        public event Action? TransformChanged;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates a new empty reference image.
        /// </summary>
        public ReferenceImage()
        {
        }

        /// <summary>
        /// Creates a reference image from pixel data.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="pixels">Pixel data in BGRA format.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <param name="filePath">Optional source file path.</param>
        public ReferenceImage(string name, byte[] pixels, int width, int height, string? filePath = null)
        {
            _name = name;
            _pixels = pixels;
            _width = width;
            _height = height;
            _filePath = filePath;
            UpdatePreview();
        }

        /// <summary>
        /// Sets the pixel data for this reference image.
        /// </summary>
        /// <param name="pixels">Pixel data in BGRA format.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        public void SetPixels(byte[] pixels, int width, int height)
        {
            _pixels = pixels;
            _width = width;
            _height = height;
            UpdatePreview();
            OnPropertyChanged(nameof(Pixels));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(HasPixels));
        }

        /// <summary>
        /// Updates the preview bitmap from the current pixel data.
        /// </summary>
        public void UpdatePreview()
        {
            if (_pixels == null || _width <= 0 || _height <= 0)
            {
                PreviewBitmap = null;
                return;
            }

            try
            {
                var bitmap = new WriteableBitmap(_width, _height);
                using var stream = bitmap.PixelBuffer.AsStream();
                stream.Write(_pixels, 0, _pixels.Length);
                PreviewBitmap = bitmap;
            }
            catch
            {
                PreviewBitmap = null;
            }
        }

        /// <summary>
        /// Gets the scaled width of the reference image.
        /// </summary>
        public float ScaledWidth => _width * _scale;

        /// <summary>
        /// Gets the scaled height of the reference image.
        /// </summary>
        public float ScaledHeight => _height * _scale;

        /// <summary>
        /// Gets the center X position of the reference image.
        /// </summary>
        public float CenterX => _positionX + ScaledWidth / 2f;

        /// <summary>
        /// Gets the center Y position of the reference image.
        /// </summary>
        public float CenterY => _positionY + ScaledHeight / 2f;

        /// <summary>
        /// Gets the bounding rectangle of the reference image in document coordinates.
        /// </summary>
        /// <returns>Tuple of (x, y, width, height).</returns>
        public (float x, float y, float width, float height) GetBounds()
        {
            return (_positionX, _positionY, ScaledWidth, ScaledHeight);
        }

        /// <summary>
        /// Tests if a point (in document coordinates) is within the reference image bounds.
        /// Accounts for rotation.
        /// </summary>
        /// <param name="docX">X coordinate in document space.</param>
        /// <param name="docY">Y coordinate in document space.</param>
        /// <returns>True if the point is within the image bounds.</returns>
        public bool HitTest(float docX, float docY)
        {
            if (!HasPixels) return false;

            // Transform point to local coordinates (unrotated)
            float centerX = CenterX;
            float centerY = CenterY;

            // Translate to center
            float localX = docX - centerX;
            float localY = docY - centerY;

            // Unrotate if needed
            if (Math.Abs(_rotation) > 0.01f)
            {
                float radians = -_rotation * MathF.PI / 180f;
                float cos = MathF.Cos(radians);
                float sin = MathF.Sin(radians);
                float rotatedX = localX * cos - localY * sin;
                float rotatedY = localX * sin + localY * cos;
                localX = rotatedX;
                localY = rotatedY;
            }

            // Translate back to corner-relative
            localX += ScaledWidth / 2f;
            localY += ScaledHeight / 2f;

            // Check if within bounds
            return localX >= 0 && localX < ScaledWidth && localY >= 0 && localY < ScaledHeight;
        }

        /// <summary>
        /// Gets the corner positions of the reference image in document coordinates.
        /// Accounts for rotation.
        /// </summary>
        /// <returns>Array of 4 corner positions (TL, TR, BR, BL).</returns>
        public (float x, float y)[] GetCorners()
        {
            float w = ScaledWidth;
            float h = ScaledHeight;
            float cx = CenterX;
            float cy = CenterY;

            var localCorners = new (float x, float y)[]
            {
                (-w / 2f, -h / 2f), // TL
                (w / 2f, -h / 2f),  // TR
                (w / 2f, h / 2f),   // BR
                (-w / 2f, h / 2f)   // BL
            };

            if (Math.Abs(_rotation) > 0.01f)
            {
                float radians = _rotation * MathF.PI / 180f;
                float cos = MathF.Cos(radians);
                float sin = MathF.Sin(radians);

                for (int i = 0; i < 4; i++)
                {
                    float rx = localCorners[i].x * cos - localCorners[i].y * sin;
                    float ry = localCorners[i].x * sin + localCorners[i].y * cos;
                    localCorners[i] = (cx + rx, cy + ry);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    localCorners[i] = (cx + localCorners[i].x, cy + localCorners[i].y);
                }
            }

            return localCorners;
        }

        /// <summary>
        /// Tests if a point is near a corner handle. Returns the corner index (0-3) or -1.
        /// </summary>
        /// <param name="docX">X coordinate in document space.</param>
        /// <param name="docY">Y coordinate in document space.</param>
        /// <param name="handleRadius">Radius around corners for hit detection.</param>
        /// <returns>Corner index (0=TL, 1=TR, 2=BR, 3=BL) or -1.</returns>
        public int HitTestCorner(float docX, float docY, float handleRadius)
        {
            var corners = GetCorners();
            float radiusSq = handleRadius * handleRadius;

            for (int i = 0; i < 4; i++)
            {
                float dx = docX - corners[i].x;
                float dy = docY - corners[i].y;
                if (dx * dx + dy * dy <= radiusSq)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Resets the transform to default values.
        /// </summary>
        public void ResetTransform()
        {
            _positionX = 0;
            _positionY = 0;
            _scale = 1.0f;
            _rotation = 0;

            OnPropertyChanged(nameof(PositionX));
            OnPropertyChanged(nameof(PositionY));
            OnPropertyChanged(nameof(Scale));
            OnPropertyChanged(nameof(ScalePercent));
            OnPropertyChanged(nameof(Rotation));
            TransformChanged?.Invoke();
        }

        /// <summary>
        /// Fits the reference image to the specified canvas dimensions.
        /// </summary>
        /// <param name="canvasWidth">Canvas width.</param>
        /// <param name="canvasHeight">Canvas height.</param>
        /// <param name="padding">Optional padding percentage (0-0.5).</param>
        public void FitToCanvas(int canvasWidth, int canvasHeight, float padding = 0.1f)
        {
            if (!HasPixels || canvasWidth <= 0 || canvasHeight <= 0) return;

            float availableW = canvasWidth * (1f - padding * 2f);
            float availableH = canvasHeight * (1f - padding * 2f);

            float scaleX = availableW / _width;
            float scaleY = availableH / _height;
            Scale = Math.Min(scaleX, scaleY);

            // Center on canvas
            _positionX = (canvasWidth - ScaledWidth) / 2f;
            _positionY = (canvasHeight - ScaledHeight) / 2f;
            _rotation = 0;

            OnPropertyChanged(nameof(PositionX));
            OnPropertyChanged(nameof(PositionY));
            OnPropertyChanged(nameof(Rotation));
            TransformChanged?.Invoke();
        }

        /// <summary>
        /// Releases allocated resources (for IDisposable).
        /// </summary>
        public void Dispose()
        {
            // Clear pixel data to allow GC to reclaim memory
            _pixels = null;
            
            // Clear the preview bitmap reference
            _previewBitmap = null;
            
            // Notify that the image data has been cleared
            OnPropertyChanged(nameof(Pixels));
            OnPropertyChanged(nameof(HasPixels));
            OnPropertyChanged(nameof(PreviewBitmap));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
