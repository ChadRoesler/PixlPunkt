using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document.Layer;

namespace PixlPunkt.UI.Helpers
{
    /// <summary>
    /// Selects <see cref="DataTemplate"/> based on layer type for list/grid item rendering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NodeTemplateSelector enables different visual representations for <see cref="RasterLayer"/> and
    /// <see cref="LayerFolder"/> items in lists, grids, or item controls. Commonly used in layer panels
    /// to show distinct UI for leaf nodes (layers) vs. containers (folders).
    /// </para>
    /// <para><strong>Usage Pattern:</strong></para>
    /// <code>
    /// &lt;local:NodeTemplateSelector x:Key="LayerNodeSelector"
    ///     LayerTemplate="{StaticResource RasterLayerTemplate}"
    ///     FolderTemplate="{StaticResource LayerFolderTemplate}" /&gt;
    /// 
    /// &lt;ListView ItemTemplateSelector="{StaticResource LayerNodeSelector}" /&gt;
    /// </code>
    /// </remarks>
    /// <seealso cref="LayerTreeTemplateSelector"/>
    public sealed class NodeTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Gets or sets the template for <see cref="RasterLayer"/> items.
        /// </summary>
        public DataTemplate? LayerTemplate { get; set; }

        /// <summary>
        /// Gets or sets the template for <see cref="LayerFolder"/> items.
        /// </summary>
        public DataTemplate? FolderTemplate { get; set; }

        /// <summary>
        /// Selects the appropriate template based on item type.
        /// </summary>
        /// <param name="item">Layer object to template.</param>
        /// <returns>LayerTemplate for RasterLayer, FolderTemplate for LayerFolder, or base template otherwise.</returns>
        protected override DataTemplate SelectTemplateCore(object item)
            => item switch
            {
                RasterLayer => LayerTemplate!,
                LayerFolder => FolderTemplate!,
                _ => base.SelectTemplateCore(item)
            };

        /// <summary>
        /// Selects template (delegates to single-parameter overload).
        /// </summary>
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
            => SelectTemplateCore(item);
    }
}
