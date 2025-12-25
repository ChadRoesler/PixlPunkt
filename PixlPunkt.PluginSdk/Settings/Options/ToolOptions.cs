using FluentIcons.Common;
using PixlPunkt.PluginSdk.Imaging;
using PixlPunkt.PluginSdk.UI;

namespace PixlPunkt.PluginSdk.Settings.Options
{
    /// <summary>
    /// Slider option for numeric values (int, double, byte).
    /// </summary>
    /// <param name="Id">Unique identifier for this option.</param>
    /// <param name="Label">Display label shown next to the slider.</param>
    /// <param name="Min">Minimum allowed value.</param>
    /// <param name="Max">Maximum allowed value.</param>
    /// <param name="Value">Current/initial value.</param>
    /// <param name="OnChanged">Callback invoked when the value changes.</param>
    /// <param name="Order">Sort order within the toolbar (lower = earlier).</param>
    /// <param name="Group">Optional grouping for organizing options.</param>
    /// <param name="Tooltip">Optional tooltip text.</param>
    /// <param name="Step">Step increment for the slider. Default is 1.</param>
    /// <param name="ShowNumberBox">Whether to show a NumberBox for precise input. Default is true.</param>
    /// <param name="ShowLabel">Whether to show the label text. Default is true.</param>
    public sealed record SliderOption(
        string Id,
        string Label,
        double Min,
        double Max,
        double Value,
        Action<double> OnChanged,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        double Step = 1,
        bool ShowNumberBox = true,
        bool ShowLabel = true
    ) : IToolOption;

    /// <summary>
    /// Toggle/checkbox option for boolean values.
    /// </summary>
    public sealed record ToggleOption(
        string Id,
        string Label,
        bool Value,
        Action<bool> OnChanged,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null
    ) : IToolOption;

    /// <summary>
    /// Shape selector option for <see cref="BrushShape"/> selection.
    /// </summary>
    public sealed record ShapeOption(
        string Id,
        string Label,
        BrushShape Value,
        Action<BrushShape> OnChanged,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        bool ShowLabel = true
    ) : IToolOption;

    /// <summary>
    /// Button option for triggering actions or opening dialogs.
    /// </summary>
    public sealed record ButtonOption(
        string Id,
        string Label,
        Icon? Icon,
        Action OnClicked,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null
    ) : IToolOption;

    /// <summary>
    /// Dropdown/ComboBox option for selecting from a list of items.
    /// </summary>
    public sealed record DropdownOption(
        string Id,
        string Label,
        IReadOnlyList<string> Items,
        int SelectedIndex,
        Action<int> OnChanged,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        bool ShowLabel = true
    ) : IToolOption;

    /// <summary>
    /// Separator option for visual division between option groups.
    /// </summary>
    public sealed record SeparatorOption(
        string Id = "",
        int Order = 0,
        string Group = "General"
    ) : IToolOption
    {
        public string Label => "";
        public string? Tooltip => null;
    }

    /// <summary>
    /// Label option for displaying read-only text.
    /// </summary>
    public sealed record LabelOption(
        string Id,
        string Label,
        string Value,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null
    ) : IToolOption;

    /// <summary>
    /// Dynamic label option for displaying read-only text that updates in real-time.
    /// Uses a getter function to retrieve the current value on each UI refresh.
    /// </summary>
    /// <param name="Id">Unique identifier for this option.</param>
    /// <param name="Label">Display label shown before the value.</param>
    /// <param name="GetValue">Function that returns the current value to display.</param>
    /// <param name="Order">Sort order within the toolbar (lower = earlier).</param>
    /// <param name="Group">Optional grouping for organizing options.</param>
    /// <param name="Tooltip">Optional tooltip text.</param>
    /// <param name="MonospacedValue">Whether to render the value in a monospaced font (useful for hex codes).</param>
    public sealed record DynamicLabelOption(
        string Id,
        string Label,
        Func<string> GetValue,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        bool MonospacedValue = false
    ) : IToolOption;

    /// <summary>
    /// Icon display option for static FluentUI icon display.
    /// </summary>
    public sealed record IconOption(
        string Id,
        Icon Icon,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        IconSize Size = IconSize.Size20
    ) : IToolOption
    {
        public string Label => "";
    }

    /// <summary>
    /// Icon sizes for <see cref="IconOption"/>.
    /// </summary>
    public enum IconSize
    {
        Size16 = 16,
        Size20 = 20,
        Size24 = 24,
        Size28 = 28,
        Size32 = 32
    }

    /// <summary>
    /// Color palette option for displaying and editing a row of color swatches.
    /// </summary>
    public sealed record PaletteOption(
        string Id,
        string Label,
        IReadOnlyList<uint> Colors,
        int SelectedIndex,
        Action<int>? OnSelectionChanged,
        Action? OnAddRequested,
        Action? OnAddRampRequested,
        Action<int>? OnEditRequested,
        Action<int>? OnRemoveRequested,
        Action? OnClearRequested,
        Action? OnReverseRequested,
        Action<int, int>? OnMoveRequested,
        Action? OnColorsChanged = null,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        bool ShowLabel = false
    ) : IToolOption;

    /// <summary>
    /// Icon button option for compact icon-only buttons.
    /// </summary>
    public sealed record IconButtonOption(
        string Id,
        string Label,
        Icon Icon,
        Action OnClicked,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        bool IsToggle = false,
        bool IsChecked = false
    ) : IToolOption;

    /// <summary>
    /// Color picker option for single color selection with inline swatch.
    /// </summary>
    public sealed record ColorOption(
        string Id,
        string Label,
        uint Color,
        Action<uint> OnChanged,
        Action? OnPickRequested = null,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        bool ShowAlpha = true,
        bool ShowLabel = true
    ) : IToolOption;

    /// <summary>
    /// Color picker window option for advanced color selection.
    /// </summary>
    public sealed record ColorPickerWindowOption(
        string Id,
        string Label,
        Func<uint> GetCurrentColor,
        Action<uint>? OnLivePreview,
        Action<uint> OnCommit,
        Icon? Icon = null,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        string WindowTitle = "Color Picker",
        bool ShowAlpha = true
    ) : IToolOption;

    /// <summary>
    /// Gradient picker window option for generating color ramps.
    /// </summary>
    public sealed record GradientPickerWindowOption(
        string Id,
        string Label,
        Func<uint> GetStartColor,
        Func<uint> GetEndColor,
        Action<IReadOnlyList<uint>> OnCommit,
        Icon? Icon = null,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        string WindowTitle = "Gradient Swatch Maker"
    ) : IToolOption;

    /// <summary>
    /// Hue slider option for selecting hue values (0-360).
    /// </summary>
    public sealed record HueSliderOption(
        string Id,
        string Label,
        double Value,
        Action<double> OnChanged,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        bool ShowLabel = true,
        double Width = 140
    ) : IToolOption;

    /// <summary>
    /// Custom brush selector option for selecting from loaded custom brushes.
    /// </summary>
    public sealed record CustomBrushOption(
        string Id,
        string Label,
        string? SelectedBrushFullName,
        BrushShape BuiltInShape,
        Action<string> OnBrushSelected,
        Action<BrushShape> OnBuiltInShapeSelected,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        bool ShowLabel = true
    ) : IToolOption;

    /// <summary>
    /// Number box option for numeric values without a slider.
    /// </summary>
    public sealed record NumberBoxOption(
        string Id,
        string Label,
        double Min,
        double Max,
        double Value,
        Action<double> OnChanged,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        double Step = 1,
        bool ShowLabel = true,
        double Width = 64,
        string? Suffix = null
    ) : IToolOption;

    /// <summary>
    /// Icon toggle option for toggle buttons that change icon based on state.
    /// </summary>
    public sealed record IconToggleOption(
        string Id,
        string Label,
        Icon IconOn,
        Icon IconOff,
        bool Value,
        Action<bool> OnChanged,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        string? TooltipOn = null,
        string? TooltipOff = null
    ) : IToolOption;

    /// <summary>
    /// Tool option that opens a plugin-defined window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This option type renders as a button in the toolbar. When clicked, the host application
    /// creates and displays a window based on the <see cref="PluginWindowDescriptor"/>
    /// </para>
    /// <para>
    /// Use this for complex configuration UI that can't be expressed with simple toolbar controls.
    /// The window content is defined using the same <see cref="IToolOption"/> system, enabling
    /// consistent declarative UI patterns.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// public override IEnumerable&lt;IToolOption&gt; GetOptions()
    /// {
    ///     yield return new PluginWindowOption(
    ///         "advancedSettings",
    ///         "Advanced...",
    ///         Icon.Settings,
    ///         () => new PluginWindowDescriptor(
    ///             Title: "Advanced Settings",
    ///             GetContent: () => new IToolOption[]
    ///             {
    ///                 new SliderOption("threshold", "Threshold", 0, 100, _threshold, v => _threshold = (int)v),
    ///                 new ToggleOption("preview", "Live Preview", _preview, v => _preview = v)
    ///             }
    ///         ),
    ///         Order: 10
    ///     );
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="Id">Unique identifier for this option.</param>
    /// <param name="Label">Button text label.</param>
    /// <param name="Icon">Optional FluentUI icon for the button.</param>
    /// <param name="GetWindowDescriptor">Factory function that returns the window descriptor.</param>
    /// <param name="Order">Sort order within the toolbar.</param>
    /// <param name="Group">Optional grouping for organizing options.</param>
    /// <param name="Tooltip">Optional tooltip text.</param>
    public sealed record PluginWindowOption(
        string Id,
        string Label,
        Icon? Icon,
        Func<PluginWindowDescriptor> GetWindowDescriptor,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null
    ) : IToolOption;

    /// <summary>
    /// Gradient preview option for displaying and editing gradient colors.
    /// Shows a visual gradient strip with editable stops.
    /// </summary>
    /// <param name="Id">Unique identifier for this option.</param>
    /// <param name="Label">Display label.</param>
    /// <param name="GetStops">Function that returns current gradient stops.</param>
    /// <param name="OnEditRequested">Callback when user wants to edit the gradient (opens custom gradient editor).</param>
    /// <param name="Order">Sort order within the toolbar.</param>
    /// <param name="Group">Optional grouping.</param>
    /// <param name="Tooltip">Optional tooltip text.</param>
    /// <param name="Width">Width of the preview strip. Default is 200.</param>
    /// <param name="Height">Height of the preview strip. Default is 24.</param>
    public sealed record GradientPreviewOption(
        string Id,
        string Label,
        Func<IReadOnlyList<GradientStopInfo>> GetStops,
        Action? OnEditRequested = null,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        double Width = 200,
        double Height = 24
    ) : IToolOption;

    /// <summary>
    /// Information about a single gradient stop for the preview.
    /// </summary>
    public sealed record GradientStopInfo(double Position, uint Color);
}
