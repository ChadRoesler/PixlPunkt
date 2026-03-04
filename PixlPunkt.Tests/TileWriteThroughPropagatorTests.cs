namespace PixlPunkt.Tests;

using PixlPunkt.Core.Tile;

[TestFixture]
public sealed class TileWriteThroughPropagatorTests
{
    [Test]
    public void Apply_SingleMappedPosition_UpdatesTileDefinitionAndLayer()
    {
        const uint baseColor = 0xFF223344;
        const uint paintedColor = 0xFFAA5500;

        var tileSet = new TileSet(2, 2);
        int tileId = tileSet.AddTile(CreateSolidPixels(2, 2, baseColor));

        var mapping = new TileMapping(1, 1);
        mapping.SetTileId(0, 0, tileId);

        var layer = CreateSolidPixels(2, 2, baseColor);
        SetPixel(layer, 2, 1, 1, paintedColor);

        bool changed = TileWriteThroughPropagator.Apply(
            layer,
            2,
            2,
            mapping,
            tileSet,
            1,
            1,
            1,
            1);

        changed.Should().BeTrue();
        ReadPixel(tileSet.GetTilePixels(tileId)!, 2, 1, 1).Should().Be(paintedColor);
        ReadPixel(layer, 2, 1, 1).Should().Be(paintedColor);
    }

    [Test]
    public void Apply_TileMappedChange_PropagatesToAllInstances()
    {
        const uint baseColor = 0xFF0A0B0C;
        const uint paintedColor = 0xFF1122EE;

        var tileSet = new TileSet(2, 2);
        int tileId = tileSet.AddTile(CreateSolidPixels(2, 2, baseColor));

        var mapping = new TileMapping(2, 1);
        mapping.SetTileId(0, 0, tileId);
        mapping.SetTileId(1, 0, tileId);

        var layer = CreateSolidPixels(4, 2, 0x00000000);
        WriteTile(layer, 4, 0, 0, 2, 2, tileSet.GetTilePixels(tileId)!);
        WriteTile(layer, 4, 2, 0, 2, 2, tileSet.GetTilePixels(tileId)!);

        // Paint left instance at local (0,1). Right instance should mirror this after propagation.
        SetPixel(layer, 4, 0, 1, paintedColor);

        bool changed = TileWriteThroughPropagator.Apply(
            layer,
            4,
            2,
            mapping,
            tileSet,
            0,
            1,
            0,
            1);

        changed.Should().BeTrue();
        ReadPixel(tileSet.GetTilePixels(tileId)!, 2, 0, 1).Should().Be(paintedColor);
        ReadPixel(layer, 4, 0, 1).Should().Be(paintedColor);
        ReadPixel(layer, 4, 2, 1).Should().Be(paintedColor);
        ReadPixel(layer, 4, 1, 0).Should().Be(baseColor);
        ReadPixel(layer, 4, 3, 0).Should().Be(baseColor);
    }

    [Test]
    public void Apply_UnmappedBounds_DoesNotModifyTiles()
    {
        const uint baseColor = 0xFF555555;
        const uint paintedColor = 0xFF77AA33;

        var tileSet = new TileSet(2, 2);
        int tileId = tileSet.AddTile(CreateSolidPixels(2, 2, baseColor));
        var originalTile = (byte[])tileSet.GetTilePixels(tileId)!.Clone();

        var mapping = new TileMapping(1, 1);
        mapping.SetTileId(0, 0, tileId);

        var layer = CreateSolidPixels(4, 4, 0x00000000);
        WriteTile(layer, 4, 0, 0, 2, 2, tileSet.GetTilePixels(tileId)!);
        SetPixel(layer, 4, 3, 3, paintedColor);

        bool changed = TileWriteThroughPropagator.Apply(
            layer,
            4,
            4,
            mapping,
            tileSet,
            3,
            3,
            3,
            3);

        changed.Should().BeFalse();
        tileSet.GetTilePixels(tileId)!.Should().Equal(originalTile);
        ReadPixel(layer, 4, 3, 3).Should().Be(paintedColor);
    }

    private static byte[] CreateSolidPixels(int width, int height, uint color)
    {
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            WriteColor(pixels, i, color);
        }
        return pixels;
    }

    private static void WriteTile(byte[] layer, int layerWidth, int x, int y, int tileWidth, int tileHeight, byte[] tilePixels)
    {
        for (int py = 0; py < tileHeight; py++)
        {
            for (int px = 0; px < tileWidth; px++)
            {
                int src = (py * tileWidth + px) * 4;
                int dst = (((y + py) * layerWidth) + (x + px)) * 4;
                layer[dst + 0] = tilePixels[src + 0];
                layer[dst + 1] = tilePixels[src + 1];
                layer[dst + 2] = tilePixels[src + 2];
                layer[dst + 3] = tilePixels[src + 3];
            }
        }
    }

    private static void SetPixel(byte[] pixels, int width, int x, int y, uint color)
    {
        WriteColor(pixels, ((y * width) + x) * 4, color);
    }

    private static uint ReadPixel(byte[] pixels, int width, int x, int y)
    {
        int idx = ((y * width) + x) * 4;
        return (uint)(pixels[idx] |
                      (pixels[idx + 1] << 8) |
                      (pixels[idx + 2] << 16) |
                      (pixels[idx + 3] << 24));
    }

    private static void WriteColor(byte[] pixels, int idx, uint color)
    {
        pixels[idx + 0] = (byte)(color & 0xFF);
        pixels[idx + 1] = (byte)((color >> 8) & 0xFF);
        pixels[idx + 2] = (byte)((color >> 16) & 0xFF);
        pixels[idx + 3] = (byte)((color >> 24) & 0xFF);
    }
}
