namespace PixlPunkt.Core.Canvas
{
    // CanvasTemplate used for dialog/template registry. Mutable properties so XAML loader can assign values.
    public class CanvasTemplate
    {
        public string Name { get; set; } = string.Empty;
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public int TileCountX { get; set; }
        public int TileCountY { get; set; }
        public bool IsBuiltIn { get; set; }

        public CanvasTemplate() { }

        public CanvasTemplate(string name, int tileWidth, int tileHeight, int tileCountX, int tileCountY, bool isBuiltIn = false)
        {
            Name = name;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileCountX = tileCountX;
            TileCountY = tileCountY;
            IsBuiltIn = isBuiltIn;
        }
    }
}
