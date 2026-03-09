using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PixlPunkt.PluginSdk.Compositing
{
    /// <summary>
    /// Abstract base class for all layer effects that can be applied during compositing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LayerEffectBase provides the foundation for implementing visual effects that modify layer
    /// appearance during rendering. Effects are applied in-place to a pixel buffer after the layer
    /// is copied to a scratch surface but before final blending.
    /// </para>
    /// <para>
    /// All effect implementations must:
    /// </para>
    /// <list type="bullet">
    /// <item>Implement <see cref="Apply(Span{uint}, int, int)"/> to perform the pixel transformation</item>
    /// <item>Provide a <see cref="DisplayName"/> for UI display</item>
    /// </list>
    /// <para>
    /// Effects can be toggled on/off via <see cref="IsEnabled"/> without removing them from the collection.
    /// Property changes automatically notify observers via <see cref="INotifyPropertyChanged"/>.
    /// </para>
    /// <para>
    /// <strong>Example Implementation:</strong>
    /// </para>
    /// <code>
    /// public sealed class InvertEffect : LayerEffectBase
    /// {
    ///     public override string DisplayName => "Invert";
    ///
    ///     public override void Apply(Span&lt;uint&gt; pixels, int width, int height)
    ///     {
    ///         if (!IsEnabled) return;
    ///         for (int i = 0; i &lt; pixels.Length; i++)
    ///         {
    ///             uint p = pixels[i];
    ///             uint a = p &amp; 0xFF000000;
    ///             uint rgb = ~p &amp; 0x00FFFFFF;
    ///             pixels[i] = a | rgb;
    ///         }
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public abstract class LayerEffectBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        /// <remarks>
        /// Used to notify UI when effect settings change, triggering re-render of the composited image.
        /// </remarks>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets the unique identifier for this effect instance.
        /// </summary>
        /// <value>
        /// A string matching the registration ID from the effect registry.
        /// For example: <c>"com.myplugin.effect.halftone"</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// This ID is used to look up the effect's registration for UI options and metadata.
        /// Set automatically when effects are created via the registration's CreateInstance method.
        /// </para>
        /// <para>
        /// Effect IDs should follow the convention: <c>{vendor}.effect.{name}</c>
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public string? EffectId { get; set; }

        private bool _isEnabled = true;

        /// <summary>
        /// Gets or sets a value indicating whether this effect is currently enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if the effect should be applied during compositing; <c>false</c> to skip it.
        /// Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// Toggling this property allows effects to be temporarily disabled without removing them
        /// from the effect collection. Changes fire <see cref="PropertyChanged"/>.
        /// </remarks>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets whether this effect requires a read-only snapshot of the pixel buffer
        /// to avoid read-after-write hazards (e.g., neighborhood sampling, offset reads).
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <c>true</c>, the compositor will provide a pre-filled snapshot via
        /// <see cref="Apply(Span{uint}, ReadOnlySpan{uint}, int, int)"/>. This avoids
        /// per-effect heap allocations that would otherwise be needed via <c>pixels.ToArray()</c>.
        /// </para>
        /// <para>
        /// Override and return <c>true</c> for effects that sample neighboring or offset pixels
        /// while writing to the current pixel (e.g., blur, outline, distortion effects).
        /// Effects that only read/write a single pixel at a time (e.g., color adjust, grain)
        /// should leave this as <c>false</c>.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public virtual bool NeedsSnapshot => false;

        /// <summary>
        /// Applies the effect to the provided pixel buffer in-place.
        /// </summary>
        /// <param name="pixels">
        /// A span of pixels in packed BGRA format (0xAARRGGBB). The effect modifies this buffer directly.
        /// </param>
        /// <param name="width">The width of the pixel buffer in pixels.</param>
        /// <param name="height">The height of the pixel buffer in pixels.</param>
        /// <remarks>
        /// <para>
        /// This method is called during compositing after copying the layer to a scratch buffer.
        /// Implementations should perform their transformation directly on the provided span for performance.
        /// </para>
        /// <para>
        /// The pixel buffer size is always <paramref name="width"/> x <paramref name="height"/>.
        /// Each pixel is a 32-bit unsigned integer in BGRA order (Blue in lowest byte, Alpha in highest).
        /// </para>
        /// <para>
        /// Effects must handle edge cases (e.g., out-of-bounds access) gracefully. Check
        /// <see cref="IsEnabled"/> at the start and return early if disabled.
        /// </para>
        /// </remarks>
        public abstract void Apply(Span<uint> pixels, int width, int height);

        /// <summary>
        /// Applies the effect using a pre-filled read-only snapshot of the pixel data.
        /// </summary>
        /// <param name="pixels">
        /// The writable pixel buffer. The effect writes its output here.
        /// </param>
        /// <param name="snapshot">
        /// A read-only copy of <paramref name="pixels"/> taken before this effect runs.
        /// Effects should read source data from this span to avoid read-after-write hazards.
        /// </param>
        /// <param name="width">The width of the pixel buffer in pixels.</param>
        /// <param name="height">The height of the pixel buffer in pixels.</param>
        /// <remarks>
        /// <para>
        /// This overload is called by the compositor when <see cref="NeedsSnapshot"/> is <c>true</c>.
        /// The snapshot is managed by the compositor using pooled memory, eliminating per-effect
        /// heap allocations.
        /// </para>
        /// <para>
        /// The default implementation ignores the snapshot and delegates to
        /// <see cref="Apply(Span{uint}, int, int)"/>. Override this method in effects that
        /// need to sample from unmodified source pixels.
        /// </para>
        /// </remarks>
        public virtual void Apply(Span<uint> pixels, ReadOnlySpan<uint> snapshot, int width, int height)
        {
            Apply(pixels, width, height);
        }

        /// <summary>
        /// Gets the human-readable display name for this effect.
        /// </summary>
        /// <value>
        /// A string suitable for displaying in UI (e.g., "Halftone", "Chromatic Aberration").
        /// </value>
        /// <remarks>
        /// This property is excluded from JSON serialization. It's used purely for UI display
        /// in effect selection lists and layer panels.
        /// </remarks>
        [JsonIgnore]
        public abstract string DisplayName { get; }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property.
        /// </summary>
        /// <param name="name">
        /// The name of the property that changed. Automatically provided by the compiler when called
        /// from a property setter using <see cref="CallerMemberNameAttribute"/>.
        /// </param>
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
