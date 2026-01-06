using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Effects;
using PixlPunkt.Uno.UI.Tools;

namespace PixlPunkt.Uno.UI.Layers
{
    /// <summary>
    /// Dynamically generates effect settings UI from <see cref="IEffectRegistration.GetOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This template selector creates UI controls dynamically using the same <see cref="ToolOptionFactory"/>
    /// that powers the tool options bar. This ensures consistent UI patterns across tools and effects,
    /// and automatically supports plugin effects without requiring XAML template changes.
    /// </para>
    /// <para><strong>How it works:</strong></para>
    /// <list type="number">
    /// <item>Gets the effect's <see cref="LayerEffectBase.EffectId"/></item>
    /// <item>Looks up the registration in <see cref="EffectRegistry.Shared"/></item>
    /// <item>Calls <see cref="IEffectRegistration.GetOptions"/> to get option descriptors</item>
    /// <item>Uses <see cref="ToolOptionFactory.CreateControl"/> for each option</item>
    /// <item>Arranges controls in a vertical StackPanel</item>
    /// </list>
    /// <para><strong>Benefits:</strong></para>
    /// <list type="bullet">
    /// <item>No hardcoded XAML templates per effect type</item>
    /// <item>Plugin effects automatically get settings UI</item>
    /// <item>Consistent look and feel with tool options</item>
    /// <item>Single point of maintenance for UI generation</item>
    /// </list>
    /// </remarks>
    public sealed class DynamicEffectTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Gets or sets the action to invoke when any effect property changes.
        /// </summary>
        /// <remarks>
        /// This is typically wired to recomposite the layer/document.
        /// </remarks>
        public System.Action? OnEffectChanged { get; set; }

        /// <inheritdoc/>
        protected override DataTemplate? SelectTemplateCore(object item)
            => SelectTemplateCore(item, null);

        /// <inheritdoc/>
        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject? container)
        {
            // We don't actually return a DataTemplate - we'll use a different approach
            // Instead, we'll create a custom control that builds the UI dynamically
            return null;
        }

        /// <summary>
        /// Creates a UI panel with dynamic controls for the given effect.
        /// </summary>
        /// <param name="effect">The effect to create UI for.</param>
        /// <param name="onChanged">Optional callback invoked when any effect property changes.</param>
        /// <returns>A StackPanel containing all the effect's option controls.</returns>
        public static FrameworkElement CreateEffectPanel(LayerEffectBase effect, System.Action? onChanged = null)
        {
            var panel = new StackPanel
            {
                Spacing = 8,
                Padding = new Thickness(6)
            };

            // Look up the effect's registration
            var registration = !string.IsNullOrEmpty(effect.EffectId)
                ? EffectRegistry.Shared.GetById(effect.EffectId)
                : null;

            if (registration == null)
            {
                // Fallback: try to find by type name (for effects not created via registry)
                registration = FindRegistrationByType(effect);
            }

            if (registration == null)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No editable settings for this effect.",
                    Opacity = 0.7
                });
                return panel;
            }

            // Get options and create controls
            var options = registration.GetOptions(effect).OrderBy(o => o.Order).ToList();

            if (options.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No editable settings for this effect.",
                    Opacity = 0.7
                });
                return panel;
            }

            foreach (var option in options)
            {
                var control = ToolOptionFactory.CreateControl(
                    option,
                    onEditStart: null,
                    onEditEnd: onChanged);

                if (control != null)
                {
                    panel.Children.Add(control);
                }
            }

            return panel;
        }

        /// <summary>
        /// Attempts to find a registration by matching the effect's type.
        /// </summary>
        private static IEffectRegistration? FindRegistrationByType(LayerEffectBase effect)
        {
            var effectType = effect.GetType();

            foreach (var reg in EffectRegistry.Shared.GetAll())
            {
                // Create a temporary instance to check the type
                var instance = reg.CreateInstance();
                if (instance.GetType() == effectType)
                {
                    // Set the EffectId on the original effect for future lookups
                    effect.EffectId = reg.Id;
                    return reg;
                }
            }

            return null;
        }
    }
}
