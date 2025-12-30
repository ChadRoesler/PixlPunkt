using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.Core.Plugins;
using PixlPunkt.Core.Symmetry;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Centralized state management for all drawing and editing tools in the application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ToolState maintains the currently active tool, tool-specific settings (brush, shape, fill, etc.),
    /// and provides events for UI synchronization when settings change. It supports temporary tool
    /// overrides (e.g., holding spacebar for Pan tool) and manages state for complex tools like
    /// the gradient brush and selection transformations.
    /// </para>
    /// <para>
    /// This class serves as the single source of truth for tool configuration, allowing UI panels
    /// and canvas hosts to remain synchronized without direct coupling.
    /// </para>
    /// </remarks>
    public sealed class ToolState
    {
        // ════════════════════════════════════════════════════════════════════
        // TOOL IDENTITY (STRING-BASED)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the base tool ID selected by the user (ignoring temporary overrides).
        /// </summary>
        /// <remarks>
        /// Tool IDs follow the convention <c>vendor.category.name</c> (e.g., "pixlpunkt.brush.brush").
        /// </remarks>
        public string CurrentToolId { get; private set; } = ToolIds.Brush;

        /// <summary>
        /// Gets the temporary tool override ID, if any (e.g., spacebar-held Pan tool).
        /// </summary>
        public string? OverrideToolId { get; private set; }

        /// <summary>
        /// Gets the active tool ID, considering temporary overrides.
        /// </summary>
        /// <remarks>
        /// This is the preferred property for determining which tool is currently in use.
        /// </remarks>
        public string ActiveToolId => OverrideToolId ?? CurrentToolId;

        // ════════════════════════════════════════════════════════════════════
        // UNIFIED TOOL REGISTRY
        // ════════════════════════════════════════════════════════════════════

        private readonly IToolRegistry _registry;

        /// <summary>
        /// Gets the tool registry used by this ToolState.
        /// </summary>
        public IToolRegistry Registry => _registry;

        /// <summary>
        /// Gets a tool registration by string ID.
        /// </summary>
        /// <param name="toolId">The tool ID to look up.</param>
        /// <returns>The registration, or null if not found.</returns>
        public IToolRegistration? GetRegistration(string toolId)
            => _registry.GetById(toolId);

        /// <summary>
        /// Gets the registration for the currently active tool.
        /// </summary>
        public IToolRegistration? ActiveRegistration => GetRegistration(ActiveToolId);

        /// <summary>
        /// Gets all registered tool IDs.
        /// </summary>
        public IEnumerable<string> RegisteredToolIds => _registry.RegisteredIds;

        /// <summary>
        /// Gets all tool registrations.
        /// </summary>
        public IEnumerable<IToolRegistration> AllRegistrations => _registry.GetAll();

        // ════════════════════════════════════════════════════════════════════
        // CATEGORY HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the category for a tool ID.
        /// </summary>
        /// <param name="toolId">The tool ID to categorize.</param>
        /// <returns>The tool category, or Utility if not found.</returns>
        public ToolCategory GetCategory(string toolId)
            => GetRegistration(toolId)?.Category ?? ToolCategory.Utility;

        /// <summary>
        /// Checks if a tool belongs to a specific category.
        /// </summary>
        /// <param name="toolId">The tool ID to check.</param>
        /// <param name="category">The category to match.</param>
        /// <returns>True if the tool belongs to the category.</returns>
        public bool IsCategory(string toolId, ToolCategory category)
            => GetCategory(toolId) == category;

        /// <summary>
        /// Gets the category for the currently active tool.
        /// </summary>
        public ToolCategory ActiveCategory => GetCategory(ActiveToolId);

        /// <summary>
        /// Checks if the active tool is a selection tool.
        /// </summary>
        public bool IsActiveSelectTool => IsCategory(ActiveToolId, ToolCategory.Select);

        /// <summary>
        /// Checks if the active tool is a brush tool.
        /// </summary>
        public bool IsActiveBrushTool => IsCategory(ActiveToolId, ToolCategory.Brush);

        /// <summary>
        /// Checks if the active tool is a shape tool.
        /// </summary>
        public bool IsActiveShapeTool => IsCategory(ActiveToolId, ToolCategory.Shape);

        /// <summary>
        /// Checks if the active tool is a utility tool.
        /// </summary>
        public bool IsActiveUtilityTool => IsCategory(ActiveToolId, ToolCategory.Utility);

        /// <summary>
        /// Checks if the active tool is a tile tool.
        /// </summary>
        public bool IsActiveTileTool => IsCategory(ActiveToolId, ToolCategory.Tile);

        // ════════════════════════════════════════════════════════════════════
        // TYPED REGISTRATION ACCESS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets a selection tool registration by string ID.
        /// </summary>
        /// <param name="toolId">The tool ID to look up.</param>
        /// <returns>The selection tool registration, or null if not found or wrong type.</returns>
        public ISelectionToolRegistration? GetSelectionRegistrationById(string toolId)
            => _registry.GetById<ISelectionToolRegistration>(toolId);

        /// <summary>
        /// Gets a brush tool registration by string ID.
        /// </summary>
        /// <param name="toolId">The tool ID to look up.</param>
        /// <returns>The brush tool registration, or null if not found or wrong type.</returns>
        public BrushToolRegistration? GetBrushRegistration(string toolId)
            => _registry.GetById<BrushToolRegistration>(toolId);

        /// <summary>
        /// Gets a shape tool registration by string ID.
        /// </summary>
        /// <param name="toolId">The tool ID to look up.</param>
        /// <returns>The shape tool registration, or null if not found or wrong type.</returns>
        public IShapeToolRegistration? GetShapeRegistration(string toolId)
            => _registry.GetById<IShapeToolRegistration>(toolId);

        /// <summary>
        /// Gets a utility tool registration by string ID.
        /// </summary>
        /// <param name="toolId">The tool ID to look up.</param>
        /// <returns>The utility tool registration, or null if not found or wrong type.</returns>
        public IUtilityToolRegistration? GetUtilityRegistration(string toolId)
            => _registry.GetById<IUtilityToolRegistration>(toolId);

        // ════════════════════════════════════════════════════════════════════
        // TOOL SWITCHING
        // ════════════════════════════════════════════════════════════════════

        private string _previousToolId = ToolIds.Brush;

        /// <summary>
        /// Sets the current tool by string ID.
        /// </summary>
        /// <param name="toolId">The tool ID to activate.</param>
        /// <exception cref="ArgumentException">Thrown if the tool ID is not registered.</exception>
        public void SetById(String toolId)
        {
            if (toolId == CurrentToolId) return;

            if (!_registry.IsRegistered(toolId))
            {
                LoggingService.Warning("Attempted to set unknown tool toolId={ToolId}", toolId);
                throw new ArgumentException($"Unknown tool: {toolId}", nameof(toolId));
            }

            // Notify deactivation of previous tool
            string previousId = CurrentToolId;
            NotifyToolDeactivated(previousId);

            _previousToolId = previousId;
            CurrentToolId = toolId;

            // Notify activation of new tool
            NotifyToolActivated(toolId);

            LoggingService.Debug("Tool switched from={PreviousId} to={CurrentId} category={Category}",
                previousId, toolId, GetCategory(toolId));

            ToolIdChanged?.Invoke(CurrentToolId);
            ActiveToolIdChanged?.Invoke(ActiveToolId);
        }

        /// <summary>
        /// Begins a temporary tool override by string ID.
        /// </summary>
        /// <param name="toolId">The tool ID to temporarily activate.</param>
        public void BeginOverrideById(string toolId)
        {
            if (OverrideToolId == toolId) return;

            // Notify deactivation of current active tool
            string previousActive = ActiveToolId;

            OverrideToolId = toolId;

            NotifyToolDeactivated(previousActive);
            NotifyToolActivated(toolId);

            LoggingService.Debug("Tool override started overrideId={OverrideId} baseId={BaseId}",
                toolId, CurrentToolId);

            ActiveToolIdChanged?.Invoke(ActiveToolId);
        }

        /// <summary>
        /// Ends the temporary tool override.
        /// </summary>
        public void EndOverrideById()
        {
            if (OverrideToolId == null) return;

            string overrideId = OverrideToolId;
            OverrideToolId = null;

            NotifyToolDeactivated(overrideId);
            NotifyToolActivated(CurrentToolId);

            LoggingService.Debug("Tool override ended overrideId={OverrideId} restoredId={RestoredId}",
                overrideId, CurrentToolId);

            ActiveToolIdChanged?.Invoke(ActiveToolId);
        }

        /// <summary>
        /// Ends the temporary tool override (alias for EndOverrideById).
        /// </summary>
        public void EndOverride() => EndOverrideById();

        /// <summary>
        /// Gets the previously active tool ID (before the last tool switch).
        /// </summary>
        public string PreviousToolId => _previousToolId;

        /// <summary>
        /// Switches back to the previously active tool.
        /// </summary>
        public void SwitchToPrevious()
        {
            if (_previousToolId != CurrentToolId && _registry.IsRegistered(_previousToolId))
            {
                SetById(_previousToolId);
            }
        }

        /// <summary>
        /// Notifies a tool's lifecycle handler that it has been activated.
        /// </summary>
        private void NotifyToolActivated(string toolId)
        {
            var settings = GetSettingsForToolId(toolId);
            if (settings is IToolLifecycle lifecycle)
            {
                lifecycle.OnActivate();
            }

            ToolActivated?.Invoke(toolId);
        }

        /// <summary>
        /// Notifies a tool's lifecycle handler that it has been deactivated.
        /// </summary>
        private void NotifyToolDeactivated(string toolId)
        {
            var settings = GetSettingsForToolId(toolId);
            if (settings is IToolLifecycle lifecycle)
            {
                lifecycle.OnDeactivate();
            }

            ToolDeactivated?.Invoke(toolId);
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Occurs when the base tool changes.
        /// </summary>
        public event Action<string>? ToolIdChanged;

        /// <summary>
        /// Occurs when the active tool changes including overrides.
        /// </summary>
        public event Action<string>? ActiveToolIdChanged;

        /// <summary>
        /// Occurs when a tool is activated (becomes the active tool).
        /// </summary>
        /// <remarks>
        /// Fired after the tool's <see cref="IToolLifecycle.OnActivate"/> method is called (if implemented).
        /// Subscribers can use this to update UI state or perform tool-specific setup.
        /// </remarks>
        public event Action<string>? ToolActivated;

        /// <summary>
        /// Occurs when a tool is deactivated (no longer the active tool).
        /// </summary>
        /// <remarks>
        /// Fired after the tool's <see cref="IToolLifecycle.OnDeactivate"/> method is called (if implemented).
        /// Subscribers can use this to clean up UI state or perform tool-specific teardown.
        /// </remarks>
        public event Action<string>? ToolDeactivated;

        /// <summary>
        /// Occurs when brush settings change.
        /// </summary>
        public event Action<BrushSettings>? BrushChanged;

        /// <summary>
        /// Occurs when any tool option changes (fill tolerance, jumble strength, etc.).
        /// </summary>
        public event Action? OptionsChanged;

        /// <summary>
        /// Occurs when selection commit is requested.
        /// </summary>
        public event Action? SelectionCommitRequested;

        /// <summary>
        /// Occurs when selection cancel is requested.
        /// </summary>
        public event Action? SelectionCancelRequested;

        /// <summary>
        /// Occurs when selection flip horizontal is requested. Parameter is whether to use global axis.
        /// </summary>
        public event Action<bool>? SelectionFlipHorizontalRequested;

        /// <summary>
        /// Occurs when selection flip vertical is requested. Parameter is whether to use global axis.
        /// </summary>
        public event Action<bool>? SelectionFlipVerticalRequested;

        // ════════════════════════════════════════════════════════════════════
        // SELECTION TOOL HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets all registered selection tool registrations.
        /// </summary>
        public IEnumerable<SelectionToolRegistration> GetAllSelectionRegistrations()
            => _registry.GetSelectionTools();

        /// <summary>
        /// Gets whether the specified tool ID is a registered selection tool.
        /// </summary>
        public bool IsSelectionToolById(string toolId)
            => GetSelectionRegistrationById(toolId) != null;

        // ════════════════════════════════════════════════════════════════════
        // TOOL SETTINGS OBJECTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the brush settings (size, opacity, density, shape).
        /// </summary>
        public BrushSettings Brush { get; } = new BrushSettings();

        /// <summary>
        /// Gets the brush tool settings.
        /// </summary>
        public BrushToolSettings BrushTool { get; } = new BrushToolSettings();

        /// <summary>
        /// Gets the eraser tool settings.
        /// </summary>
        public EraserToolSettings Eraser { get; } = new EraserToolSettings();

        /// <summary>
        /// Gets the fill tool settings (tolerance, contiguous).
        /// </summary>
        public FillToolSettings Fill { get; } = new FillToolSettings();

        /// <summary>
        /// Gets the replacer tool settings (ignoreAlpha).
        /// </summary>
        public ReplacerToolSettings Replacer { get; } = new ReplacerToolSettings();

        /// <summary>
        /// Gets the rectangle tool settings (filled, constrain).
        /// </summary>
        public RectToolSettings Rect { get; } = new RectToolSettings();

        /// <summary>
        /// Gets the ellipse tool settings (filled, constrain).
        /// </summary>
        public EllipseToolSettings Ellipse { get; } = new EllipseToolSettings();

        /// <summary>
        /// Gets the jumble tool settings (strength, gamma, locality, etc.).
        /// </summary>
        public JumbleToolSettings Jumble { get; } = new JumbleToolSettings();

        /// <summary>
        /// Gets the smudge tool settings (strength, falloff, etc.).
        /// </summary>
        public SmudgeToolSettings Smudge { get; } = new SmudgeToolSettings();

        /// <summary>
        /// Gets the blur tool settings (size, shape, strength).
        /// </summary>
        public BlurToolSettings Blur { get; } = new BlurToolSettings();

        /// <summary>
        /// Gets the gradient tool settings (colors, loop, ignoreAlpha).
        /// </summary>
        public GradientToolSettings Gradient { get; } = new GradientToolSettings();

        /// <summary>
        /// Gets the gradient fill tool settings (type, dither, colors).
        /// </summary>
        public GradientFillToolSettings GradientFill { get; } = new GradientFillToolSettings();

        /// <summary>
        /// Gets the selection tool settings (scale, filter, rotation).
        /// </summary>
        public SelectionToolSettings Selection { get; }

        /// <summary>
        /// Gets the rectangle selection tool settings.
        /// </summary>
        public SelectRectToolSettings SelectRect { get; }

        /// <summary>
        /// Gets the wand tool settings (tolerance, contiguous).
        /// </summary>
        public WandToolSettings Wand { get; }

        /// <summary>
        /// Gets the lasso tool settings (autoClose, closeDistance).
        /// </summary>
        public LassoToolSettings Lasso { get; }

        /// <summary>
        /// Gets the paint selection tool settings (size, shape).
        /// </summary>
        public PaintSelectToolSettings PaintSelect { get; }

        // ════════════════════════════════════════════════════════════════════
        // UTILITY TOOL SETTINGS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the pan tool settings.
        /// </summary>
        public PanToolSettings Pan { get; } = new PanToolSettings();

        /// <summary>
        /// Gets the zoom tool settings.
        /// </summary>
        public ZoomToolSettings Zoom { get; } = new ZoomToolSettings();

        /// <summary>
        /// Gets the dropper tool settings.
        /// </summary>
        public DropperToolSettings Dropper { get; } = new DropperToolSettings();

        // ════════════════════════════════════════════════════════════════════
        // TILE TOOL SETTINGS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the tile stamper tool settings.
        /// </summary>
        public TileStamperToolSettings TileStamper { get; } = new TileStamperToolSettings();

        /// <summary>
        /// Gets the tile modifier tool settings.
        /// </summary>
        public TileModifierToolSettings TileModifier { get; } = new TileModifierToolSettings();

        /// <summary>
        /// Gets the tile animation tool settings.
        /// </summary>
        public TileAnimationToolSettings TileAnimation { get; } = new TileAnimationToolSettings();

        // ════════════════════════════════════════════════════════════════════
        // SYMMETRY SETTINGS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the shared symmetry settings that control live stroke mirroring.
        /// </summary>
        public SymmetrySettings Symmetry { get; } = new SymmetrySettings();

        /// <summary>
        /// Gets the symmetry tool settings.
        /// </summary>
        public SymmetryToolSettings SymmetryTool { get; private set; } = null!;

        // ════════════════════════════════════════════════════════════════════
        // COLOR STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the last known foreground RGB color (alpha forced to 255).
        /// </summary>
        public uint? LastKnownFgRgb { get; private set; }

        /// <summary>
        /// Gets the last known background RGB color (alpha forced to 255).
        /// </summary>
        public uint? LastKnownBgRgb { get; private set; }

        // ════════════════════════════════════════════════════════════════════
        // TOOL TYPE CONVENIENCE PROPERTIES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets a value indicating whether the active tool is Brush.</summary>
        public bool IsBrush => ActiveToolId == ToolIds.Brush;
        /// <summary>Gets a value indicating whether the active tool is Eraser.</summary>
        public bool IsEraser => ActiveToolId == ToolIds.Eraser;
        /// <summary>Gets a value indicating whether the active tool is Pan.</summary>
        public bool IsPan => ActiveToolId == ToolIds.Pan;
        /// <summary>Gets a value indicating whether the active tool is Magnifier/Zoom.</summary>
        public bool IsMagnifier => ActiveToolId == ToolIds.Zoom;
        /// <summary>Gets a value indicating whether the active tool is Dropper.</summary>
        public bool IsDropper => ActiveToolId == ToolIds.Dropper;
        /// <summary>Gets a value indicating whether the active tool is Replacer.</summary>
        public bool IsReplacer => ActiveToolId == ToolIds.Replacer;
        /// <summary>Gets a value indicating whether the active tool is Fill.</summary>
        public bool IsFill => ActiveToolId == ToolIds.Fill;
        /// <summary>Gets a value indicating whether the active tool is Blur.</summary>
        public bool IsBlur => ActiveToolId == ToolIds.Blur;
        /// <summary>Gets a value indicating whether the active tool is Jumble.</summary>
        public bool IsJumble => ActiveToolId == ToolIds.Jumble;
        /// <summary>Gets a value indicating whether the active tool is Rectangle.</summary>
        public bool IsRect => ActiveToolId == ToolIds.ShapeRect;
        /// <summary>Gets a value indicating whether the active tool is Ellipse.</summary>
        public bool IsEllipse => ActiveToolId == ToolIds.ShapeEllipse;
        /// <summary>Gets a value indicating whether the active tool is Smudge.</summary>
        public bool IsSmudge => ActiveToolId == ToolIds.Smudge;
        /// <summary>Gets a value indicating whether the active tool is GradientFill.</summary>
        public bool IsGradientFill => ActiveToolId == ToolIds.GradientFill;
        /// <summary>Gets a value indicating whether the active tool is TileStamper.</summary>
        public bool IsTileStamper => ActiveToolId == ToolIds.TileStamper;
        /// <summary>Gets a value indicating whether the active tool is TileModifier.</summary>
        public bool IsTileModifier => ActiveToolId == ToolIds.TileModifier;

        // ════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes a new instance of <see cref="ToolState"/> with the shared tool registry.
        /// </summary>
        public ToolState() : this(ToolRegistry.Shared)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ToolState"/> with a custom tool registry.
        /// </summary>
        /// <param name="registry">The tool registry to use.</param>
        public ToolState(IToolRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            // Initialize symmetry tool settings with the shared symmetry settings
            SymmetryTool = new SymmetryToolSettings(Symmetry);

            // Initialize selection settings first (shared across all selection tools)
            Selection = new SelectionToolSettings();

            // Initialize selection tools with shared SelectionSettings
            SelectRect = new SelectRectToolSettings { SelectionSettings = Selection };
            Wand = new WandToolSettings { SelectionSettings = Selection };
            Lasso = new LassoToolSettings { SelectionSettings = Selection };
            PaintSelect = new PaintSelectToolSettings { SelectionSettings = Selection };

            // Register all built-in tools with the registry
            BuiltInBrushTools.RegisterAll(_registry, this);
            BuiltInTileTools.RegisterAll(_registry, this);
            BuiltInShapeTools.RegisterAll(_registry, this);
            BuiltInSelectionTools.RegisterAll(_registry, this);
            BuiltInUtilityTools.RegisterAll(_registry, this);

            // Wire all settings Changed events to the unified OptionsChanged event
            BrushTool.Changed += () => OptionsChanged?.Invoke();
            Eraser.Changed += () => OptionsChanged?.Invoke();
            Fill.Changed += () => OptionsChanged?.Invoke();
            Replacer.Changed += () => OptionsChanged?.Invoke();
            Rect.Changed += () => OptionsChanged?.Invoke();
            Ellipse.Changed += () => OptionsChanged?.Invoke();
            Jumble.Changed += () => OptionsChanged?.Invoke();
            Smudge.Changed += () => OptionsChanged?.Invoke();
            Blur.Changed += () => OptionsChanged?.Invoke();
            Gradient.Changed += () => OptionsChanged?.Invoke();
            GradientFill.Changed += () => OptionsChanged?.Invoke();
            Selection.Changed += () => OptionsChanged?.Invoke();
            SelectRect.Changed += () => OptionsChanged?.Invoke();
            Wand.Changed += () => OptionsChanged?.Invoke();
            Lasso.Changed += () => OptionsChanged?.Invoke();
            PaintSelect.Changed += () => OptionsChanged?.Invoke();
            Pan.Changed += () => OptionsChanged?.Invoke();
            Zoom.Changed += () => OptionsChanged?.Invoke();
            Dropper.Changed += () => OptionsChanged?.Invoke();
            TileStamper.Changed += () => OptionsChanged?.Invoke();
            TileModifier.Changed += () => OptionsChanged?.Invoke();
            TileAnimation.Changed += () => OptionsChanged?.Invoke();
            SymmetryTool.Changed += () => OptionsChanged?.Invoke();

            // Wire selection commit/cancel events
            Selection.CommitRequested += () => SelectionCommitRequested?.Invoke();
            Selection.CancelRequested += () => SelectionCancelRequested?.Invoke();
            Selection.FlipHorizontalRequested += useGlobal => SelectionFlipHorizontalRequested?.Invoke(useGlobal);
            Selection.FlipVerticalRequested += useGlobal => SelectionFlipVerticalRequested?.Invoke(useGlobal);
        }

        // ════════════════════════════════════════════════════════════════════
        // TOOL SETTINGS ACCESS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the settings object for the currently active tool.
        /// </summary>
        public ToolSettingsBase? ActiveSettings => GetSettingsForToolId(ActiveToolId);

        /// <summary>
        /// Gets the settings object for the specified tool ID.
        /// </summary>
        /// <param name="toolId">The tool ID to get settings for.</param>
        /// <returns>The tool settings, or null for tools without custom settings.</returns>
        public ToolSettingsBase? GetSettingsForToolId(string toolId) => toolId switch
        {
            ToolIds.Brush => BrushTool,
            ToolIds.Eraser => Eraser,
            ToolIds.Fill => Fill,
            ToolIds.Replacer => Replacer,
            ToolIds.ShapeRect => Rect,
            ToolIds.ShapeEllipse => Ellipse,
            ToolIds.Jumble => Jumble,
            ToolIds.Smudge => Smudge,
            ToolIds.Blur => Blur,
            ToolIds.Gradient => Gradient,
            ToolIds.GradientFill => GradientFill,
            ToolIds.SelectRect => SelectRect,
            ToolIds.Wand => Wand,
            ToolIds.Lasso => Lasso,
            ToolIds.PaintSelect => PaintSelect,
            ToolIds.Pan => Pan,
            ToolIds.Zoom => Zoom,
            ToolIds.Dropper => Dropper,
            ToolIds.TileStamper => TileStamper,
            ToolIds.TileModifier => TileModifier,
            ToolIds.TileAnimation => TileAnimation,
            ToolIds.Symmetry => SymmetryTool,
            _ => GetPluginToolSettings(toolId)
        };

        /// <summary>
        /// Gets settings for a plugin tool, injecting SelectionSettings for selection tools.
        /// Also ensures the settings' Changed event is wired to OptionsChanged.
        /// </summary>
        private ToolSettingsBase? GetPluginToolSettings(string toolId)
        {
            var registration = GetRegistration(toolId);
            if (registration?.Settings == null)
                return null;

            var settings = registration.Settings;

            // For plugin selection tools, inject the shared SelectionSettings
            // so they get the transform options (scale, rotation, etc.)
            if (registration.Category == ToolCategory.Select &&
                settings is Plugins.PluginToolSettings pluginSettings)
            {
                pluginSettings.SelectionSettings = Selection;
            }

            // Ensure the settings' Changed event is wired to OptionsChanged
            // We track which settings have been wired to avoid duplicate subscriptions
            if (!_wiredPluginSettings.Contains(toolId))
            {
                _wiredPluginSettings.Add(toolId);
                settings.Changed += () => OptionsChanged?.Invoke();
            }

            return settings;
        }

        /// <summary>
        /// Tracks which plugin tool settings have had their Changed events wired.
        /// </summary>
        private readonly HashSet<string> _wiredPluginSettings = new();

        /// <summary>
        /// Gets all tool settings objects with their string IDs.
        /// Used for shortcut matching and iteration over tools.
        /// </summary>
        /// <returns>Enumerable of (string Id, ToolSettingsBase Settings) tuples.</returns>
        public IEnumerable<(string Id, ToolSettingsBase Settings)> GetAllToolSettingsById()
        {
            yield return (ToolIds.Brush, BrushTool);
            yield return (ToolIds.Eraser, Eraser);
            yield return (ToolIds.Fill, Fill);
            yield return (ToolIds.Replacer, Replacer);
            yield return (ToolIds.ShapeRect, Rect);
            yield return (ToolIds.ShapeEllipse, Ellipse);
            yield return (ToolIds.Jumble, Jumble);
            yield return (ToolIds.Smudge, Smudge);
            yield return (ToolIds.Blur, Blur);
            yield return (ToolIds.Gradient, Gradient);
            yield return (ToolIds.GradientFill, GradientFill);
            yield return (ToolIds.TileStamper, TileStamper);
            yield return (ToolIds.TileModifier, TileModifier);
            yield return (ToolIds.TileAnimation, TileAnimation);
            yield return (ToolIds.SelectRect, SelectRect);
            yield return (ToolIds.Wand, Wand);
            yield return (ToolIds.Lasso, Lasso);
            yield return (ToolIds.PaintSelect, PaintSelect);
            yield return (ToolIds.Pan, Pan);
            yield return (ToolIds.Zoom, Zoom);
            yield return (ToolIds.Dropper, Dropper);
            yield return (ToolIds.Symmetry, SymmetryTool);
        }

        /// <summary>
        /// Attempts to find and activate a tool matching the given key binding.
        /// Uses custom shortcut overrides from ShortcutSettings when available.
        /// </summary>
        /// <param name="key">The pressed key (Windows.System.VirtualKey).</param>
        /// <param name="ctrl">Whether Ctrl is held.</param>
        /// <param name="shift">Whether Shift is held.</param>
        /// <param name="alt">Whether Alt is held.</param>
        /// <returns>True if a matching tool was found and activated; false otherwise.</returns>
        public bool TryActivateByShortcut(Windows.System.VirtualKey key, bool ctrl, bool shift, bool alt)
        {
            int keyCode = (int)key;
            var shortcutSettings = Core.Settings.ShortcutSettings.Instance;

            // Check built-in tools
            foreach (var (id, settings) in GetAllToolSettingsById())
            {
                var effectiveBinding = shortcutSettings.GetEffectiveBinding(id, settings.Shortcut);
                if (effectiveBinding?.Matches(keyCode, ctrl, shift, alt) == true)
                {
                    SetById(id);
                    return true;
                }
            }

            // Check plugin tools
            foreach (var registration in AllRegistrations)
            {
                // Skip built-in (already checked above)
                if (ToolIds.IsBuiltIn(registration.Id))
                    continue;

                if (registration.Settings?.Shortcut == null)
                    continue;

                var effectiveBinding = shortcutSettings.GetEffectiveBinding(
                    registration.Id,
                    registration.Settings.Shortcut);

                if (effectiveBinding?.Matches(keyCode, ctrl, shift, alt) == true)
                {
                    SetById(registration.Id);
                    return true;
                }
            }

            return false;
        }

        // ════════════════════════════════════════════════════════════════════
        // BRUSH METHODS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the universal BrushSettings via a mutator action.
        /// Also synchronizes with the active tool's settings if it implements IBrushLikeSettings.
        /// </summary>
        /// <remarks>
        /// This method allows universal brush shortcuts (like [ ] for size, Ctrl+wheel)
        /// to affect the currently active brush-like tool.
        /// IMPORTANT: First syncs FROM the active tool to avoid desync where universal Brush
        /// has stale/default values.
        /// </remarks>
        public void UpdateBrush(Action<BrushSettings> mutator)
        {
            // CRITICAL: First sync FROM active tool to get current values
            // This prevents the universal Brush from overwriting tool settings with stale defaults
            SyncBrushFromActiveToolQuiet();

            // Apply the mutation (e.g., size adjustment)
            mutator?.Invoke(Brush);

            // Synchronize back to the active tool's settings
            SyncBrushToActiveTool();

            BrushChanged?.Invoke(Brush);
        }

        /// <summary>
        /// Synchronizes the universal BrushSettings to the active tool's IBrushLikeSettings.
        /// </summary>
        private void SyncBrushToActiveTool()
        {
            var settings = ActiveSettings;
            if (settings == null) return;

            // Skip utility tools that don't use brush settings (Pan, Zoom, Dropper)
            if (settings is PanToolSettings || settings is ZoomToolSettings || settings is DropperToolSettings)
                return;

            // Update based on the settings type
            switch (settings)
            {
                case BrushToolSettings brush:
                    brush.SetSize(Brush.Size);
                    brush.SetShape(Brush.Shape);
                    brush.SetOpacity(Brush.Opacity);
                    brush.SetDensity(Brush.Density);
                    break;
                case EraserToolSettings eraser:
                    eraser.SetSize(Brush.Size);
                    eraser.SetShape(Brush.Shape);
                    eraser.SetDensity(Brush.Density);
                    break;
                case BlurToolSettings blur:
                    blur.SetSize(Brush.Size);
                    blur.SetShape(Brush.Shape);
                    blur.SetDensity(Brush.Density);
                    break;
                case JumbleToolSettings jumble:
                    // Jumble uses strength-based falloff, not density
                    jumble.SetSize(Brush.Size);
                    jumble.SetShape(Brush.Shape);
                    break;
                case SmudgeToolSettings smudge:
                    smudge.SetSize(Brush.Size);
                    smudge.SetShape(Brush.Shape);
                    smudge.SetDensity(Brush.Density);
                    break;
                case ReplacerToolSettings replacer:
                    replacer.SetSize(Brush.Size);
                    replacer.SetShape(Brush.Shape);
                    replacer.SetDensity(Brush.Density);
                    break;
                case GradientToolSettings gradient:
                    gradient.SetSize(Brush.Size);
                    gradient.SetShape(Brush.Shape);
                    gradient.SetDensity(Brush.Density);
                    break;
                case PaintSelectToolSettings paintSelect:
                    paintSelect.SetSize(Brush.Size);
                    paintSelect.SetShape(Brush.Shape);
                    break;
                case RectToolSettings rect:
                    // Shape tools sync size to stroke width
                    rect.SetStrokeWidth(Brush.Size);
                    rect.SetShape(Brush.Shape);
                    rect.SetOpacity(Brush.Opacity);
                    rect.SetDensity(Brush.Density);
                    break;
                case EllipseToolSettings ellipse:
                    // Shape tools sync size to stroke width
                    ellipse.SetStrokeWidth(Brush.Size);
                    ellipse.SetShape(Brush.Shape);
                    ellipse.SetOpacity(Brush.Opacity);
                    ellipse.SetDensity(Brush.Density);
                    break;
                case Plugins.PluginToolSettings pluginSettings:
                    // Plugin tools (including shape tools) - use setter methods
                    pluginSettings.SetSize(Brush.Size);
                    pluginSettings.SetShape(Brush.Shape);
                    pluginSettings.SetOpacity(Brush.Opacity);
                    pluginSettings.SetDensity(Brush.Density);
                    break;
                default:
                    // For any other settings implementing the interfaces
                    SyncBrushToSettingsViaInterfaces(settings);
                    break;
            }
        }

        /// <summary>
        /// Synchronizes brush settings via interface-based duck typing.
        /// Used for plugin tools that implement IStrokeSettings, IOpacitySettings, IDensitySettings.
        /// DynamicDependency attributes ensure methods are preserved during trimming.
        /// </summary>
        [DynamicDependency("SetSize", typeof(PluginToolSettings))]
        [DynamicDependency("SetShape", typeof(PluginToolSettings))]
        [DynamicDependency("SetOpacity", typeof(PluginToolSettings))]
        [DynamicDependency("SetDensity", typeof(PluginToolSettings))]
        private void SyncBrushToSettingsViaInterfaces(ToolSettingsBase settings)
        {
            // Use reflection to call setter methods if they exist
            // This allows plugin tools with SetSize/SetShape/SetOpacity/SetDensity to respond
            var type = settings.GetType();

            // Try SetSize
            type.GetMethod("SetSize", new[] { typeof(int) })?.Invoke(settings, new object[] { Brush.Size });

            // Try SetShape
            type.GetMethod("SetShape", new[] { typeof(BrushShape) })?.Invoke(settings, new object[] { Brush.Shape });

            // Try SetOpacity
            type.GetMethod("SetOpacity", new[] { typeof(byte) })?.Invoke(settings, new object[] { Brush.Opacity });

            // Try SetDensity
            type.GetMethod("SetDensity", new[] { typeof(byte) })?.Invoke(settings, new object[] { Brush.Density });
        }

        /// <summary>
        /// Synchronizes the universal BrushSettings from the active tool's settings.
        /// Call this when switching tools to pick up the tool's current settings.
        /// </summary>
        public void SyncBrushFromActiveTool()
        {
            if (SyncBrushFromActiveToolQuiet())
            {
                BrushChanged?.Invoke(Brush);
            }
        }

        /// <summary>
        /// Synchronizes the universal BrushSettings from the active tool's settings WITHOUT firing BrushChanged.
        /// Used internally by UpdateBrush to avoid recursive event firing.
        /// </summary>
        /// <returns>True if sync was performed, false if active tool doesn't have stroke settings.</returns>
        private bool SyncBrushFromActiveToolQuiet()
        {
            var settings = ActiveSettings;
            if (settings is IStrokeSettings strokeSettings)
            {
                Brush.Size = strokeSettings.Size;
                Brush.Shape = strokeSettings.Shape;
                Brush.Density = settings is IDensitySettings ds ? ds.Density : (byte)255;
                Brush.Opacity = settings is IOpacitySettings os ? os.Opacity : (byte)255;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets whether shapes are drawn filled or outlined.
        /// </summary>
        public void SetShapeFilled(bool filled)
        {
            if (Brush.Filled == filled) return;
            Brush.Filled = filled;
            BrushChanged?.Invoke(Brush);
        }

        // ════════════════════════════════════════════════════════════════════
        // COLOR STATE METHODS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Stores the last known foreground RGB (alpha forced to 255).
        /// </summary>
        public void SetKnownFg(uint bgra)
        {
            LastKnownFgRgb = 0xFF000000u | (bgra & 0x00FFFFFFu);
            Gradient.LastKnownFg = LastKnownFgRgb;
            GradientFill.LastKnownFg = LastKnownFgRgb;
        }

        /// <summary>
        /// Stores the last known background RGB (alpha forced to 255).
        /// </summary>
        public void SetKnownBg(uint bgra)
        {
            LastKnownBgRgb = 0xFF000000u | (bgra & 0x00FFFFFFu);
            Gradient.LastKnownBg = LastKnownBgRgb;
            GradientFill.LastKnownBg = LastKnownBgRgb;
        }

        // ════════════════════════════════════════════════════════════════════
        // LEGACY COMPATIBILITY PROPERTIES (delegate to settings objects)
        // ════════════════════════════════════════════════════════════════════

        // Fill Tool
        /// <summary>Gets or sets a value indicating whether fill tool uses contiguous mode.</summary>
        public bool FillContiguous => Fill.Contiguous;
        /// <summary>Gets the fill tool color tolerance (0-255).</summary>
        public int FillTolerance => Fill.Tolerance;
        /// <summary>Sets the fill tool tolerance.</summary>
        public void SetFillTolerance(int tol) => Fill.SetTolerance(tol);
        /// <summary>Sets whether fill tool uses contiguous mode.</summary>
        public void SetFillContiguous(bool contiguous) => Fill.SetContiguous(contiguous);

        // Replacer Tool
        /// <summary>Gets whether replacer ignores alpha.</summary>
        public bool ReplaceIgnoreAlpha => Replacer.IgnoreAlpha;
        /// <summary>Sets whether replacer ignores alpha.</summary>
        public void SetReplaceIgnoreAlpha(bool v) => Replacer.SetIgnoreAlpha(v);

        // Jumble Tool
        /// <summary>Gets jumble strength percentage.</summary>
        public int JumbleStrengthPercent => Jumble.StrengthPercent;
        /// <summary>Gets jumble falloff gamma.</summary>
        public double JumbleFalloffGamma => Jumble.FalloffGamma;
        /// <summary>Gets jumble locality percentage.</summary>
        public int JumbleLocalityPercent => Jumble.LocalityPercent;
        /// <summary>Gets whether jumble includes transparent.</summary>
        public bool JumbleIncludeTransparent => Jumble.IncludeTransparent;
        /// <summary>Sets jumble strength.</summary>
        public void SetJumbleStrengthPercent(int pct) => Jumble.SetStrengthPercent(pct);
        /// <summary>Sets jumble falloff gamma.</summary>
        public void SetJumbleFalloffGamma(double g) => Jumble.SetFalloffGamma(g);
        /// <summary>Sets jumble locality.</summary>
        public void SetJumbleLocalityPercent(int pct) => Jumble.SetLocalityPercent(pct);
        /// <summary>Sets whether jumble includes transparent.</summary>
        public void SetJumbleIncludeTransparent(bool v) => Jumble.SetIncludeTransparent(v);

        // Smudge Tool
        /// <summary>Gets smudge strength percentage.</summary>
        public int SmudgeStrengthPercent => Smudge.StrengthPercent;
        /// <summary>Gets smudge falloff gamma.</summary>
        public double SmudgeFalloffGamma => Smudge.FalloffGamma;
        /// <summary>Gets whether smudge uses hard edge.</summary>
        public bool SmudgeHardEdge => Smudge.HardEdge;
        /// <summary>Gets whether smudge blends on transparent.</summary>
        public bool SmudgeBlendOnTransparent => Smudge.BlendOnTransparent;
        /// <summary>Sets smudge strength.</summary>
        public void SetSmudgeStrengthPercent(int percent) => Smudge.SetStrengthPercent(percent);
        /// <summary>Sets smudge falloff gamma.</summary>
        public void SetSmudgeFalloffGamma(double gamma) => Smudge.SetFalloffGamma(gamma);
        /// <summary>Sets whether smudge uses hard edge.</summary>
        public void SetSmudgeHardEdge(bool value) => Smudge.SetHardEdge(value);
        /// <summary>Sets whether smudge blends on transparent.</summary>
        public void SetSmudgeBlendOnTransparent(bool value) => Smudge.SetBlendOnTransparent(value);

        // Blur Tool
        /// <summary>Gets blur size.</summary>
        public int BlurSize => Blur.Size;
        /// <summary>Gets blur shape.</summary>
        public BrushShape BlurShape => Blur.Shape;
        /// <summary>Gets blur density.</summary>
        public byte BlurDensity => Blur.Density;
        /// <summary>Gets blur strength percentage.</summary>
        public int BlurStrengthPercent => Blur.StrengthPercent;
        /// <summary>Sets blur size.</summary>
        public void SetBlurSize(int size) => Blur.SetSize(size);
        /// <summary>Sets blur shape.</summary>
        public void SetBlurShape(BrushShape shape) => Blur.SetShape(shape);
        /// <summary>Sets blur density.</summary>
        public void SetBlurDensity(byte density) => Blur.SetDensity(density);
        /// <summary>Sets blur strength percentage.</summary>
        public void SetBlurStrengthPercent(int strength) => Blur.SetStrengthPercent(strength);

        // Gradient Tool
        /// <summary>Gets gradient colors.</summary>
        public IReadOnlyList<uint> GradientColors => Gradient.Colors;
        /// <summary>Gets whether gradient ignores alpha.</summary>
        public bool GradientIgnoreAlpha => Gradient.IgnoreAlpha;
        /// <summary>Gets whether gradient loops.</summary>
        public bool GradientLoop => Gradient.Loop;
        /// <summary>Gets last gradient pick.</summary>
        public uint? LastGradientPick => Gradient.LastPick;
        /// <summary>Sets whether gradient ignores alpha.</summary>
        public void SetGradientIgnoreAlpha(bool v) => Gradient.SetIgnoreAlpha(v);
        /// <summary>Sets whether gradient loops.</summary>
        public void SetGradientLoop(bool v) => Gradient.SetLoop(v);
        /// <summary>Clears gradient.</summary>
        public void ClearGradient() => Gradient.Clear();
        /// <summary>Adds gradient color.</summary>
        public void AddGradientColor(uint bgra) => Gradient.AddColor(bgra);
        /// <summary>Adds multiple gradient colors.</summary>
        public void AddGradientColors(IEnumerable<uint> colors) => Gradient.AddColors(colors);
        /// <summary>Removes gradient at index.</summary>
        public void RemoveGradientAt(int index) => Gradient.RemoveAt(index);
        /// <summary>Replaces gradient at index.</summary>
        public void ReplaceGradientAt(int index, uint bgra) => Gradient.ReplaceAt(index, bgra);
        /// <summary>Moves gradient.</summary>
        public void MoveGradient(int from, int to) => Gradient.Move(from, to);
        /// <summary>Reverses gradient.</summary>
        public void ReverseGradient() => Gradient.Reverse();

        // Selection Tool
        /// <summary>Gets selection scale X percent.</summary>
        public double SelScalePercentX => Selection.ScalePercentX;
        /// <summary>Gets selection scale Y percent.</summary>
        public double SelScalePercentY => Selection.ScalePercentY;
        /// <summary>Gets whether selection scale is linked.</summary>
        public bool SelScaleLink => Selection.ScaleLink;
        /// <summary>Gets selection scale mode.</summary>
        public ScaleMode SelScaleMode => Selection.ScaleMode;
        /// <summary>Gets whether selection is active.</summary>
        public bool SelActive => Selection.Active;
        /// <summary>Gets whether selection is floating.</summary>
        public bool SelFloating => Selection.Floating;
        /// <summary>Gets rotation angle.</summary>
        public double RotationAngleDeg => Selection.RotationAngleDeg;
        /// <summary>Gets rotation mode.</summary>
        public RotationMode RotationMode => Selection.RotationMode;
        /// <summary>Sets selection scale.</summary>
        public void SetSelectionScale(double px, double py, bool link) => Selection.SetScale(px, py, link);
        /// <summary>Sets selection scale mode.</summary>
        public void SetSelectionScaleMode(ScaleMode mode) => Selection.SetScaleMode(mode);
        /// <summary>Sets selection presence.</summary>
        public void SetSelectionPresence(bool active, bool floating) => Selection.SetPresence(active, floating);
        /// <summary>Sets rotation angle.</summary>
        public void SetRotationAngle(double degrees) => Selection.SetRotationAngle(degrees);
        /// <summary>Sets rotation mode.</summary>
        public void SetRotationMode(RotationMode mode) => Selection.SetRotationMode(mode);
        /// <summary>Requests selection commit.</summary>
        public void RequestSelectionCommit() => Selection.RequestCommit();
        /// <summary>Requests selection cancel.</summary>
        public void RequestSelectionCancel() => Selection.RequestCancel();

        // Wand Tool
        /// <summary>Gets wand tolerance.</summary>
        public int WandTolerance => Wand.Tolerance;
        /// <summary>Gets whether wand is contiguous.</summary>
        public bool WandContiguous => Wand.Contiguous;
        /// <summary>Sets wand tolerance.</summary>
        public void SetWandTolerance(int tol) => Wand.SetTolerance(tol);
        /// <summary>Sets whether wand is contiguous.</summary>
        public void SetWandContiguous(bool contiguous) => Wand.SetContiguous(contiguous);

        // PaintSelect Tool
        /// <summary>Gets paint select shape.</summary>
        public BrushShape PaintSelectShape => PaintSelect.Shape;
        /// <summary>Gets paint select size.</summary>
        public int PaintSelectSize => PaintSelect.Size;
        /// <summary>Sets paint select shape.</summary>
        public void SetPaintSelectShape(BrushShape shape) => PaintSelect.SetShape(shape);
        /// <summary>Sets paint select size.</summary>
        public void SetPaintSelectSize(int size) => PaintSelect.SetSize(size);
    }
}