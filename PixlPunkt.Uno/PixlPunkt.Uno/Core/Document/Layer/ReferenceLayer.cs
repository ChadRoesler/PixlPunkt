using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PixlPunkt.Uno.Core.Document.Layer
{
    /// <summary>
    /// Represents a reference image layer for tracing and artistic reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference layers display external images on the canvas to help artists trace,
    /// match colors, or maintain proportions while creating pixel art. They differ from
    /// <see cref="RasterLayer"/> in several key ways:
    /// </para>
    /// <list type="bullet">
    /// <item>Reference layers are NOT included in exports (image, animation, etc.)</item>
    /// <item>Reference layers render using smooth interpolation, not pixel-snapped</item>
    /// <item>Reference layers can extend beyond canvas boundaries</item>
    /// <item>Reference layers have transform properties (position, scale, rotation)</item>
    /// <item>Reference layers cannot be painted on</item>
    /// </list>
    /// <para>
    /// Reference layers participate in the layer stack ordering, allowing them to be
    /// positioned above or below other layers as needed.
    /// </para>
    /// </remarks>
    public sealed class ReferenceLayer : LayerBase
    {
        private byte[]? _pixels;
        private int _imageWidth;
        private int _imageHeight;
        private string? _filePath;

        private float _positionX;
        private float _positionY;
        private float _scale = 1.0f;
        private float _rotation;
        private byte _opacity = 128; // 50% opacity default

        private WriteableBitmap? _previewBitmap;

        /// <summary>
        /// Initializes a new empty reference layer.
        /// </summary>
        /// <param name="name">The layer name.</param>
        public ReferenceLayer(string name = "Reference")
        {
            Name = name;
        }

        /// <summary>
        /// Initializes a reference layer with pixel data.
        /// </summary>
        /// <param name="name">The layer name.</param>
        /// <param name="pixels">Pixel data in BGRA format.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <param name="filePath">Optional source file path.</param>
        public ReferenceLayer(string name, byte[] pixels, int width, int height, string? filePath = null)
        {
            Name = name;
            SetPixels(pixels, width, height);
            _filePath = filePath;
        }

        /// <inheritdoc/>
        public override bool CanHaveChildren => false;

        ///////////////////////////////////////////////////////////////////////
        // IMAGE DATA
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the pixel data (BGRA format).
        /// </summary>
        public byte[]? Pixels => _pixels;

        /// <summary>
        /// Gets the width of the reference image in pixels.
        /// </summary>
        public int ImageWidth => _imageWidth;

        /// <summary>
        /// Gets the height of the reference image in pixels.
        /// </summary>
        public int ImageHeight => _imageHeight;

        /// <summary>
        /// Gets or sets the original file path of the image.
        /// </summary>
        public string? FilePath
        {
            get => _filePath;
            set { if (_filePath != value) { _filePath = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets whether this reference layer has loaded pixel data.
        /// </summary>
        public bool HasPixels => _pixels != null && _pixels.Length > 0;

        /// <summary>
        /// Sets the pixel data for this reference layer.
        /// </summary>
        /// <param name="pixels">Pixel data in BGRA format.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        public void SetPixels(byte[] pixels, int width, int height)
        {
            _pixels = pixels;
            _imageWidth = width;
            _imageHeight = height;
            UpdatePreview();
            OnPropertyChanged(nameof(Pixels));
            OnPropertyChanged(nameof(ImageWidth));
            OnPropertyChanged(nameof(ImageHeight));
            OnPropertyChanged(nameof(HasPixels));
        }

        ///////////////////////////////////////////////////////////////////////
        // TRANSFORM PROPERTIES
        ///////////////////////////////////////////////////////////////////////

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
        /// Gets or sets the opacity of the reference image (0 = invisible, 255 = fully visible).
        /// </summary>
        public byte Opacity
        {
            get => _opacity;
            set { if (_opacity != value) { _opacity = value; OnPropertyChanged(); OnPropertyChanged(nameof(OpacityPercent)); } }
        }

        /// <summary>
        /// Gets or sets the opacity as a percentage (0-100).
        /// </summary>
        public int OpacityPercent
        {
            get => (int)(_opacity / 255f * 100f);
            set => Opacity = (byte)(value / 100f * 255f);
        }

        /// <summary>
        /// Raised when the transform (position, scale, rotation) changes.
        /// </summary>
        public event Action? TransformChanged;

        ///////////////////////////////////////////////////////////////////////
        // COMPUTED PROPERTIES
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the scaled width of the reference image.
        /// </summary>
        public float ScaledWidth => _imageWidth * _scale;

        /// <summary>
        /// Gets the scaled height of the reference image.
        /// </summary>
        public float ScaledHeight => _imageHeight * _scale;

        /// <summary>
        /// Gets the center X position of the reference image.
        /// </summary>
        public float CenterX => _positionX + ScaledWidth / 2f;

        /// <summary>
        /// Gets the center Y position of the reference image.
        /// </summary>
        public float CenterY => _positionY + ScaledHeight / 2f;

        ///////////////////////////////////////////////////////////////////////
        // HIT TESTING
        ///////////////////////////////////////////////////////////////////////

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

            float centerX = CenterX;
            float centerY = CenterY;

            float localX = docX - centerX;
            float localY = docY - centerY;

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

            localX += ScaledWidth / 2f;
            localY += ScaledHeight / 2f;

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
                (-w / 2f, -h / 2f),
                (w / 2f, -h / 2f),
                (w / 2f, h / 2f),
                (-w / 2f, h / 2f)
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
        /// Gets the edge midpoint positions of the reference image in document coordinates.
        /// Accounts for rotation.
        /// </summary>
        /// <returns>Array of 4 edge midpoints (Top, Right, Bottom, Left).</returns>
        public (float x, float y)[] GetEdgeMidpoints()
        {
            float w = ScaledWidth;
            float h = ScaledHeight;
            float cx = CenterX;
            float cy = CenterY;

            // Local space edge midpoints (relative to center)
            var localMidpoints = new (float x, float y)[]
            {
                (0, -h / 2f),      // Top
                (w / 2f, 0),       // Right
                (0, h / 2f),       // Bottom
                (-w / 2f, 0)       // Left
            };

            if (Math.Abs(_rotation) > 0.01f)
            {
                float radians = _rotation * MathF.PI / 180f;
                float cos = MathF.Cos(radians);
                float sin = MathF.Sin(radians);

                for (int i = 0; i < 4; i++)
                {
                    float rx = localMidpoints[i].x * cos - localMidpoints[i].y * sin;
                    float ry = localMidpoints[i].x * sin + localMidpoints[i].y * cos;
                    localMidpoints[i] = (cx + rx, cy + ry);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    localMidpoints[i] = (cx + localMidpoints[i].x, cy + localMidpoints[i].y);
                }
            }

            return localMidpoints;
        }

        /// <summary>
        /// Tests if a point is near an edge midpoint handle. Returns the edge index (0-3) or -1.
        /// </summary>
        /// <param name="docX">X coordinate in document space.</param>
        /// <param name="docY">Y coordinate in document space.</param>
        /// <param name="handleRadius">Radius around midpoints for hit detection.</param>
        /// <returns>Edge index (0=Top, 1=Right, 2=Bottom, 3=Left) or -1.</returns>
        public int HitTestEdge(float docX, float docY, float handleRadius)
        {
            var midpoints = GetEdgeMidpoints();
            float radiusSq = handleRadius * handleRadius;

            for (int i = 0; i < 4; i++)
            {
                float dx = docX - midpoints[i].x;
                float dy = docY - midpoints[i].y;
                if (dx * dx + dy * dy <= radiusSq)
                    return i;
            }

            return -1;
        }

        ///////////////////////////////////////////////////////////////////////
        // TRANSFORM UTILITIES
        ///////////////////////////////////////////////////////////////////////

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

            float scaleX = availableW / _imageWidth;
            float scaleY = availableH / _imageHeight;
            Scale = Math.Min(scaleX, scaleY);

            _positionX = (canvasWidth - ScaledWidth) / 2f;
            _positionY = (canvasHeight - ScaledHeight) / 2f;
            _rotation = 0;

            OnPropertyChanged(nameof(PositionX));
            OnPropertyChanged(nameof(PositionY));
            OnPropertyChanged(nameof(Rotation));
            TransformChanged?.Invoke();
        }

        ///////////////////////////////////////////////////////////////////////
        // PREVIEW
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the preview image source for UI display.
        /// </summary>
        public ImageSource? Preview => _previewBitmap;

        /// <summary>
        /// Updates the preview bitmap from the current pixel data.
        /// </summary>
        public void UpdatePreview()
        {
            if (_pixels == null || _imageWidth <= 0 || _imageHeight <= 0)
            {
                _previewBitmap = null;
                OnPropertyChanged(nameof(Preview));
                return;
            }

            try
            {
                _previewBitmap = new WriteableBitmap(_imageWidth, _imageHeight);
                using var stream = _previewBitmap.PixelBuffer.AsStream();
                stream.Write(_pixels, 0, _pixels.Length);
                OnPropertyChanged(nameof(Preview));
            }
            catch
            {
                _previewBitmap = null;
                OnPropertyChanged(nameof(Preview));
            }
        }

        /// <summary>
        /// Manually triggers a PropertyChanged notification for the Preview property.
        /// </summary>
        public void NotifyPreviewChanged() => OnPropertyChanged(nameof(Preview));
    }
}
