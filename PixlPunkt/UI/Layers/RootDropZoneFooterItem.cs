namespace PixlPunkt.UI.Layers
{
    /// <summary>
    /// Marker class for the root drop zone footer item in the layers list.
    /// This is a virtual UI-only item that appears at the bottom of the layers ListView
    /// and is never saved to the document.
    /// </summary>
    public sealed class RootDropZoneFooterItem
    {
        public static readonly RootDropZoneFooterItem Instance = new();

        private RootDropZoneFooterItem() { }
    }
}
