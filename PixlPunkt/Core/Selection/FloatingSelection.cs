using System;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using Windows.Graphics;

namespace PixlPunkt.Core.Selection
{
    /// <summary>
    /// Pixels that have been lifted off a layer and are being moved/transformed before being
    /// committed back. This is <em>document</em> state, owned by <c>CanvasDocument.Floating</c>,
    /// and it is bound to the layer it came from: no matter which layer becomes active later,
    /// commit, cancel and undo all operate on <see cref="Layer"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything the view needs to draw and hit-test the floating pixels lives here, so the
    /// history items, the save path and the view all see one object. The view's
    /// <c>SelectionSubsystem</c> forwards its floating/transform properties to this object.
    /// </para>
    /// <para>
    /// <see cref="SourceBounds"/> / <see cref="SourcePixelsBefore"/> remember what was under the
    /// selection before it was lifted so that cancelling (or undoing a discard) can put it back
    /// without consulting history. A pasted selection has no source.
    /// </para>
    /// </remarks>
    public sealed class FloatingSelection
    {
        /// <summary>The layer the pixels were lifted from and will be committed to.</summary>
        public RasterLayer Layer { get; }

        /// <summary>BGRA pixel buffer, <see cref="Width"/> × <see cref="Height"/> × 4. Scale is baked into it on release; rotation is not.</summary>
        public byte[] Pixels { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>Top-left of the (unrotated, unscaled) buffer in document coordinates. May be off-canvas.</summary>
        public int X { get; set; }
        public int Y { get; set; }

        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;
        public bool ScaleLink { get; set; }
        public ScaleMode ScaleFilter { get; set; } = ScaleMode.NearestNeighbor;
        public RotationMode RotMode { get; set; } = RotationMode.RotSprite;

        /// <summary>Rotation of the drag in progress, in degrees.</summary>
        public double AngleDeg { get; set; }
        /// <summary>Rotation already applied by previous drags, in degrees. Only baked into pixels at commit.</summary>
        public double CumulativeAngleDeg { get; set; }

        /// <summary>
        /// Handle-frame size: the unscaled buffer size (scale is never baked into the pixels
        /// before commit, so this equals <see cref="Width"/>/<see cref="Height"/>). Invariant: <see cref="OrigW"/>,
        /// <see cref="OrigH"/>, <see cref="OrigCenterX"/> and <see cref="OrigCenterY"/> are always
        /// valid. They are set in the constructor, every move shifts the centre with the float,
        /// and a bake re-centres and resizes them. Readers use them directly; there is no
        /// fallback to derive from X/Y or the buffer size.
        /// </summary>
        public int OrigW { get; set; }
        public int OrigH { get; set; }
        /// <summary>Handle-frame centre in document coordinates; the pivot orbits around this.</summary>
        public int OrigCenterX { get; set; }
        public int OrigCenterY { get; set; }

        public double PivotOffsetX { get; set; }
        public double PivotOffsetY { get; set; }
        public bool PivotCustom { get; set; }

        /// <summary>True when the lifted shape is not a plain rectangle (lasso, wand, paint).</summary>
        public bool RegionNonRectangular { get; set; }
        /// <summary>True once the buffer has been flipped, so the marquee must be traced from buffer alpha.</summary>
        public bool BufferFlipped { get; set; }

        /// <summary>Where on the layer the pixels were lifted from (empty for a paste).</summary>
        public RectInt32 SourceBounds { get; }
        /// <summary>The layer pixels under <see cref="SourceBounds"/> before the lift cleared them (empty for a paste).</summary>
        public byte[] SourcePixelsBefore { get; }
        /// <summary>False for pasted selections, which have nothing to restore on cancel.</summary>
        public bool HasSource => SourcePixelsBefore.Length > 0;

        public FloatingSelection(
            RasterLayer layer,
            byte[] pixels, int width, int height,
            int x, int y,
            RectInt32 sourceBounds, byte[] sourcePixelsBefore)
        {
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));
            Pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Floating selection must have a positive size.");
            if (pixels.Length != width * height * 4) throw new ArgumentException("Pixel buffer does not match the given size.", nameof(pixels));

            Width = width;
            Height = height;
            X = x;
            Y = y;
            OrigW = width;
            OrigH = height;
            OrigCenterX = x + width / 2;
            OrigCenterY = y + height / 2;
            SourceBounds = sourceBounds;
            SourcePixelsBefore = sourcePixelsBefore ?? Array.Empty<byte>();
        }

        /// <summary>Width after the current (unbaked) scale.</summary>
        public int ScaledW => Math.Max(1, (int)Math.Round(Width * ScaleX));
        /// <summary>Height after the current (unbaked) scale.</summary>
        public int ScaledH => Math.Max(1, (int)Math.Round(Height * ScaleY));

        /// <summary>Deep copy, used for history snapshots.</summary>
        public FloatingSelection Clone()
        {
            var c = new FloatingSelection(Layer, (byte[])Pixels.Clone(), Width, Height, X, Y, SourceBounds, SourcePixelsBefore)
            {
                ScaleX = ScaleX,
                ScaleY = ScaleY,
                ScaleLink = ScaleLink,
                ScaleFilter = ScaleFilter,
                RotMode = RotMode,
                AngleDeg = AngleDeg,
                CumulativeAngleDeg = CumulativeAngleDeg,
                OrigW = OrigW,
                OrigH = OrigH,
                OrigCenterX = OrigCenterX,
                OrigCenterY = OrigCenterY,
                PivotOffsetX = PivotOffsetX,
                PivotOffsetY = PivotOffsetY,
                PivotCustom = PivotCustom,
                RegionNonRectangular = RegionNonRectangular,
                BufferFlipped = BufferFlipped,
            };
            return c;
        }

        /// <summary>Copies the non-pixel transform state from another instance (pixels are shared by reference).</summary>
        public void CopyTransformFrom(FloatingSelection other)
        {
            X = other.X; Y = other.Y;
            ScaleX = other.ScaleX; ScaleY = other.ScaleY; ScaleLink = other.ScaleLink;
            ScaleFilter = other.ScaleFilter; RotMode = other.RotMode;
            AngleDeg = other.AngleDeg; CumulativeAngleDeg = other.CumulativeAngleDeg;
            OrigW = other.OrigW; OrigH = other.OrigH; OrigCenterX = other.OrigCenterX; OrigCenterY = other.OrigCenterY;
            PivotOffsetX = other.PivotOffsetX; PivotOffsetY = other.PivotOffsetY; PivotCustom = other.PivotCustom;
            RegionNonRectangular = other.RegionNonRectangular; BufferFlipped = other.BufferFlipped;
        }
    }
}
