using System;

namespace PixlPunkt.Uno.Core.Tools
{
    /// <summary>
    /// Represents the configuration settings for brush-based drawing tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BrushSettings controls the appearance and behavior of brush strokes in the painting system.
    /// It manages size, shape, opacity, density, and spacing parameters that affect how pixels
    /// are applied during drawing operations.
    /// </para>
    /// <para>
    /// Key concepts:
    /// - **Size**: Brush diameter in pixels (1-128)
    /// - **Shape**: Geometric shape (Circle or Square)
    /// - **Opacity**: How much the brush color affects existing pixels (0-255)
    /// - **Density**: Coverage within the brush shape, creating speckled/dithered effects when low (0-255)
    /// - **Spacing**: Distance between stamp applications during stroke (1.0 = touching, &lt;1.0 = overlap, &gt;1.0 = gap)
    /// </para>
    /// <para>
    /// The <see cref="Changed"/> event fires whenever any setting is modified, allowing UI and
    /// rendering systems to respond immediately. Use the setter methods (SetSize, SetShape, etc.)
    /// to ensure proper validation and event notification.
    /// </para>
    /// </remarks>
    public sealed class BrushSettings
    {
        /// <summary>
        /// Gets or sets the brush size in pixels (diameter).
        /// </summary>
        /// <value>
        /// An integer from 1 to 128. Default is 1.
        /// Use <see cref="SetSize"/> for automatic clamping and change notification.
        /// </value>
        public int Size { get; set; } = 1;

        /// <summary>
        /// Gets or sets the brush shape.
        /// </summary>
        /// <value>
        /// A <see cref="BrushShape"/> enum value (Circle or Square). Default is Circle.
        /// Use <see cref="SetShape"/> for change notification.
        /// </value>
        public BrushShape Shape { get; set; } = BrushShape.Circle;

        /// <summary>
        /// Gets or sets the brush opacity (alpha value).
        /// </summary>
        /// <value>
        /// A byte from 0 (fully transparent) to 255 (fully opaque). Default is 255.
        /// Use <see cref="SetOpacity"/> for change notification.
        /// </value>
        /// <remarks>
        /// Opacity is multiplied with the paint color's alpha during stroke application.
        /// This allows for semi-transparent painting and glazing effects.
        /// </remarks>
        public byte Opacity { get; set; } = 255;

        /// <summary>
        /// Gets or sets the brush density (coverage).
        /// </summary>
        /// <value>
        /// A byte from 0 (sparse/dithered) to 255 (fully dense). Default is 255.
        /// </value>
        /// <remarks>
        /// <para>
        /// Density controls how many pixels within the brush shape are affected. At 255, all pixels
        /// in the shape are painted. At lower values, a random subset is painted, creating a
        /// speckled or spray-paint effect.
        /// </para>
        /// <para>
        /// This is distinct from opacity: density affects which pixels are touched, while opacity
        /// affects how strongly touched pixels are modified.
        /// </para>
        /// </remarks>
        public byte Density { get; set; } = 255;

        /// <summary>
        /// Gets or sets the spacing between brush stamps during a stroke.
        /// </summary>
        /// <value>
        /// A float where 1.0 means stamps touch edge-to-edge. Values less than 1.0 create overlap,
        /// values greater than 1.0 create gaps. Default is 1.0.
        /// </value>
        /// <remarks>
        /// Spacing is multiplied by the brush size to determine the distance between successive
        /// stamp applications as the cursor moves. Lower values create smoother, more continuous
        /// strokes but may be slower. Higher values create dotted lines.
        /// </remarks>
        public float Spacing { get; set; } = 1f;

        /// <summary>
        /// Occurs when any brush setting changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised by all setter methods (SetSize, SetShape, SetOpacity, SetDensity).
        /// Subscribers receive the entire BrushSettings instance to read the new configuration.
        /// </para>
        /// <para>
        /// UI controls typically subscribe to this event to update their display, and the brush
        /// mask cache subscribes to invalidate cached stamp bitmaps when settings change.
        /// </para>
        /// </remarks>
        public event Action<BrushSettings>? Changed;

        /// <summary>
        /// Sets the brush size with automatic clamping and change notification.
        /// </summary>
        /// <param name="v">The desired size in pixels. Will be clamped to range [1, 128].</param>
        /// <remarks>
        /// Fires the <see cref="Changed"/> event after updating the value. Prefer this method
        /// over directly setting the <see cref="Size"/> property to ensure proper validation
        /// and event notification.
        /// </remarks>
        public void SetSize(int v)
        {
            Size = Math.Clamp(v, 1, 128);
            Changed?.Invoke(this);
        }

        /// <summary>
        /// Sets the brush shape and fires the change notification.
        /// </summary>
        /// <param name="s">The new brush shape (Circle or Square).</param>
        public void SetShape(BrushShape s)
        {
            Shape = s;
            Changed?.Invoke(this);
        }

        /// <summary>
        /// Sets the brush opacity and fires the change notification.
        /// </summary>
        /// <param name="a">The opacity value (0-255).</param>
        public void SetOpacity(byte a)
        {
            Opacity = a;
            Changed?.Invoke(this);
        }

        /// <summary>
        /// Sets the brush density and fires the change notification.
        /// </summary>
        /// <param name="d">The density value (0-255).</param>
        public void SetDensity(byte d)
        {
            Density = d;
            Changed?.Invoke(this);
        }

        public bool Filled { get; set; } = false;

        public void SetFilled(bool f)
        {
            Filled = f;
            Changed?.Invoke(this);
        }
    }
}
