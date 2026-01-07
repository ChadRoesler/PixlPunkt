using System;
using System.Collections.Generic;
using FluentIcons.Common;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.IO;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Tools;
using PixlPunkt.Core.Tools.Selection;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.Core.Tools.Utility;
using PixlPunkt.PluginSdk.Tile;
using Windows.Foundation;
using SdkBrushShape = PixlPunkt.PluginSdk.Imaging.BrushShape;
// SDK types that don't have global aliases
using SdkBrushToolRegistration = PixlPunkt.PluginSdk.Tools.BrushToolRegistration;
using SdkIEffectRegistration = PixlPunkt.PluginSdk.Effects.IEffectRegistration;
using SdkIExportRegistration = PixlPunkt.PluginSdk.IO.IExportRegistration;
using SdkIImportRegistration = PixlPunkt.PluginSdk.IO.IImportRegistration;
using SdkISelectionTool = PixlPunkt.PluginSdk.Selection.ISelectionTool;
using SdkIShapeBuilder = PixlPunkt.PluginSdk.Shapes.IShapeBuilder;
using SdkIStrokePainter = PixlPunkt.PluginSdk.Painting.IStrokePainter;
using SdkITileContext = PixlPunkt.PluginSdk.Tile.ITileContext;
using SdkITileHandler = PixlPunkt.PluginSdk.Tile.ITileHandler;
using SdkIUtilityContext = PixlPunkt.PluginSdk.Utility.IUtilityContext;
using SdkIUtilityHandler = PixlPunkt.PluginSdk.Utility.IUtilityHandler;
using SdkLayerEffectBase = PixlPunkt.PluginSdk.Compositing.LayerEffectBase;
using SdkPixelChangeResult = PixlPunkt.PluginSdk.Painting.PixelChangeResult;
using SdkPixelSurface = PixlPunkt.PluginSdk.Imaging.PixelSurface;
using SdkSelectionToolRegistration = PixlPunkt.PluginSdk.Tools.SelectionToolRegistration;
using SdkShapeToolRegistration = PixlPunkt.PluginSdk.Tools.ShapeToolRegistration;
using SdkStrokeContext = PixlPunkt.PluginSdk.Painting.StrokeContext;
using SdkTileCursorHint = PixlPunkt.PluginSdk.Tile.TileCursorHint;
using SdkTileToolRegistration = PixlPunkt.PluginSdk.Tools.TileToolRegistration;
using SdkToolSettingsBase = PixlPunkt.PluginSdk.Settings.ToolSettingsBase;
using SdkUtilityCursorHint = PixlPunkt.PluginSdk.Utility.UtilityCursorHint;
using SdkUtilityToolRegistration = PixlPunkt.PluginSdk.Tools.UtilityToolRegistration;


namespace PixlPunkt.Core.Plugins
{
    /// <summary>
    /// Adapts SDK BrushToolRegistration to main project's IToolRegistration.
    /// </summary>
    internal sealed class PluginBrushToolRegistration : IToolRegistration, IToolBehavior
    {
        private readonly SdkBrushToolRegistration _sdkRegistration;
        private readonly PluginToolSettings? _adaptedSettings;

        public PluginBrushToolRegistration(SdkBrushToolRegistration sdkRegistration)
        {
            _sdkRegistration = sdkRegistration ?? throw new ArgumentNullException(nameof(sdkRegistration));

            if (sdkRegistration.Settings != null)
            {
                _adaptedSettings = new PluginToolSettings(sdkRegistration.Settings);
            }
        }

        public string Id => _sdkRegistration.Id;
        public ToolCategory Category => ToolCategory.Brush;
        public string DisplayName => _sdkRegistration.DisplayName;
        public ToolSettingsBase? Settings => _adaptedSettings;

        /// <summary>
        /// Gets whether this tool has a painter.
        /// </summary>
        public bool HasPainter => _sdkRegistration.HasPainter;

        /// <summary>
        /// Gets the settings as IStrokeSettings if applicable.
        /// </summary>
        public IStrokeSettings? StrokeSettings => _adaptedSettings as IStrokeSettings;

        /// <summary>
        /// Creates a new painter instance for this tool.
        /// /// </summary>
        /// <returns>An adapted painter that wraps the SDK painter.</returns>
        public IStrokePainter? CreatePainter()
        {
            var sdkPainter = _sdkRegistration.CreatePainter();
            if (sdkPainter == null) return null;
            return new PluginStrokePainterAdapter(sdkPainter);
        }

        //////////////////////////////////////////////////////////////////
        // IToolBehavior implementation
        //////////////////////////////////////////////////////////////////

        string IToolBehavior.ToolId => Id;

        public ToolInputPattern InputPattern => ToolInputPattern.Stroke;

        public bool HandlesRightClick => false;

        public bool SuppressRmbDropper => false; // Brush tools allow RMB dropper for quick color sampling

        public bool SupportsModifiers => false;

        public ToolOverlayStyle OverlayStyle => ToolOverlayStyle.Outline;

        public bool OverlayVisibleWhileActive => true;

        public bool UsesPainter => HasPainter;

        public bool ModifiesPixels => true;
    }

    /// <summary>
    /// Adapts SDK ToolSettingsBase to main project's ToolSettingsBase.
    /// Implements IStrokeSettings, IOpacitySettings, IDensitySettings by forwarding to SDK settings
    /// or using local state for shape tools.
    /// </summary>
    internal sealed class PluginToolSettings : ToolSettingsBase, IStrokeSettings, IOpacitySettings, IDensitySettings
    {
        private readonly SdkToolSettingsBase _sdkSettings;

        // Cache interface checks for read-only access
        private readonly IStrokeSettings? _sdkStrokeSettings;
        private readonly IOpacitySettings? _sdkOpacitySettings;
        private readonly IDensitySettings? _sdkDensitySettings;

        // Local state for shape tools (when SDK settings don't implement the interfaces)
        private int _localSize = 1;
        private BrushShape _localShape = BrushShape.Circle;
        private byte _localOpacity = 255;
        private byte _localDensity = 255;
        private bool _filled = false;

        public PluginToolSettings(SdkToolSettingsBase sdkSettings)
        {
            _sdkSettings = sdkSettings ?? throw new ArgumentNullException(nameof(sdkSettings));
            _sdkSettings.Changed += () => RaiseChanged();

            // Cache interface implementations for fast lookup (read-only)
            _sdkStrokeSettings = sdkSettings as IStrokeSettings;
            _sdkOpacitySettings = sdkSettings as IOpacitySettings;
            _sdkDensitySettings = sdkSettings as IDensitySettings;
        }

        public override Icon Icon => _sdkSettings.Icon;
        public override String DisplayName => _sdkSettings.DisplayName;
        public override String Description => _sdkSettings.Description;

        public override KeyBinding? Shortcut
        {
            get
            {
                var sdkShortcut = _sdkSettings.Shortcut;
                if (sdkShortcut == null) return null;

                // SDK KeyBinding already uses SDK VirtualKey, just return it directly
                // Both Core and SDK now use the same KeyBinding type via global using
                return sdkShortcut;
            }
        }

        /// <summary>
        /// Gets or sets the shared selection settings (injected by the adapter for selection tools).
        /// When set, allows plugin selection tools to include universal transform options.
        /// </summary>
        public SelectionToolSettings? SelectionSettings { get; set; }

        /// <summary>
        /// Gets or sets whether this is a shape tool (enables Filled toggle and standard shape options).
        /// </summary>
        public bool IsShapeTool { get; set; }

        /// <summary>
        /// Gets or sets whether the shape should be filled (for shape tools).
        /// </summary>
        public bool Filled
        {
            get => _filled;
            set
            {
                if (_filled == value) return;
                _filled = value;
                RaiseChanged();
            }
        }

        /// <summary>
        /// Sets whether the shape is filled.
        /// </summary>
        public void SetFilled(bool value) => Filled = value;

        public override IEnumerable<IToolOption> GetOptions()
        {
            // For shape tools, add standard shape options first (brush shape, stroke width, etc.)
            if (IsShapeTool)
            {
                yield return new ShapeOption("brushShape", "Brush", Shape, SetShape, Order: 0);
                yield return new SliderOption("strokeWidth", "Stroke", 1, 128, Size, v => SetSize((int)v), Order: 1);
                yield return new SliderOption("opacity", "Opacity", 0, 255, Opacity, v => SetOpacity((byte)v), Order: 2);
                yield return new SliderOption("density", "Density", 0, 255, Density, v => SetDensity((byte)v), Order: 3);
                yield return new SeparatorOption(Order: 4);
                yield return new ToggleOption("filled", "Filled", _filled, SetFilled, Order: 5);
                yield return new SeparatorOption(Order: 6);
            }

            // SDK options from the plugin (tool-specific options like star point count)
            foreach (var opt in _sdkSettings.GetOptions())
            {
                yield return opt;
            }

            // For selection tools, add the shared selection transform options when selection is active
            if (SelectionSettings is { Active: true })
            {
                yield return new SeparatorOption(Order: 100);
                foreach (var opt in SelectionSettings.GetTransformOptions(baseOrder: 101))
                    yield return opt;
            }
        }

        //////////////////////////////////////////////////////////////////
        // IStrokeSettings implementation (read-only interface)
        // For shape tools without SDK stroke settings, use local state
        //////////////////////////////////////////////////////////////////

        public int Size => _sdkStrokeSettings?.Size ?? _localSize;

        public BrushShape Shape => _sdkStrokeSettings?.Shape ?? _localShape;

        //////////////////////////////////////////////////////////////////
        // IOpacitySettings implementation (read-only interface)
        //////////////////////////////////////////////////////////////////

        public byte Opacity => _sdkOpacitySettings?.Opacity ?? _localOpacity;

        //////////////////////////////////////////////////////////////////
        // IDensitySettings implementation (read-only interface)
        //////////////////////////////////////////////////////////////////

        public byte Density => _sdkDensitySettings?.Density ?? _localDensity;

        //////////////////////////////////////////////////////////////////
        // Setters for shape tools (update local state only)
        // These are used by the options panel to modify local state
        //////////////////////////////////////////////////////////////////

        public void SetSize(int size)
        {
            size = Math.Clamp(size, 1, 128);
            if (_localSize != size)
            {
                _localSize = size;
                RaiseChanged();
            }
        }

        public void SetShape(BrushShape shape)
        {
            if (_localShape != shape)
            {
                _localShape = shape;
                RaiseChanged();
            }
        }

        public void SetOpacity(byte opacity)
        {
            if (_localOpacity != opacity)
            {
                _localOpacity = opacity;
                RaiseChanged();
            }
        }

        public void SetDensity(byte density)
        {
            if (_localDensity != density)
            {
                _localDensity = density;
                RaiseChanged();
            }
        }
    }

    /// <summary>
    /// Adapts SDK IStrokePainter to main project's IStrokePainter.
    /// </summary>
    internal sealed class PluginStrokePainterAdapter : IStrokePainter
    {
        private readonly SdkIStrokePainter _sdkPainter;
        private RasterLayer? _layer;
        private SdkPixelSurface? _sdkSurface;

        public PluginStrokePainterAdapter(SdkIStrokePainter sdkPainter)
        {
            _sdkPainter = sdkPainter ?? throw new ArgumentNullException(nameof(sdkPainter));
        }

        public bool NeedsSnapshot => _sdkPainter.NeedsSnapshot;

        public void Begin(RasterLayer layer, byte[]? snapshot)
        {
            _layer = layer;

            // Create SDK surface that shares the layer's pixel data
            _sdkSurface = new SdkPixelSurface(layer.Surface.Width, layer.Surface.Height);
            // Copy pixels to SDK surface (they share the same format)
            Array.Copy(layer.Surface.Pixels, _sdkSurface.Pixels, layer.Surface.Pixels.Length);

            _sdkPainter.Begin(_sdkSurface, snapshot);
        }

        public void StampAt(int cx, int cy, StrokeContext context)
        {
            if (_sdkSurface == null || _layer == null) return;

            var sdkContext = CreateSdkContext(context);
            _sdkPainter.StampAt(cx, cy, sdkContext);

            // Sync pixels back to main surface
            SyncPixelsToMain();
        }

        public void StampLine(int x0, int y0, int x1, int y1, StrokeContext context)
        {
            if (_sdkSurface == null || _layer == null) return;

            var sdkContext = CreateSdkContext(context);
            _sdkPainter.StampLine(x0, y0, x1, y1, sdkContext);

            // Sync pixels back to main surface
            SyncPixelsToMain();
        }

        public IRenderResult? End(string description = "Brush Stroke", Icon icon = Icon.History)
        {
            if (_layer == null || _sdkSurface == null)
            {
                _layer = null;
                _sdkSurface = null;
                return null;
            }

            // Final sync before ending
            SyncPixelsToMain();

            var sdkResult = _sdkPainter.End(description);

            // Convert SDK result to main project result
            if (sdkResult is SdkPixelChangeResult sdkPixelResult && sdkPixelResult.HasChanges)
            {
                var item = new PixelChangeItem(_layer, description, icon);
                foreach (var change in sdkPixelResult.Changes)
                {
                    item.Add(change.ByteIndex, change.Before, change.After);
                }

                var result = item.IsEmpty ? null : item;
                _layer = null;
                _sdkSurface = null;
                return result;
            }

            _layer = null;
            _sdkSurface = null;
            return null;
        }

        private SdkStrokeContext CreateSdkContext(StrokeContext ctx)
        {
            return new SdkStrokeContext
            {
                Surface = _sdkSurface!,
                ForegroundColor = ctx.ForegroundColor,
                BackgroundColor = ctx.BackgroundColor,
                BrushSize = ctx.BrushSize,
                BrushShape = (SdkBrushShape)(int)ctx.BrushShape,
                BrushDensity = ctx.BrushDensity,
                BrushOpacity = ctx.BrushOpacity,
                IsCustomBrush = ctx.IsCustomBrush,
                CustomBrushFullName = ctx.CustomBrushFullName,
                BrushOffsets = ctx.BrushOffsets,
                ComputeAlphaAtOffset = ctx.ComputeAlphaAtOffset,
                Snapshot = ctx.Snapshot
            };
        }

        private void SyncPixelsToMain()
        {
            if (_sdkSurface == null || _layer == null) return;
            Array.Copy(_sdkSurface.Pixels, _layer.Surface.Pixels, _layer.Surface.Pixels.Length);
        }
    }

    /// <summary>
    /// Adapts SDK IEffectRegistration to main project's IEffectRegistration.
    /// </summary>
    internal sealed class PluginEffectRegistration : IEffectRegistration
    {
        private readonly SdkIEffectRegistration _sdkRegistration;

        public PluginEffectRegistration(SdkIEffectRegistration sdkRegistration)
        {
            _sdkRegistration = sdkRegistration ?? throw new ArgumentNullException(nameof(sdkRegistration));
        }

        public string Id => _sdkRegistration.Id;
        public EffectCategory Category => (EffectCategory)(int)_sdkRegistration.Category;
        public string DisplayName => _sdkRegistration.DisplayName;
        public string Description => _sdkRegistration.Description;

        public LayerEffectBase CreateInstance()
        {
            var sdkEffect = _sdkRegistration.CreateInstance();
            return new PluginLayerEffectAdapter(sdkEffect, Id);
        }

        public IEnumerable<IToolOption> GetOptions(LayerEffectBase effect)
        {
            if (effect is PluginLayerEffectAdapter adapter)
            {
                // SDK options are now the same types as Core options via global usings
                // Just forward them directly - no adaptation needed!
                return _sdkRegistration.GetOptions(adapter.SdkEffect);
            }
            return [];
        }
    }

    /// <summary>
    /// Adapts SDK LayerEffectBase to main project's LayerEffectBase.
    /// </summary>
    internal sealed class PluginLayerEffectAdapter : LayerEffectBase
    {
        private readonly SdkLayerEffectBase _sdkEffect;

        public PluginLayerEffectAdapter(SdkLayerEffectBase sdkEffect, string effectId)
        {
            _sdkEffect = sdkEffect ?? throw new ArgumentNullException(nameof(sdkEffect));
            EffectId = effectId;
            _sdkEffect.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        internal SdkLayerEffectBase SdkEffect => _sdkEffect;

        public override string DisplayName => _sdkEffect.DisplayName;

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            _sdkEffect.Apply(pixels, width, height);
        }
    }

    /// <summary>
    /// Adapts SDK ShapeToolRegistration to main project's IShapeToolRegistration.
    /// </summary>
    internal sealed class PluginShapeToolRegistration : IShapeToolRegistration, IToolBehavior
    {
        private readonly SdkShapeToolRegistration _sdkRegistration;
        private readonly PluginToolSettings? _adaptedSettings;
        private readonly IShapeBuilder? _shapeBuilder;

        public PluginShapeToolRegistration(SdkShapeToolRegistration sdkRegistration)
        {
            _sdkRegistration = sdkRegistration ?? throw new ArgumentNullException(nameof(sdkRegistration));

            if (sdkRegistration.Settings != null)
            {
                _adaptedSettings = new PluginToolSettings(sdkRegistration.Settings)
                {
                    IsShapeTool = true  // Enable standard shape options
                };
            }

            // Create adapted shape builder if factory exists
            var sdkBuilder = sdkRegistration.CreateShapeBuilder();
            if (sdkBuilder != null)
            {
                _shapeBuilder = new PluginShapeBuilderAdapter(sdkBuilder);
            }
        }

        public string Id => _sdkRegistration.Id;
        public ToolCategory Category => ToolCategory.Shape;
        public string DisplayName => _sdkRegistration.DisplayName;
        public ToolSettingsBase? Settings => _adaptedSettings;

        /// <summary>
        /// Gets the shape builder for this tool.
        /// </summary>
        public IShapeBuilder? ShapeBuilder => _shapeBuilder;

        /// <summary>
        /// Gets whether this tool has a shape builder.
        /// </summary>
        public bool HasShapeBuilder => _shapeBuilder != null;

        /// <summary>
        /// Gets whether this tool uses a stroke painter.
        /// </summary>
        public bool HasPainter => false; // Plugin shape tools don't support painters yet

        /// <summary>
        /// Gets the effective shape renderer.
        /// </summary>
        public IShapeRenderer EffectiveRenderer => BrushStrokeShapeRenderer.Shared;

        /// <summary>
        /// Creates a new painter instance (not supported for plugin shapes).
        /// </summary>
        public IStrokePainter? CreatePainter() => null;

        //////////////////////////////////////////////////////////////////
        // IToolBehavior implementation
        //////////////////////////////////////////////////////////////////

        string IToolBehavior.ToolId => Id;

        public ToolInputPattern InputPattern => ToolInputPattern.TwoPoint;

        public bool HandlesRightClick => false;

        public bool SuppressRmbDropper => false; // Shape tools allow RMB dropper for quick color sampling

        public bool SupportsModifiers => true; // Shape tools support Shift/Ctrl

        public ToolOverlayStyle OverlayStyle => ToolOverlayStyle.ShapePreview;

        public bool OverlayVisibleWhileActive => true;

        public bool UsesPainter => false;

        public bool ModifiesPixels => true;
    }

    /// <summary>
    /// Adapts SDK IShapeBuilder to main project's IShapeBuilder.
    /// </summary>
    internal sealed class PluginShapeBuilderAdapter : IShapeBuilder
    {
        private readonly SdkIShapeBuilder _sdkBuilder;

        public PluginShapeBuilderAdapter(SdkIShapeBuilder sdkBuilder)
        {
            _sdkBuilder = sdkBuilder ?? throw new ArgumentNullException(nameof(sdkBuilder));
        }

        public string DisplayName => _sdkBuilder.DisplayName;

        public (int x0, int y0, int x1, int y1) ApplyModifiers(int startX, int startY, int endX, int endY, bool shift, bool ctrl)
            => _sdkBuilder.ApplyModifiers(startX, startY, endX, endY, shift, ctrl);

        public HashSet<(int x, int y)> BuildOutlinePoints(int x0, int y0, int x1, int y1)
            => _sdkBuilder.BuildOutlinePoints(x0, y0, x1, y1);

        public HashSet<(int x, int y)> BuildFilledPoints(int x0, int y0, int x1, int y1)
            => _sdkBuilder.BuildFilledPoints(x0, y0, x1, y1);
    }

    /// <summary>
    /// Adapts SDK SelectionToolRegistration to main project's ISelectionToolRegistration.
    /// </summary>
    internal sealed class PluginSelectionToolRegistration : ISelectionToolRegistration, IToolBehavior
    {
        private readonly SdkSelectionToolRegistration _sdkRegistration;
        private readonly PluginToolSettings? _adaptedSettings;

        public PluginSelectionToolRegistration(SdkSelectionToolRegistration sdkRegistration)
        {
            _sdkRegistration = sdkRegistration ?? throw new ArgumentNullException(nameof(sdkRegistration));

            if (sdkRegistration.Settings != null)
            {
                _adaptedSettings = new PluginToolSettings(sdkRegistration.Settings);
            }
        }

        public string Id => _sdkRegistration.Id;
        public ToolCategory Category => ToolCategory.Select;
        public string DisplayName => _sdkRegistration.DisplayName;
        public ToolSettingsBase? Settings => _adaptedSettings;

        /// <summary>
        /// Gets whether this tool has a selection tool implementation.
        /// </summary>
        public bool HasTool => _sdkRegistration.HasSelectionTool;

        /// <summary>
        /// Creates a new selection tool instance.
        /// </summary>
        /// <param name="context">Context providing dependencies for the tool.</param>
        /// <returns>An adapted selection tool that wraps the SDK selection tool.</returns>
        public ISelectionTool? CreateTool(SelectionToolContext context)
        {
            var sdkTool = _sdkRegistration.CreateSelectionTool();
            if (sdkTool == null) return null;
            return new PluginSelectionToolAdapter(sdkTool, context);
        }

        //////////////////////////////////////////////////////////////////
        // IToolBehavior implementation
        //////////////////////////////////////////////////////////////////

        string IToolBehavior.ToolId => Id;

        public ToolInputPattern InputPattern => ToolInputPattern.Custom;

        public bool HandlesRightClick => false;

        public bool SuppressRmbDropper => false; // Selection tools allow RMB dropper

        public bool SupportsModifiers => true; // Selection tools support Shift/Alt

        public ToolOverlayStyle OverlayStyle => ToolOverlayStyle.None;

        public bool OverlayVisibleWhileActive => false;

        public bool UsesPainter => false;

        public bool ModifiesPixels => false; // Selection modifies region, not pixels
    }

    /// <summary>
    /// Adapts SDK ISelectionTool to main project's ISelectionTool.
    /// </summary>
    internal sealed class PluginSelectionToolAdapter : ISelectionTool
    {
        private enum CombineMode { Replace, Add, Subtract }

        private readonly SdkISelectionTool _sdkTool;
        private readonly SelectionToolContext _context;
        private CombineMode _combineMode = CombineMode.Replace;

        public PluginSelectionToolAdapter(SdkISelectionTool sdkTool, SelectionToolContext context)
        {
            _sdkTool = sdkTool ?? throw new ArgumentNullException(nameof(sdkTool));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Id => _sdkTool.Id;
        public bool HasPreview => _sdkTool.HasPreview;
        public bool IsActive => _sdkTool.IsActive;
        public bool NeedsContinuousRender => _sdkTool.NeedsContinuousRender;

        public bool PointerPressed(Point docPos, PointerRoutedEventArgs e)
        {
            var (shift, ctrl, alt) = GetModifiers();

            // Determine combine mode based on modifiers (like built-in tools)
            if (shift && !alt)
                _combineMode = CombineMode.Add;
            else if (alt && !shift)
                _combineMode = CombineMode.Subtract;
            else
                _combineMode = CombineMode.Replace;

            return _sdkTool.PointerPressed(docPos.X, docPos.Y, shift, ctrl, alt);
        }

        public bool PointerMoved(Point docPos, PointerRoutedEventArgs e)
        {
            var (shift, ctrl, alt) = GetModifiers();
            var result = _sdkTool.PointerMoved(docPos.X, docPos.Y, shift, ctrl, alt);

            // Request redraw to update preview
            if (result)
                _context.RequestRedraw();

            return result;
        }

        public bool PointerReleased(Point docPos, PointerRoutedEventArgs e)
        {
            var (shift, ctrl, alt) = GetModifiers();

            // Apply the selection from preview points BEFORE calling SDK tool's release
            // so we can use the preview points
            ApplySelectionFromPreviewPoints();

            // Now call the SDK tool's release, which may clear its state
            var result = _sdkTool.PointerReleased(docPos.X, docPos.Y, shift, ctrl, alt);

            // Ensure the SDK tool's preview is cleared after applying the selection
            // This prevents the preview overlay from remaining visible
            _sdkTool.Cancel();

            return result;
        }

        /// <summary>
        /// Applies the selection from the SDK tool's preview points to the selection region.
        /// </summary>
        private void ApplySelectionFromPreviewPoints()
        {
            var points = _sdkTool.GetPreviewPoints();
            if (points == null || points.Count < 3) return; // Need at least 3 points for a polygon

            var (docW, docH) = _context.GetDocumentSize();
            var region = _context.GetSelectionRegion();
            region.EnsureSize(docW, docH);

            // Build the selection region from the polygon points
            // For a simple rectangular selection, we can compute the bounding rect
            // For complex polygons, we'd need to rasterize the polygon

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var (x, y) in points)
            {
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            // Clamp to document bounds
            int x0 = Math.Clamp((int)Math.Floor(minX), 0, docW);
            int y0 = Math.Clamp((int)Math.Floor(minY), 0, docH);
            int x1 = Math.Clamp((int)Math.Ceiling(maxX), 0, docW);
            int y1 = Math.Clamp((int)Math.Ceiling(maxY), 0, docH);

            int width = x1 - x0;
            int height = y1 - y0;

            if (width <= 0 || height <= 0) return;

            var selRect = new Windows.Graphics.RectInt32(x0, y0, width, height);

            // Check if this is a simple rectangle (4 points forming axis-aligned rect)
            bool isSimpleRect = points.Count == 4 && IsAxisAlignedRectangle(points);

            switch (_combineMode)
            {
                case CombineMode.Replace:
                    region.Clear();
                    if (isSimpleRect)
                    {
                        region.AddRect(selRect);
                    }
                    else
                    {
                        // Rasterize polygon into region
                        RasterizePolygonToRegion(region, points, docW, docH);
                    }
                    break;

                case CombineMode.Add:
                    if (isSimpleRect)
                    {
                        region.AddRect(selRect);
                    }
                    else
                    {
                        RasterizePolygonToRegion(region, points, docW, docH);
                    }
                    break;

                case CombineMode.Subtract:
                    if (isSimpleRect)
                    {
                        region.SubtractRect(selRect);
                    }
                    else
                    {
                        // For subtract, we need to create a temp region and subtract it
                        var tempRegion = new Core.Selection.SelectionRegion();
                        tempRegion.EnsureSize(docW, docH);
                        RasterizePolygonToRegion(tempRegion, points, docW, docH);
                        region.SubtractRegion(tempRegion);
                    }
                    break;
            }

            _context.RequestRedraw();
        }

        /// <summary>
        /// Checks if the points form an axis-aligned rectangle.
        /// </summary>
        private static bool IsAxisAlignedRectangle(IReadOnlyList<(double x, double y)> points)
        {
            if (points.Count != 4) return false;

            // Get unique X and Y values
            var xs = new HashSet<double>();
            var ys = new HashSet<double>();

            foreach (var (x, y) in points)
            {
                xs.Add(Math.Round(x));
                ys.Add(Math.Round(y));
            }

            // Axis-aligned rectangle has exactly 2 unique X and 2 unique Y values
            return xs.Count == 2 && ys.Count == 2;
        }

        /// <summary>
        /// Rasterizes a polygon into the selection region using scanline fill.
        /// </summary>
        private static void RasterizePolygonToRegion(
            Core.Selection.SelectionRegion region,
            IReadOnlyList<(double x, double y)> points,
            int docW, int docH)
        {
            if (points.Count < 3) return;

            // Find bounding box
            double minY = double.MaxValue, maxY = double.MinValue;
            foreach (var (_, y) in points)
            {
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }

            int yStart = Math.Max(0, (int)Math.Floor(minY));
            int yEnd = Math.Min(docH - 1, (int)Math.Ceiling(maxY));

            // Scanline fill
            for (int y = yStart; y <= yEnd; y++)
            {
                var intersections = new List<double>();

                // Find all intersections with polygon edges
                for (int i = 0; i < points.Count; i++)
                {
                    var p1 = points[i];
                    var p2 = points[(i + 1) % points.Count];

                    // Check if scanline intersects this edge
                    if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                    {
                        // Calculate X intersection
                        double t = (y - p1.y) / (p2.y - p1.y);
                        double x = p1.x + t * (p2.x - p1.x);
                        intersections.Add(x);
                    }
                }

                // Sort intersections
                intersections.Sort();

                // Fill between pairs of intersections
                for (int i = 0; i < intersections.Count - 1; i += 2)
                {
                    int x0 = Math.Max(0, (int)Math.Ceiling(intersections[i]));
                    int x1 = Math.Min(docW - 1, (int)Math.Floor(intersections[i + 1]));

                    if (x1 >= x0)
                    {
                        region.AddRect(new Windows.Graphics.RectInt32(x0, y, x1 - x0 + 1, 1));
                    }
                }
            }
        }

        public void Deactivate() => _sdkTool.Deactivate();
        public void Cancel() => _sdkTool.Cancel();
        public void Configure(ToolSettingsBase settings) { }

        public void DrawPreview(CanvasDrawingSession ds, Rect destRect, double scale, float antsPhase)
        {
            var points = _sdkTool.GetPreviewPoints();
            if (points == null || points.Count < 2) return;

            using var pathBuilder = new Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder(ds);
            var firstPoint = points[0];
            pathBuilder.BeginFigure((float)(destRect.X + firstPoint.x * scale), (float)(destRect.Y + firstPoint.y * scale));

            for (int i = 1; i < points.Count; i++)
            {
                var pt = points[i];
                pathBuilder.AddLine((float)(destRect.X + pt.x * scale), (float)(destRect.Y + pt.y * scale));
            }

            pathBuilder.EndFigure(Microsoft.Graphics.Canvas.Geometry.CanvasFigureLoop.Closed);
            using var geometry = Microsoft.Graphics.Canvas.Geometry.CanvasGeometry.CreatePath(pathBuilder);

            var strokeStyle = new Microsoft.Graphics.Canvas.Geometry.CanvasStrokeStyle
            {
                DashStyle = Microsoft.Graphics.Canvas.Geometry.CanvasDashStyle.Dash,
                DashOffset = -antsPhase,
                CustomDashStyle = new float[] { 4f, 4f }
            };

            ds.DrawGeometry(geometry, Windows.UI.Color.FromArgb(255, 255, 255, 255), 2f);
            ds.DrawGeometry(geometry, Windows.UI.Color.FromArgb(255, 0, 0, 0), 1f, strokeStyle);
        }

        private static (bool shift, bool ctrl, bool alt) GetModifiers()
        {
            var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
            var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            var altState = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            return (
                (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0,
                (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0,
                (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0
            );
        }
    }

    /// <summary>
    /// Adapts SDK UtilityToolRegistration to main project's IUtilityToolRegistration.
    /// </summary>
    internal sealed class PluginUtilityToolRegistration : IUtilityToolRegistration, IToolBehavior
    {
        private readonly SdkUtilityToolRegistration _sdkRegistration;
        private readonly PluginToolSettings? _adaptedSettings;

        public PluginUtilityToolRegistration(SdkUtilityToolRegistration sdkRegistration)
        {
            _sdkRegistration = sdkRegistration ?? throw new ArgumentNullException(nameof(sdkRegistration));

            if (sdkRegistration.Settings != null)
            {
                _adaptedSettings = new PluginToolSettings(sdkRegistration.Settings);
            }
        }

        public string Id => _sdkRegistration.Id;
        public ToolCategory Category => ToolCategory.Utility;
        public string DisplayName => _sdkRegistration.DisplayName;
        public ToolSettingsBase? Settings => _adaptedSettings;

        /// <summary>
        /// Gets whether this tool has a utility handler implementation.
        /// </summary>
        public bool HasHandler => _sdkRegistration.HasUtilityHandler;

        /// <summary>
        /// Creates a new handler instance for this tool.
        /// </summary>
        /// <param name="context">The utility context for canvas operations.</param>
        /// <returns>An adapted handler that wraps the SDK handler.</returns>
        public IUtilityHandler? CreateHandler(IUtilityContext context)
        {
            // Create SDK context adapter
            var sdkContext = new PluginUtilityContextAdapter(context);
            var sdkHandler = _sdkRegistration.CreateUtilityHandler(sdkContext);
            if (sdkHandler == null) return null;
            return new PluginUtilityHandlerAdapter(sdkHandler);
        }

        //////////////////////////////////////////////////////////////////
        // IToolBehavior implementation
        //////////////////////////////////////////////////////////////////

        string IToolBehavior.ToolId => Id;

        public ToolInputPattern InputPattern => ToolInputPattern.Custom;

        public bool HandlesRightClick => false;

        public bool SuppressRmbDropper => true; // Utility tools suppress RMB dropper (they handle their own input or don't need it)

        public bool SupportsModifiers => false;

        public ToolOverlayStyle OverlayStyle => ToolOverlayStyle.None;

        public bool OverlayVisibleWhileActive => false;

        public bool UsesPainter => false;

        public bool ModifiesPixels => false;
    }

    /// <summary>
    /// Adapts SDK IUtilityHandler to main project's IUtilityHandler.
    /// </summary>
    internal sealed class PluginUtilityHandlerAdapter : IUtilityHandler
    {
        private readonly SdkIUtilityHandler _sdkHandler;

        public PluginUtilityHandlerAdapter(SdkIUtilityHandler sdkHandler)
        {
            _sdkHandler = sdkHandler ?? throw new ArgumentNullException(nameof(sdkHandler));
        }

        public string ToolId => _sdkHandler.ToolId;
        public bool IsActive => _sdkHandler.IsActive;

        public UtilityCursorHint CursorHint => _sdkHandler.CursorHint switch
        {
            SdkUtilityCursorHint.Hand => UtilityCursorHint.Hand,
            SdkUtilityCursorHint.Grabbing => UtilityCursorHint.Grabbing,
            SdkUtilityCursorHint.ZoomIn => UtilityCursorHint.ZoomIn,
            SdkUtilityCursorHint.ZoomOut => UtilityCursorHint.ZoomOut,
            SdkUtilityCursorHint.Eyedropper => UtilityCursorHint.Eyedropper,
            SdkUtilityCursorHint.Crosshair => UtilityCursorHint.Crosshair,
            _ => UtilityCursorHint.Default
        };

        public bool PointerPressed(Point screenPos, Point docPos, PointerPointProperties props)
        {
            return _sdkHandler.PointerPressed(
                screenPos.X, screenPos.Y,
                docPos.X, docPos.Y,
                props.IsLeftButtonPressed,
                props.IsRightButtonPressed);
        }

        public bool PointerMoved(Point screenPos, Point docPos, PointerPointProperties props)
        {
            return _sdkHandler.PointerMoved(
                screenPos.X, screenPos.Y,
                docPos.X, docPos.Y,
                props.IsLeftButtonPressed,
                props.IsRightButtonPressed);
        }

        public bool PointerReleased(Point screenPos, Point docPos, PointerPointProperties props)
        {
            return _sdkHandler.PointerReleased(
                screenPos.X, screenPos.Y,
                docPos.X, docPos.Y,
                props.IsLeftButtonPressed,
                props.IsRightButtonPressed);
        }

        public bool PointerWheelChanged(Point screenPos, int delta)
        {
            return _sdkHandler.PointerWheelChanged(screenPos.X, screenPos.Y, delta);
        }

        public void Reset() => _sdkHandler.Reset();
    }

    /// <summary>
    /// Adapts main project's IUtilityContext to SDK's IUtilityContext.
    /// </summary>
    internal sealed class PluginUtilityContextAdapter : SdkIUtilityContext
    {
        private readonly IUtilityContext _mainContext;

        public PluginUtilityContextAdapter(IUtilityContext mainContext)
        {
            _mainContext = mainContext ?? throw new ArgumentNullException(nameof(mainContext));
        }

        // The main project's IUtilityContext has different property names
        // We need to adapt them to match the SDK interface

        public double Zoom
        {
            get => _mainContext.ZoomScale;
            set { /* Main context doesn't support direct zoom set - use ZoomAt instead */ }
        }

        public double MinZoom => 0.125; // Default min zoom
        public double MaxZoom => 64.0;  // Default max zoom

        public double ScrollX
        {
            get => 0; // Main context doesn't expose scroll position directly
            set { /* Use PanBy instead */ }
        }

        public double ScrollY
        {
            get => 0; // Main context doesn't expose scroll position directly
            set { /* Use PanBy instead */ }
        }

        public void Pan(double deltaX, double deltaY) => _mainContext.PanBy(deltaX, deltaY);

        public void ZoomAt(double factor, double centerX, double centerY)
            => _mainContext.ZoomAt(new Point(centerX, centerY), factor);

        public uint? SampleColor(int docX, int docY)
        {
            var (w, h) = _mainContext.DocumentSize;
            if (docX < 0 || docX >= w || docY < 0 || docY >= h)
                return null;
            return _mainContext.SampleColorAt(docX, docY);
        }

        public void SetForegroundColor(uint bgra) => _mainContext.SetForegroundColor(bgra);

        public void SetBackgroundColor(uint bgra) => _mainContext.SetBackgroundColor(bgra);

        public bool CopyToClipboard(string text)
        {
            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Invalidate() => _mainContext.RequestRedraw();
    }

    internal sealed class PluginTileToolRegistration : ITileToolRegistration, IToolBehavior /* + your ITileToolRegistration */
    {
        private readonly SdkTileToolRegistration _sdkRegistration;
        private readonly PluginToolSettings? _adaptedSettings;

        public PluginTileToolRegistration(SdkTileToolRegistration sdkRegistration)
        {
            _sdkRegistration = sdkRegistration ?? throw new ArgumentNullException(nameof(sdkRegistration));

            if (sdkRegistration.Settings != null)
                _adaptedSettings = new PluginToolSettings(sdkRegistration.Settings);
        }

        public string Id => _sdkRegistration.Id;
        public ToolCategory Category => ToolCategory.Tile;
        public string DisplayName => _sdkRegistration.DisplayName;
        public ToolSettingsBase? Settings => _adaptedSettings;

        public bool HasHandler => _sdkRegistration.HasTileHandler;

        /// <summary>
        /// Creates a new tile handler instance for this tool.
        /// </summary>
        /// <param name="context">The tile context for canvas operations.</param>
        /// <returns>An adapted handler that wraps the SDK handler.</returns>
        public ITileHandler? CreateHandler(ITileContext context)
        {
            // Create an adapter that forwards calls to the SDK handler
            var sdkCtx = new PluginTileContextAdapter(context);
            var sdkHandler = _sdkRegistration.CreateTileHandler(sdkCtx);
            if (sdkHandler == null) return null;

            Core.Logging.LoggingService.Debug("Created plugin tile handler for tool: {ToolId}", Id);
            return new PluginTileHandlerAdapter(sdkHandler);
        }

        //////////////////////////////////////////////////////////////////
        // IToolBehavior implementation
        //////////////////////////////////////////////////////////////////

        string IToolBehavior.ToolId => Id;
        public ToolInputPattern InputPattern => ToolInputPattern.Custom;
        public bool HandlesRightClick => true;        // tile tools own RMB sampling
        public bool SuppressRmbDropper => true;       // don't let host color-dropper steal RMB
        public bool SupportsModifiers => true;        // Shift/Ctrl are common for tile tools
        public ToolOverlayStyle OverlayStyle => ToolOverlayStyle.TileBoundary;
        public bool OverlayVisibleWhileActive => true;
        public bool UsesPainter => false;
        public bool ModifiesPixels => true;           // mapping + sometimes pixels
    }

    internal sealed class PluginTileContextAdapter : SdkITileContext
    {
        private readonly ITileContext _core; // your Core tile context

        public PluginTileContextAdapter(ITileContext core) => _core = core;

        public int DocumentWidth => _core.DocumentWidth;
        public int DocumentHeight => _core.DocumentHeight;

        public int TileWidth => _core.TileWidth;
        public int TileHeight => _core.TileHeight;

        public int TileCountX => _core.TileCountX;
        public int TileCountY => _core.TileCountY;

        public int SelectedTileId => _core.SelectedTileId;
        public int TileCount => _core.TileCount;

        public (int tileX, int tileY) DocToTile(int docX, int docY) => _core.DocToTile(docX, docY);
        public (int docX, int docY) TileToDoc(int tileX, int tileY) => _core.TileToDoc(tileX, tileY);
        public (int x, int y, int width, int height) GetTileRect(int tileX, int tileY) => _core.GetTileRect(tileX, tileY);

        public IEnumerable<int> GetAllTileIds() => _core.GetAllTileIds();
        public int GetTileIdAtIndex(int index) => _core.GetTileIdAtIndex(index);

        public byte[]? GetTilePixels(int tileId) => _core.GetTilePixels(tileId);
        public int CreateTile(byte[] pixels) => _core.CreateTile(pixels);
        public bool DeleteTile(int tileId) => _core.DeleteTile(tileId);
        public int DuplicateTile(int tileId) => _core.DuplicateTile(tileId);
        public bool UpdateTilePixels(int tileId, byte[] pixels) => _core.UpdateTilePixels(tileId, pixels);

        public int GetMappedTileId(int tileX, int tileY) => _core.GetMappedTileId(tileX, tileY);
        public void SetTileMapping(int tileX, int tileY, int tileId) => _core.SetTileMapping(tileX, tileY, tileId);
        public void ClearTileMapping(int tileX, int tileY) => _core.ClearTileMapping(tileX, tileY);

        public byte[] GetActiveLayerPixels() => _core.GetActiveLayerPixels();
        public byte[] ReadLayerRect(int x, int y, int width, int height) => _core.ReadLayerRect(x, y, width, height);
        public void WriteLayerRect(int x, int y, int width, int height, byte[] pixels) => _core.WriteLayerRect(x, y, width, height, pixels);
        public void BlendLayerRect(int x, int y, int width, int height, byte[] pixels) => _core.BlendLayerRect(x, y, width, height, pixels);
        public void BlendAndPropagateTiles(int x, int y, int width, int height, byte[] pixels) => _core.BlendAndPropagateTiles(x, y, width, height, pixels);
        public void ClearLayerRect(int x, int y, int width, int height) => _core.ClearLayerRect(x, y, width, height);

        public (int tileId, int tileX, int tileY) SampleTileAt(int docX, int docY) => _core.SampleTileAt(docX, docY);
        public void SetSelectedTile(int tileId) => _core.SetSelectedTile(tileId);

        public void BeginHistoryTransaction(string description) => _core.BeginHistoryTransaction(description);
        public void CommitHistoryTransaction() => _core.CommitHistoryTransaction();
        public void CancelHistoryTransaction() => _core.CancelHistoryTransaction();

        public void Invalidate() => _core.Invalidate();
        public void CapturePointer() => _core.CapturePointer();
        public void ReleasePointer() => _core.ReleasePointer();
    }

    internal sealed class PluginTileHandlerAdapter : ITileHandler // your Core tile handler type
    {

        private readonly SdkITileHandler _sdkHandler;

        public void Reset() => _sdkHandler.Reset();
        public PluginTileHandlerAdapter(SdkITileHandler sdkHandler) => _sdkHandler = sdkHandler;

        public string ToolId => _sdkHandler.ToolId;
        public bool IsActive => _sdkHandler.IsActive;

        public TileCursorHint CursorHint => _sdkHandler.CursorHint switch
        {
            SdkTileCursorHint.Stamp => TileCursorHint.Stamp,
            SdkTileCursorHint.Create => TileCursorHint.Create,
            _ => TileCursorHint.Default
        };

        public bool PointerPressed(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            Core.Logging.LoggingService.Debug("PluginTileHandlerAdapter.PointerPressed: Forwarding to SDK handler {ToolId}", _sdkHandler.ToolId);
            var result = _sdkHandler.PointerPressed(
                screenX, screenY,
                docX, docY,
                isLeftButton,
                isRightButton,
                isShiftHeld, isCtrlHeld);
            Core.Logging.LoggingService.Debug("PluginTileHandlerAdapter.PointerPressed: SDK handler returned {Result}", result);
            return result;
        }

        public bool PointerMoved(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            return _sdkHandler.PointerMoved(
                screenX, screenY,
                docX, docY,
                isLeftButton,
                isRightButton,
                isShiftHeld, isCtrlHeld);
        }

        public bool PointerReleased(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            return _sdkHandler.PointerReleased(
                screenX, screenY,
                docX, docY,
                isLeftButton,
                isRightButton,
                isShiftHeld, isCtrlHeld);
        }
        public TileOverlayPreview? GetOverlayPreview() => _sdkHandler.GetOverlayPreview(); // or adapt if Core differs
    }


    /// <summary>
    /// Registers import/export handlers from plugins into the Core registries.
    /// </summary>
    internal static class PluginIOAdapter
    {
        /// <summary>
        /// Registers all import handlers from a plugin.
        /// </summary>
        public static void RegisterImportHandlers(IEnumerable<SdkIImportRegistration> handlers)
        {
            foreach (var handler in handlers)
            {
                // SDK types are already compatible via global usings - no adapter needed!
                ImportRegistry.Instance.Register(handler);
            }
        }

        /// <summary>
        /// Registers all export handlers from a plugin.
        /// </summary>
        public static void RegisterExportHandlers(IEnumerable<SdkIExportRegistration> handlers)
        {
            foreach (var handler in handlers)
            {
                // SDK types are already compatible via global usings - no adapter needed!
                ExportRegistry.Instance.Register(handler);
            }
        }

        /// <summary>
        /// Unregisters all import/export handlers from a plugin.
        /// </summary>
        public static void UnregisterPluginHandlers(string pluginId)
        {
            // Handlers have IDs like "pluginId.import.format" or "pluginId.export.format"
            var importHandlers = ImportRegistry.Instance.GetAllHandlers();
            foreach (var handler in importHandlers)
            {
                if (handler.Id.StartsWith(pluginId + ".", StringComparison.Ordinal))
                {
                    ImportRegistry.Instance.Unregister(handler.Id);
                }
            }

            var exportHandlers = ExportRegistry.Instance.GetAllHandlers();
            foreach (var handler in exportHandlers)
            {
                if (handler.Id.StartsWith(pluginId + ".", StringComparison.Ordinal))
                {
                    ExportRegistry.Instance.Unregister(handler.Id);
                }
            }
        }
    }
}
