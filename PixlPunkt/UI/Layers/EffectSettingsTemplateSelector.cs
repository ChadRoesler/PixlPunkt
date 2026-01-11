using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Compositing.Effects;

namespace PixlPunkt.UI.Layers
{
    /// <summary>
    /// Selects <see cref="DataTemplate"/> for effect settings panels based on effect type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EffectSettingsTemplateSelector provides custom UI templates for editing effect parameters.
    /// Each effect type can have a dedicated settings template with specialized controls for its
    /// parameters (e.g., thickness slider for ScanLinesEffect, offset controls for DropShadowEffect).
    /// </para>
    /// <para><strong>DataContext Handling:</strong></para>
    /// <para>
    /// WinUI sometimes passes null for the item parameter. When this occurs, the selector falls back
    /// to reading container.DataContext to access the actual effect object. Debug logging is included
    /// to troubleshoot binding issues.
    /// </para>
    /// <para><strong>Current Implementation:</strong></para>
    /// <para>
    /// Only <see cref="ScanLinesEffect"/> has explicit template support via <see cref="ScanLinesTemplate"/>.
    /// Other effects return null (no custom UI), relying on default property grid or base template.
    /// </para>
    /// <para><strong>Extensibility:</strong></para>
    /// <para>
    /// Add properties for each effect type requiring custom settings UI:
    /// <code>
    /// public DataTemplate? ChromaticAberrationTemplate { get; set; }
    /// public DataTemplate? DropShadowTemplate { get; set; }
    /// </code>
    /// Then add cases to SelectTemplateCore.
    /// </para>
    /// </remarks>
    /// <seealso cref="ScanLinesEffect"/>
    public sealed partial class EffectSettingsTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Gets or sets the settings template for <see cref="ScanLinesEffect"/>.
        /// </summary>
        public DataTemplate? ScanLinesTemplate { get; set; }

        /// <summary>
        /// Selects template (delegates to two-parameter overload).
        /// </summary>
        protected override DataTemplate SelectTemplateCore(object item)
            => SelectTemplateCore(item, null);

        /// <summary>
        /// Selects settings template based on effect type.
        /// </summary>
        /// <param name="item">Effect instance to template (may be null).</param>
        /// <param name="container">Container element (fallback for DataContext if item is null).</param>
        /// <returns>Specialized settings template, or null to use default/base template.</returns>
        /// <remarks>
        /// Debug logging writes selected type name to help diagnose binding/template issues during development.
        /// </remarks>
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject? container)
        {
            // WinUI sometimes passes null for 'item' here – the real value is on the container's DataContext
            var data = item;

            if (data is null && container is FrameworkElement fe)
                data = fe.DataContext;

            if (data is ScanLinesEffect && ScanLinesTemplate is not null)
                return ScanLinesTemplate;

            // No known template → let the default kick in (or render nothing)
            return null!;
        }
    }
}
