using System;
using Microsoft.UI.Xaml.Controls;

namespace PixlPunkt.UI.Helpers
{
    /// <summary>
    /// Provides two-way binding synchronization between <see cref="Slider"/> and <see cref="NumberBox"/> controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SliderNumberBoxBinder prevents infinite update loops when synchronizing slider and number box values
    /// in two-way binding scenarios. Common pattern in settings/adjustment UI where users need both continuous
    /// slider control and precise numeric input.
    /// </para>
    /// <para><strong>Problem Solved:</strong></para>
    /// <para>
    /// Without suppression, value changes trigger:
    /// <br/>1. User drags slider ? slider.ValueChanged fires
    /// <br/>2. Updates number box ? numberBox.ValueChanged fires
    /// <br/>3. Updates slider ? slider.ValueChanged fires again (infinite loop!)
    /// </para>
    /// <para><strong>Solution:</strong></para>
    /// <para>
    /// Uses <c>_suppress</c> flag to prevent re-entrant updates. When one control changes,
    /// flag is set before updating the other control, preventing the change event from
    /// propagating back.
    /// </para>
    /// <para><strong>Usage Pattern:</strong></para>
    /// <code>
    /// var binder = new SliderNumberBoxBinder();
    /// binder.BindPair(opacitySlider, opacityNumberBox, newValue => 
    /// {
    ///     // Apply value change (e.g., update layer opacity)
    ///     CurrentLayer.Opacity = (byte)newValue;
    /// });
    /// </code>
    /// </remarks>
    public class SliderNumberBoxBinder
    {
        private bool _suppress;

        /// <summary>
        /// Binds a slider and number box pair with bidirectional synchronization.
        /// </summary>
        /// <param name="slider">Slider control for continuous value adjustment.</param>
        /// <param name="numberBox">NumberBox control for precise numeric input.</param>
        /// <param name="onValueChanged">Callback invoked when value changes from either control.
        /// Receives the new value.</param>
        /// <remarks>
        /// <para>
        /// Both controls should have matching Min/Max ranges for consistent behavior.
        /// The callback is invoked only once per user interaction (not during synchronization).
        /// </para>
        /// <para>
        /// Slider and NumberBox values are automatically kept in sync. User changes to either
        /// control update the other and trigger the callback exactly once.
        /// </para>
        /// </remarks>
        public void BindPair(Slider slider, NumberBox numberBox, Action<double> onValueChanged)
        {
            slider.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                _suppress = true;
                numberBox.Value = e.NewValue;
                _suppress = false;
                onValueChanged?.Invoke(e.NewValue);
            };

            numberBox.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                _suppress = true;
                slider.Value = e.NewValue;
                _suppress = false;
                onValueChanged?.Invoke(e.NewValue);
            };
        }
    }
}