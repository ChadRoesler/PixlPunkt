using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document.Layer;

namespace PixlPunkt.UI.Layers
{
    /// <summary>
    /// Selects different data templates for layers vs folders in the layers panel ListView.
    /// </summary>
    public sealed class LayerItemTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Template for RasterLayer items (with preview thumbnail).
        /// </summary>
        public DataTemplate? RasterLayerTemplate { get; set; }

        /// <summary>
        /// Template for LayerFolder items (folder icon, expand/collapse chevron).
        /// </summary>
        public DataTemplate? FolderTemplate { get; set; }

        /// <summary>
        /// Template for ReferenceLayer items (reference image with transform controls).
        /// </summary>
        public DataTemplate? ReferenceLayerTemplate { get; set; }

        /// <summary>
        /// Template for RootDropZoneFooterItem (drag-to-root footer for list).
        /// </summary>
        public DataTemplate? RootDropZoneFooterTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item)
        {
            return item switch
            {
                RootDropZoneFooterItem => RootDropZoneFooterTemplate,
                ReferenceLayer => ReferenceLayerTemplate,
                RasterLayer => RasterLayerTemplate,
                LayerFolder => FolderTemplate,
                _ => base.SelectTemplateCore(item)
            };
        }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
            => SelectTemplateCore(item);
    }
}
