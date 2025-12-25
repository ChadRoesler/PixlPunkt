using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document.Layer;

namespace PixlPunkt.UI.Helpers
{
    /// <summary>
    /// Selects <see cref="DataTemplate"/> for <see cref="TreeView"/> items based on layer type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LayerTreeTemplateSelector handles TreeView's specific data binding where items are wrapped in
    /// <see cref="TreeViewNode"/>. Unwraps the node content before selecting template, enabling
    /// hierarchical layer panel UI with folder/layer distinction.
    /// </para>
    /// <para><strong>TreeView Handling:</strong></para>
    /// <para>
    /// When using TreeView with TreeViewNode binding, the item parameter is TreeViewNode.
    /// This selector extracts TreeViewNode.Content to access the actual layer object before
    /// template selection.
    /// </para>
    /// <para><strong>Usage Pattern:</strong></para>
    /// <code>
    /// &lt;local:LayerTreeTemplateSelector x:Key="LayerTreeSelector"
    ///     LayerTemplate="{StaticResource RasterLayerTreeTemplate}"
    ///     FolderTemplate="{StaticResource LayerFolderTreeTemplate}" /&gt;
    /// 
    /// &lt;TreeView ItemTemplateSelector="{StaticResource LayerTreeSelector}" /&gt;
    /// </code>
    /// </remarks>
    /// <seealso cref="NodeTemplateSelector"/>
    /// <seealso cref="LayerListItem"/>
    public sealed class LayerTreeTemplateSelector : DataTemplateSelector
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
        /// Selects template after unwrapping TreeViewNode content.
        /// </summary>
        /// <param name="item">Item or TreeViewNode containing layer.</param>
        /// <param name="container">Container (not used).</param>
        /// <returns>
        /// LayerTemplate for RasterLayer, FolderTemplate for LayerFolder, or LayerTemplate as safe default.
        /// </returns>
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            // When using TreeViewNode mode, the item is a TreeViewNode; we want its Content.
            var content = (item as TreeViewNode)?.Content ?? item;

            return content switch
            {
                RasterLayer => LayerTemplate!,
                LayerFolder => FolderTemplate!,
                _ => LayerTemplate!   // safe default
            };
        }

        /// <summary>
        /// Delegates to two-parameter overload.
        /// </summary>
        protected override DataTemplate SelectTemplateCore(object item)
            => SelectTemplateCore(item, null!);
    }
}
