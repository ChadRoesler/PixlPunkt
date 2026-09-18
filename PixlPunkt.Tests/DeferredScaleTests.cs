namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Selection;
using Windows.Graphics;

/// <summary>
/// Scale is never baked into the floating buffer before commit; the commit resamples the
/// original pixels once at the final scale.
/// </summary>
[TestFixture]
public class DeferredScaleTests
{
    private static CanvasDocument NewDoc() =>
        new("t", 64, 64, new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 4, Height = 4 });

    private static byte Alpha(RasterLayer l, int x, int y) => l.Surface.Pixels[(y * l.Surface.Width + x) * 4 + 3];
    private static byte Red(RasterLayer l, int x, int y) => l.Surface.Pixels[(y * l.Surface.Width + x) * 4 + 2];

    private static byte[] Checker2x2()
    {
        // (0,0) red, (1,0) transparent, (0,1) transparent, (1,1) red
        var b = new byte[2 * 2 * 4];
        b[0 * 4 + 2] = 255; b[0 * 4 + 3] = 255;
        b[3 * 4 + 2] = 255; b[3 * 4 + 3] = 255;
        return b;
    }

    [Test]
    public void Commit_AtLiveScale_ResamplesOnce()
    {
        var doc = NewDoc(); var l = doc.Layers[0];
        doc.History.Push(FloatingSelectionOps.Paste(doc, l, Checker2x2(), 2, 2, 10, 10));
        var f = doc.Floating!;
        f.Width.Should().Be(2, "the buffer is the original");

        f.ScaleX = 2.0; f.ScaleY = 2.0;               // what a scale drag leaves behind
        doc.History.Push(FloatingSelectionOps.Commit(doc)!);

        // 4x4 result centred on the float's centre (11,11): x 9..12, y 9..12
        Alpha(l, 9, 9).Should().Be(255); Alpha(l, 10, 10).Should().Be(255);
        Alpha(l, 11, 9).Should().Be(0);  Alpha(l, 12, 10).Should().Be(0);
        Alpha(l, 11, 11).Should().Be(255); Alpha(l, 12, 12).Should().Be(255);
        Alpha(l, 8, 8).Should().Be(0);  Alpha(l, 13, 13).Should().Be(0);
    }

    [Test]
    public void ScaleDownThenUp_CommitsOriginalPixels()
    {
        var doc = NewDoc(); var l = doc.Layers[0];
        doc.History.Push(FloatingSelectionOps.Paste(doc, l, Checker2x2(), 2, 2, 10, 10));
        var f = doc.Floating!;

        f.ScaleX = 0.5; f.ScaleY = 0.5;               // would have destroyed the checker if baked
        f.ScaleX *= 2.0; f.ScaleY *= 2.0;
        doc.History.Push(FloatingSelectionOps.Commit(doc)!);

        Red(l, 10, 10).Should().Be(255); Alpha(l, 11, 10).Should().Be(0);
        Alpha(l, 10, 11).Should().Be(0); Red(l, 11, 11).Should().Be(255);
    }
}
