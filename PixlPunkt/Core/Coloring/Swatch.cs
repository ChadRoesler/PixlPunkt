using System.ComponentModel;
using System.Runtime.CompilerServices;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Palette.Helpers.Defaults;

namespace PixlPunkt.Core.Coloring
{
    /// <summary>
    /// Represents a single color swatch in a palette picker UI with highlighting and selection state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Swatch provides UI-bindable state for color palette display, tracking whether a swatch
    /// is the currently selected color (<see cref="IsCenter"/>) and whether it matches a search
    /// or filter criterion (<see cref="IsMatch"/>). Implements <see cref="INotifyPropertyChanged"/>
    /// for WinUI 3 data binding support.
    /// </para>
    /// <para><strong>Properties:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Color</strong>: The immutable BGRA color value this swatch represents.</item>
    /// <item><strong>IsCenter</strong>: True if this is the active/selected swatch in a palette picker.
    /// Used for visual highlighting (e.g., bold border, different background).</item>
    /// <item><strong>IsMatch</strong>: Dynamic property indicating whether this swatch matches
    /// current search/filter criteria. Can be updated at runtime to highlight relevant colors.</item>
    /// </list>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// - Color picker UI where one swatch is "active" (IsCenter)
    /// <br/>- Palette search/filter where matching swatches are highlighted (IsMatch)
    /// <br/>- Quick swatch panels with selection state tracking
    /// <br/>- Gradient stops or color ramp visualization
    /// </para>
    /// </remarks>
    /// <seealso cref="PaletteService"/>
    /// <seealso cref="NamedPalette"/>
    public partial class Swatch : INotifyPropertyChanged
    {
        private bool _isMatch;

        /// <summary>
        /// Gets the BGRA color value represented by this swatch.
        /// </summary>
        /// <value>
        /// A packed 32-bit BGRA color (0xAARRGGBB format). Immutable after construction.
        /// </value>
        public uint Color { get; }

        /// <summary>
        /// Gets a value indicating whether this swatch is the center/active selection in a picker.
        /// </summary>
        /// <value>
        /// <c>true</c> if this swatch should be highlighted as the current selection;
        /// otherwise, <c>false</c>. Immutable after construction.
        /// </value>
        /// <remarks>
        /// Used to visually distinguish the active color in a palette grid or picker UI.
        /// Typically renders with a distinct border, background, or other styling to indicate selection.
        /// </remarks>
        public bool IsCenter { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this swatch matches current filter/search criteria.
        /// </summary>
        /// <value>
        /// <c>true</c> if this swatch matches the active filter; otherwise, <c>false</c>.
        /// Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// This property can be dynamically updated based on search queries, color similarity tests,
        /// or other filtering logic. Changes trigger <see cref="PropertyChanged"/> notification for
        /// UI updates.
        /// </remarks>
        public bool IsMatch
        {
            get => _isMatch;
            set
            {
                if (_isMatch != value)
                {
                    _isMatch = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Swatch"/> class.
        /// </summary>
        /// <param name="color">The BGRA color value for this swatch.</param>
        /// <param name="isCenter">True if this swatch is the center/active selection. Default is <c>false</c>.</param>
        /// <param name="isMatch">Initial match state for filtering. Default is <c>false</c>.</param>
        public Swatch(uint color, bool isCenter = false, bool isMatch = false)
        {
            Color = color;
            IsCenter = isCenter;
            _isMatch = isMatch;
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed. Automatically supplied via
        /// <c>[CallerMemberName]</c> attribute when called from property setters.</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
