using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PixlPunkt.Uno.UI.Ascii
{
    /// <summary>
    /// Selects the appropriate data template for glyph set folders vs items.
    /// </summary>
    public sealed class GlyphSetItemTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Template for folder items.
        /// </summary>
        public DataTemplate? FolderTemplate { get; set; }

        /// <summary>
        /// Template for glyph set items.
        /// </summary>
        public DataTemplate? ItemTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item)
        {
            return item switch
            {
                GlyphSetFolder => FolderTemplate,
                GlyphSetItem => ItemTemplate,
                _ => base.SelectTemplateCore(item)
            };
        }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}
