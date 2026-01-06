using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Uno.Core.Compositing.Helpers;
using PixlPunkt.Uno.Core.Effects;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.Core.Tile;
using Windows.Foundation;

namespace PixlPunkt.Uno.Core.Document.Layer
{
    /// <summary>
    /// Represents a raster (pixel-based) layer with a surface, blend mode, opacity, effects, and preview image.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RasterLayer is the primary drawable layer type in PixlPunkt. It contains:
    /// - A <see cref="PixelSurface"/> for pixel data
    /// - Blend mode and opacity for compositing
    /// - A collection of <see cref="LayerEffectBase"/> effects applied during rendering
    /// - An auto-updating preview bitmap for UI display
    /// - A <see cref="TileMapping"/> for tile-based editing (optional)
    /// </para>
    /// <para>
    /// The preview is automatically regenerated whenever the surface pixels change, showing
    /// the layer content with transparency visible. Effects are not applied to the preview;
    /// they are only applied during final compositing via <see cref="Compositor"/>.
    /// </para>
    /// <para>
    /// All registered effects are pre-added to the <see cref="Effects"/> collection at construction
    /// with <see cref="LayerEffectBase.IsEnabled"/> set to false, allowing UI to toggle them on/off.
    /// </para>
    /// </remarks>
    public sealed partial class RasterLayer : LayerBase
    {
        private const byte DefaultOpacity = 255;
        private const BlendMode DefaultBlend = BlendMode.Normal;

        private byte _opacity = DefaultOpacity;
        private BlendMode _blend = DefaultBlend;

        private WriteableBitmap _previewBitmap;

        private bool _updatingPreview;

        private TileMapping? _tileMapping;

        /// <summary>
        /// Initializes a new instance of the <see cref="RasterLayer"/> class with specified dimensions and name.
        /// </summary>
        /// <param name="w">The width in pixels. Must be positive.</param>
        /// <param name="h">The height in pixels. Must be positive.</param>
        /// <param name="name">The layer name. Default is "Layer".</param>
        /// <remarks>
        /// <para>
        /// Creates a transparent pixel surface and initializes all available effects in disabled state.
        /// The preview bitmap is created and initially rendered. The layer subscribes to surface pixel
        /// changes to automatically update the preview.
        /// </para>
        /// <para>
        /// Effects are dynamically populated from <see cref="EffectRegistry.Shared"/>, allowing
        /// both built-in and plugin effects to be available on new layers.
        /// </para>
        /// </remarks>
        public RasterLayer(int w, int h, string name = "Layer")
        {
            Surface = new PixelSurface(w, h);
            Name = name;

            _previewBitmap = new WriteableBitmap(w, h);

            // Populate effects from the registry (built-in + plugins)
            SyncEffectsFromRegistry();

            // Subscribe to registry changes for plugin effect support
            EffectRegistry.Shared.EffectRegistered += OnEffectRegistered;
            EffectRegistry.Shared.EffectUnregistered += OnEffectUnregistered;

            UpdatePreview(); // initial (blank over pattern)

            Surface.PixelsChanged += OnSurfacePixelsChanged;
        }

        private void OnSurfacePixelsChanged() => UpdatePreview();

        /// <summary>
        /// Called when a new effect is registered (e.g., plugin loaded).
        /// Adds the effect to this layer if not already present.
        /// </summary>
        private void OnEffectRegistered(IEffectRegistration registration)
        {
            // Check if we already have this effect
            if (Effects.Any(e => e.EffectId == registration.Id))
                return;

            var effect = registration.CreateInstance();
            effect.IsEnabled = false;
            Effects.Add(effect);
        }

        /// <summary>
        /// Called when an effect is unregistered (e.g., plugin unloaded).
        /// Removes the effect from this layer if present and disabled.
        /// </summary>
        private void OnEffectUnregistered(string effectId)
        {
            var effect = Effects.FirstOrDefault(e => e.EffectId == effectId);
            if (effect != null && !effect.IsEnabled)
            {
                // Only remove if disabled (don't break active effects)
                Effects.Remove(effect);
            }
        }

        /// <summary>
        /// Synchronizes the effects collection with the current registry state.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method ensures the layer has all registered effects from the registry.
        /// It's called at construction and can be called manually to refresh effects
        /// after bulk plugin operations.
        /// </para>
        /// <para>
        /// Effects that are already in the collection (by ID) are not duplicated.
        /// New effects are added in disabled state.
        /// </para>
        /// </remarks>
        public void SyncEffectsFromRegistry()
        {
            var existingIds = Effects.Select(e => e.EffectId).ToHashSet();

            foreach (var registration in EffectRegistry.Shared.GetAll())
            {
                if (existingIds.Contains(registration.Id))
                    continue;

                var effect = registration.CreateInstance();
                effect.IsEnabled = false;
                Effects.Add(effect);
            }
        }

        /// <summary>
        /// Gets the pixel surface containing the layer's drawable content.
        /// </summary>
        /// <value>
        /// A <see cref="PixelSurface"/> in BGRA format matching the layer dimensions.
        /// </value>
        /// <remarks>
        /// This is the surface that drawing tools write to and that gets composited with other layers.
        /// </remarks>
        public readonly PixelSurface Surface;

        /// <summary>
        /// Gets or sets the layer opacity (0 = fully transparent, 255 = fully opaque).
        /// </summary>
        /// <value>
        /// A value from 0 to 255. Default is 255.
        /// </value>
        /// <remarks>
        /// Fires <see cref="LayerBase.PropertyChanged"/> when set to a different value.
        /// This opacity is applied as a global multiplier during layer compositing.
        /// </remarks>
        public byte Opacity
        {
            get => _opacity;
            set
            {
                if (value == _opacity) return;
                _opacity = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the blend mode used when compositing this layer with layers below.
        /// </summary>
        /// <value>
        /// A <see cref="BlendMode"/> value. Default is <see cref="BlendMode.Normal"/>.
        /// </value>
        /// <remarks>
        /// Fires <see cref="LayerBase.PropertyChanged"/> when set to a different value.
        /// Blend modes determine how this layer's colors mix with the layers beneath it
        /// (e.g., Multiply, Screen, Overlay, Additive).
        /// </remarks>
        public BlendMode Blend
        {
            get => _blend;
            set
            {
                if (value == _blend) return;
                _blend = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets a value indicating whether this layer type can contain child layers.
        /// </summary>
        /// <value>
        /// Always <c>false</c> for <see cref="RasterLayer"/>. Only <see cref="LayerFolder"/> can have children.
        /// </value>
        public override bool CanHaveChildren => false;

        /// <summary>
        /// Gets the collection of layer effects applied during compositing.
        /// </summary>
        /// <value>
        /// An observable collection of <see cref="LayerEffectBase"/> instances.
        /// </value>
        /// <remarks>
        /// <para>
        /// Effects in this collection are applied in order during compositing if their
        /// <see cref="LayerEffectBase.IsEnabled"/> property is true.
        /// </para>
        /// <para>
        /// All registered effects (built-in and plugin) are pre-added at construction in disabled state.
        /// UI can toggle effects on/off and configure their settings.
        /// </para>
        /// </remarks>
        public ObservableCollection<LayerEffectBase> Effects { get; } = [];

        /// <summary>
        /// Gets or sets the tile mapping for this layer.
        /// </summary>
        /// <value>
        /// A <see cref="TileMapping"/> instance, or null if no tile mapping is associated.
        /// </value>
        /// <remarks>
        /// <para>
        /// The tile mapping stores which tile is placed at each grid position for this layer.
        /// Each layer can have independent tile mappings, allowing different tile arrangements.
        /// </para>
        /// <para>
        /// The mapping is created lazily when first accessed via <see cref="GetOrCreateTileMapping"/>.
        /// </para>
        /// </remarks>
        public TileMapping? TileMapping
        {
            get => _tileMapping;
            set
            {
                if (_tileMapping == value) return;
                _tileMapping = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the tile mapping for this layer, creating it if necessary.
        /// </summary>
        /// <param name="tileCountX">Number of tile columns (required if creating new mapping).</param>
        /// <param name="tileCountY">Number of tile rows (required if creating new mapping).</param>
        /// <returns>The tile mapping for this layer.</returns>
        public TileMapping GetOrCreateTileMapping(int tileCountX, int tileCountY)
        {
            _tileMapping ??= new TileMapping(tileCountX, tileCountY);
            return _tileMapping;
        }

        /// <summary>
        /// Checks if this layer has any tile mappings.
        /// </summary>
        /// <returns>True if the layer has a tile mapping with at least one tile placed.</returns>
        public bool HasTileMappings()
        {
            return _tileMapping?.HasAnyMappings() == true;
        }

        /// <summary>
        /// Gets the preview image source for UI display.
        /// </summary>
        /// <value>
        /// A <see cref="WriteableBitmap"/> showing the layer content with transparency visible.
        /// </value>
        /// <remarks>
        /// The preview is automatically updated whenever <see cref="Surface"/> pixels change.
        /// Effects are not applied to the preview; it shows raw pixel data only.
        /// </remarks>
        public ImageSource Preview => _previewBitmap;

        /// <summary>
        /// Gets the bounding rectangle of the layer in canvas coordinates.
        /// </summary>
        /// <value>
        /// A <see cref="Rect"/> spanning (0, 0) to (Width, Height), or null if no content.
        /// </value>
        public Rect? Bounds => new Rect(0, 0, Surface.Width, Surface.Height);

        // ════════════════════════════════════════════════════════════════════
        // LAYER MASK
        // ════════════════════════════════════════════════════════════════════

        private LayerMask? _mask;
        private bool _isEditingMask;

        /// <summary>
        /// Gets the surface that should be painted on - either the layer surface or mask surface
        /// depending on the current editing mode.
        /// </summary>
        /// <returns>The surface to paint on.</returns>
        public PixelSurface GetPaintingSurface()
        {
            if (_isEditingMask && _mask != null)
            {
                return _mask.Surface;
            }
            return Surface;
        }

        /// <summary>
        /// Gets or sets whether the user is currently editing the mask (vs the layer pixels).
        /// When true, painting operations should target the mask instead of the layer surface.
        /// </summary>
        public bool IsEditingMask
        {
            get => _isEditingMask;
            set
            {
                if (_isEditingMask == value) return;

                // Can only edit mask if one exists
                if (value && _mask == null)
                    return;

                _isEditingMask = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the layer mask for non-destructive editing.
        /// </summary>
        /// <value>
        /// A <see cref="LayerMask"/> instance, or null if no mask is attached.
        /// </value>
        /// <remarks>
        /// <para>
        /// Layer masks allow non-destructive hiding of portions of a layer.
        /// White areas in the mask reveal the layer; black areas hide it.
        /// </para>
        /// </remarks>
        public LayerMask? Mask
        {
            get => _mask;
            set
            {
                if (_mask == value) return;

                // Unhook old mask events
                if (_mask != null)
                {
                    _mask.MaskChanged -= OnMaskChanged;
                }

                _mask = value;

                // Exit mask editing mode if mask is removed
                if (_mask == null)
                {
                    _isEditingMask = false;
                    OnPropertyChanged(nameof(IsEditingMask));
                }

                // Hook new mask events
                if (_mask != null)
                {
                    _mask.MaskChanged += OnMaskChanged;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMask));
                OnPropertyChanged(nameof(MaskPreview));
            }
        }

        /// <summary>
        /// Gets whether this layer has a mask attached.
        /// </summary>
        public bool HasMask => _mask != null;

        /// <summary>
        /// Gets the mask preview image source, or null if no mask.
        /// </summary>
        public ImageSource? MaskPreview => _mask?.Preview;

        /// <summary>
        /// Creates and attaches a new layer mask.
        /// </summary>
        /// <returns>The newly created mask.</returns>
        public LayerMask CreateMask()
        {
            Mask = new LayerMask(Surface.Width, Surface.Height);
            return Mask;
        }

        /// <summary>
        /// Removes the layer mask.
        /// </summary>
        public void RemoveMask()
        {
            Mask = null;
        }

        /// <summary>
        /// Applies the mask to the layer permanently (destructive).
        /// After this operation, the mask is removed and masked areas become transparent.
        /// </summary>
        public void ApplyMask()
        {
            if (_mask == null || !_mask.IsEnabled) return;

            var pixels = Surface.Pixels;
            int w = Surface.Width;
            int h = Surface.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = (y * w + x) * 4;
                    byte maskValue = _mask.GetEffectiveMaskValue(x, y);

                    // Multiply alpha by mask value
                    byte currentAlpha = pixels[idx + 3];
                    pixels[idx + 3] = (byte)((currentAlpha * maskValue) / 255);
                }
            }

            // Remove the mask after applying
            Mask = null;
            Surface.NotifyChanged();
        }

        /// <summary>
        /// Gets the effective alpha at a pixel position, considering the mask.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>Effective alpha value (0-255).</returns>
        public byte GetEffectiveAlpha(int x, int y)
        {
            if (x < 0 || x >= Surface.Width || y < 0 || y >= Surface.Height)
                return 0;

            int idx = (y * Surface.Width + x) * 4;
            byte pixelAlpha = Surface.Pixels[idx + 3];

            if (_mask == null || !_mask.IsEnabled)
                return pixelAlpha;

            byte maskValue = _mask.GetEffectiveMaskValue(x, y);
            return (byte)((pixelAlpha * maskValue) / 255);
        }

        private void OnMaskChanged()
        {
            OnPropertyChanged(nameof(MaskPreview));
        }

        /// <summary>
        /// Rebuilds the preview bitmap from the current surface pixels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method copies pixel data from <see cref="Surface"/> to the preview bitmap.
        /// It's automatically called when surface pixels change via <see cref="PixelSurface.PixelsChanged"/>.
        /// </para>
        /// <para>
        /// The preview shows raw BGRA pixel data with alpha preserved, allowing the UI to
        /// composite it over a transparency pattern background.
        /// </para>
        /// <para>
        /// Includes reentrancy protection to prevent recursive updates.
        /// If the surface dimensions have changed, the preview bitmap is recreated.
        /// </para>
        /// </remarks>
        public void UpdatePreview()
        {
            if (_updatingPreview) return;
            _updatingPreview = true;
            try
            {
                int w = Surface.Width;
                int h = Surface.Height;
                var src = Surface.Pixels;

                // Recreate preview bitmap if dimensions changed
                if (_previewBitmap.PixelWidth != w || _previewBitmap.PixelHeight != h)
                {
                    _previewBitmap = new WriteableBitmap(w, h);
                }

                using var stream = _previewBitmap.PixelBuffer.AsStream();
                stream.Seek(0, SeekOrigin.Begin);

                byte[] row = new byte[w * 4];

                for (int y = 0; y < h; y++)
                {
                    int srcRow = y * w * 4;

                    for (int x = 0; x < w; x++)
                    {
                        int si = srcRow + x * 4;
                        int di = x * 4;

                        // Copy BGRA as-is, preserve A so UI can composite over pattern
                        row[di + 0] = src[si + 0];
                        row[di + 1] = src[si + 1];
                        row[di + 2] = src[si + 2];
                        row[di + 3] = src[si + 3];
                    }

                    stream.Write(row, 0, row.Length);
                }
            }
            finally
            {
                _updatingPreview = false;
            }

            OnPropertyChanged(nameof(Preview));
        }

        /// <summary>
        /// Manually triggers a <see cref="LayerBase.PropertyChanged"/> notification for the <see cref="Preview"/> property.
        /// </summary>
        /// <remarks>
        /// Use this when the preview bitmap content has been updated externally (e.g., effects applied)
        /// and the UI needs to refresh without a full <see cref="UpdatePreview"/> regeneration.
        /// </remarks>
        public void NotifyPreviewChanged() => OnPropertyChanged(nameof(Preview));

        /// <summary>
        /// Replaces the layer's surface with new dimensions and pixel data.
        /// </summary>
        /// <param name="newSurface">The new pixel surface with resized dimensions and data.</param>
        /// <remarks>
        /// <para>
        /// This method is used during canvas resize operations. The existing <see cref="Surface"/>
        /// object is resized in place using <see cref="PixelSurface.Resize"/>, preserving the
        /// object reference while updating its internal data.
        /// </para>
        /// <para>
        /// After calling this method, call <see cref="UpdatePreview"/> to regenerate the preview bitmap.
        /// </para>
        /// </remarks>
        public void ReplaceSurface(PixelSurface newSurface)
        {
            if (newSurface == null) return;
            Surface.Resize(newSurface.Width, newSurface.Height, newSurface.Pixels);
        }
    }
}