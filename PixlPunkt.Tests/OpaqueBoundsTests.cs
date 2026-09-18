namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Imaging;

/// <summary>Auto-crop bounds: the tight box around non-transparent pixels.</summary>
[TestFixture]
public class OpaqueBoundsTests
{
    private static byte[] Blank(int w, int h) => new byte[w * h * 4];
    private static void Set(byte[] b, int w, int x, int y, byte a = 255) { b[(y * w + x) * 4 + 3] = a; }

    [Test]
    public void FullyTransparent_ReturnsNull()
    {
        PixelRectOps.OpaqueBounds(Blank(8, 8), 8, 8).Should().BeNull();
    }

    [Test]
    public void SinglePixel_IsOneByOne()
    {
        var b = Blank(8, 8); Set(b, 8, 5, 2);
        var r = PixelRectOps.OpaqueBounds(b, 8, 8)!.Value;
        (r.X, r.Y, r.Width, r.Height).Should().Be((5, 2, 1, 1));
    }

    [Test]
    public void ScatteredPixels_TightBox_AndCropKeepsThem()
    {
        var b = Blank(16, 16);
        Set(b, 16, 3, 4); Set(b, 16, 10, 4, 1); Set(b, 16, 6, 12);
        var r = PixelRectOps.OpaqueBounds(b, 16, 16)!.Value;
        (r.X, r.Y, r.Width, r.Height).Should().Be((3, 4, 8, 9));

        var cropped = PixelRectOps.CopyRect(b, 16, 16, r);
        cropped.Length.Should().Be(8 * 9 * 4);
        cropped[(0 * 8 + 0) * 4 + 3].Should().Be(255, "(3,4) lands at the crop origin");
        cropped[(0 * 8 + 7) * 4 + 3].Should().Be(1, "faint alpha still counts as content");
        cropped[(8 * 8 + 3) * 4 + 3].Should().Be(255);
    }
}
