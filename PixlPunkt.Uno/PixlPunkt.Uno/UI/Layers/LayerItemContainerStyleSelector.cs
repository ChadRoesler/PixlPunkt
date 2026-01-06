using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Document.Layer;

namespace PixlPunkt.Uno.UI.Layers
{
    public sealed class LayerItemContainerStyleSelector : StyleSelector
    {
        public Style? RasterStyle { get; set; }
        public Style? FolderStyle { get; set; }
        public Style? ReferenceStyle { get; set; }
        public Style? RootDropZoneFooterStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            return item switch
            {
                RootDropZoneFooterItem => RootDropZoneFooterStyle ?? base.SelectStyleCore(item, container),
                ReferenceLayer => ReferenceStyle ?? RasterStyle ?? base.SelectStyleCore(item, container),
                LayerFolder => FolderStyle ?? base.SelectStyleCore(item, container),
                _ => RasterStyle ?? base.SelectStyleCore(item, container)
            };
        }
    }
}
