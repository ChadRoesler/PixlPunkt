namespace PixlPunkt.Tests;

using System.Text;
using PixlPunkt.Core.Export;

/// <summary>
/// Unit tests for <see cref="SvgExporter"/>.
/// Covers input validation, both export modes (Block/Monolith),
/// greedy meshing correctness, scaling, transparency, and file I/O.
/// </summary>
[TestFixture]
public class SvgExporterTests
{
    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>Creates a BGRA pixel buffer filled with a single color.</summary>
    private static byte[] MakeSolid(int w, int h, byte r, byte g, byte b, byte a = 255)
    {
        var buf = new byte[w * h * 4];
        for (int i = 0; i < buf.Length; i += 4)
        {
            buf[i + 0] = b;  // B
            buf[i + 1] = g;  // G
            buf[i + 2] = r;  // R
            buf[i + 3] = a;  // A
        }
        return buf;
    }

    /// <summary>Creates a fully transparent BGRA buffer.</summary>
    private static byte[] MakeTransparent(int w, int h) => new byte[w * h * 4];

    /// <summary>Sets a single pixel in a BGRA buffer.</summary>
    private static void SetPixel(byte[] buf, int width, int x, int y,
        byte r, byte g, byte b, byte a = 255)
    {
        int i = (y * width + x) * 4;
        buf[i + 0] = b;
        buf[i + 1] = g;
        buf[i + 2] = r;
        buf[i + 3] = a;
    }

    // ════════════════════════════════════════════════════════════════════
    // VALIDATION
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Export_NullPixels_ThrowsArgumentException()
    {
        var act = () => SvgExporter.Export(null!, 1, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Export_BufferTooShort_ThrowsArgumentException()
    {
        var act = () => SvgExporter.Export(new byte[4], 2, 2);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Export_ExceedsPixelCap_ThrowsArgumentException()
    {
        // 1001 x 1001 = 1_002_001 > 1_000_000 cap
        var act = () => SvgExporter.Export(new byte[1001 * 1001 * 4], 1001, 1001);
        act.Should().Throw<ArgumentException>().WithMessage("*safety cap*");
    }

    [Test]
    public void Export_InvalidMode_ThrowsArgumentOutOfRangeException()
    {
        var pixels = MakeSolid(1, 1, 255, 0, 0);
        var act = () => SvgExporter.Export(pixels, 1, 1, (SvgExportMode)99);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ════════════════════════════════════════════════════════════════════
    // SVG STRUCTURE
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Export_ContainsXmlDeclaration()
    {
        var svg = SvgExporter.Export(MakeSolid(1, 1, 255, 0, 0), 1, 1);
        svg.Should().StartWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    }

    [Test]
    public void Export_ContainsViewBox()
    {
        var svg = SvgExporter.Export(MakeSolid(4, 3, 255, 0, 0), 4, 3);
        svg.Should().Contain("viewBox=\"0 0 4 3\"");
    }

    [Test]
    public void Export_ContainsCrispEdges()
    {
        var svg = SvgExporter.Export(MakeSolid(1, 1, 255, 0, 0), 1, 1);
        svg.Should().Contain("shape-rendering=\"crispEdges\"");
    }

    [Test]
    public void Export_EndsWithSvgClosingTag()
    {
        var svg = SvgExporter.Export(MakeSolid(1, 1, 255, 0, 0), 1, 1);
        svg.TrimEnd().Should().EndWith("</svg>");
    }

    [TestCase(2, 8, 6, "viewBox=\"0 0 8 6\"", "width=\"8\" height=\"6\"")]
    [TestCase(3, 12, 9, "viewBox=\"0 0 12 9\"", "width=\"12\" height=\"9\"")]
    public void Export_Scale_AffectsViewBoxAndDimensions(int scale, int expectedW, int expectedH,
        string expectedViewBox, string expectedDims)
    {
        var svg = SvgExporter.Export(MakeSolid(4, 3, 255, 0, 0), 4, 3, SvgExportMode.Block, scale);
        svg.Should().Contain(expectedViewBox);
        svg.Should().Contain(expectedDims);
    }

    [Test]
    public void Export_ScaleBelowOne_ClampedToOne()
    {
        var svg = SvgExporter.Export(MakeSolid(2, 2, 255, 0, 0), 2, 2, SvgExportMode.Block, -5);
        svg.Should().Contain("viewBox=\"0 0 2 2\"");
    }

    // ════════════════════════════════════════════════════════════════════
    // BLOCK MODE
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Block_SingleOpaquePixel_EmitsOneRect()
    {
        var svg = SvgExporter.Export(MakeSolid(1, 1, 255, 0, 0), 1, 1, SvgExportMode.Block);

        svg.Should().Contain("<rect");
        svg.Should().Contain("fill=\"#ff0000\"");
        svg.Should().Contain("x=\"0\" y=\"0\"");
        svg.Should().Contain("width=\"1\" height=\"1\"");
        svg.Should().NotContain("opacity");
    }

    [Test]
    public void Block_TransparentPixel_EmitsNoRect()
    {
        var svg = SvgExporter.Export(MakeTransparent(1, 1), 1, 1, SvgExportMode.Block);

        svg.Should().NotContain("<rect");
    }

    [Test]
    public void Block_SemiTransparentPixel_EmitsOpacity()
    {
        var pixels = MakeSolid(1, 1, 0, 128, 255, 128);
        var svg = SvgExporter.Export(pixels, 1, 1, SvgExportMode.Block);

        svg.Should().Contain("fill=\"#0080ff\"");
        svg.Should().Contain("opacity=\"0.502\"");
    }

    [Test]
    public void Block_MultiplePixels_EmitsCorrectCount()
    {
        // 2x2 fully opaque → 4 rects
        var svg = SvgExporter.Export(MakeSolid(2, 2, 128, 128, 128), 2, 2, SvgExportMode.Block);

        var rectCount = CountOccurrences(svg, "<rect");
        rectCount.Should().Be(4);
    }

    [Test]
    public void Block_Scale_MultipliesPositionAndSize()
    {
        var svg = SvgExporter.Export(MakeSolid(1, 1, 255, 0, 0), 1, 1, SvgExportMode.Block, 4);

        svg.Should().Contain("x=\"0\" y=\"0\"");
        svg.Should().Contain("width=\"4\" height=\"4\"");
    }

    [Test]
    public void Block_MixedTransparency_OnlyEmitsVisiblePixels()
    {
        // 2x1: first pixel opaque red, second pixel transparent
        var pixels = new byte[2 * 1 * 4];
        SetPixel(pixels, 2, 0, 0, 255, 0, 0);  // opaque
        // pixel (1,0) remains all-zero = transparent

        var svg = SvgExporter.Export(pixels, 2, 1, SvgExportMode.Block);

        CountOccurrences(svg, "<rect").Should().Be(1);
        svg.Should().Contain("fill=\"#ff0000\"");
    }

    // ════════════════════════════════════════════════════════════════════
    // MONOLITH MODE — GREEDY MESHING
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Monolith_SinglePixel_EmitsOnePath()
    {
        var svg = SvgExporter.Export(MakeSolid(1, 1, 0, 255, 0), 1, 1, SvgExportMode.Monolith);

        svg.Should().Contain("<path");
        svg.Should().Contain("fill=\"#00ff00\"");
        svg.Should().NotContain("<rect");
    }

    [Test]
    public void Monolith_FullyTransparent_EmitsNoPaths()
    {
        var svg = SvgExporter.Export(MakeTransparent(4, 4), 4, 4, SvgExportMode.Monolith);

        svg.Should().NotContain("<path");
    }

    [Test]
    public void Monolith_SolidRect_MergesIntoSingleSubPath()
    {
        // 3x2 solid red → should merge into one rectangle in the path
        var svg = SvgExporter.Export(MakeSolid(3, 2, 255, 0, 0), 3, 2, SvgExportMode.Monolith);

        // One <path> element
        CountOccurrences(svg, "<path").Should().Be(1);

        // The path data should have exactly one M (moveto = one rect)
        var pathData = ExtractPathData(svg);
        CountOccurrences(pathData, "M").Should().Be(1);

        // Should describe a 3x2 rect at origin
        pathData.Should().Contain("M0 0h3v2h-3Z");
    }

    [Test]
    public void Monolith_TwoColors_EmitsTwoPaths()
    {
        // 2x1 image: red pixel, blue pixel
        var pixels = new byte[2 * 1 * 4];
        SetPixel(pixels, 2, 0, 0, 255, 0, 0);
        SetPixel(pixels, 2, 1, 0, 0, 0, 255);

        var svg = SvgExporter.Export(pixels, 2, 1, SvgExportMode.Monolith);

        CountOccurrences(svg, "<path").Should().Be(2);
        svg.Should().Contain("fill=\"#ff0000\"");
        svg.Should().Contain("fill=\"#0000ff\"");
    }

    [Test]
    public void Monolith_LShape_ProducesMultipleSubPaths()
    {
        // 3x3 L-shape in red:
        //  R . .
        //  R . .
        //  R R R
        var pixels = MakeTransparent(3, 3);
        SetPixel(pixels, 3, 0, 0, 255, 0, 0);  // (0,0)
        SetPixel(pixels, 3, 0, 1, 255, 0, 0);  // (0,1)
        SetPixel(pixels, 3, 0, 2, 255, 0, 0);  // (0,2)
        SetPixel(pixels, 3, 1, 2, 255, 0, 0);  // (1,2)
        SetPixel(pixels, 3, 2, 2, 255, 0, 0);  // (2,2)

        var svg = SvgExporter.Export(pixels, 3, 3, SvgExportMode.Monolith);

        // Should be one <path> (all same color) but with multiple M sub-paths
        // because the L can't be merged into a single rect
        CountOccurrences(svg, "<path").Should().Be(1);

        var pathData = ExtractPathData(svg);
        CountOccurrences(pathData, "M").Should().BeGreaterThanOrEqualTo(2);
    }

    [Test]
    public void Monolith_SemiTransparent_EmitsOpacity()
    {
        var pixels = MakeSolid(2, 2, 64, 128, 200, 100);
        var svg = SvgExporter.Export(pixels, 2, 2, SvgExportMode.Monolith);

        svg.Should().Contain("opacity=");
        svg.Should().Contain("fill=\"#4080c8\"");
    }

    [Test]
    public void Monolith_Scale_MultipliesPathCoordinates()
    {
        // 1x1 red pixel at scale=5 → M0 0h5v5h-5Z
        var svg = SvgExporter.Export(MakeSolid(1, 1, 255, 0, 0), 1, 1, SvgExportMode.Monolith, 5);

        var pathData = ExtractPathData(svg);
        pathData.Should().Contain("M0 0h5v5h-5Z");
    }

    [Test]
    public void Monolith_Checkerboard_DoesNotMergeAcrossColors()
    {
        // 2x2 checkerboard: red/blue alternating
        //  R B
        //  B R
        var pixels = MakeTransparent(2, 2);
        SetPixel(pixels, 2, 0, 0, 255, 0, 0);
        SetPixel(pixels, 2, 1, 0, 0, 0, 255);
        SetPixel(pixels, 2, 0, 1, 0, 0, 255);
        SetPixel(pixels, 2, 1, 1, 255, 0, 0);

        var svg = SvgExporter.Export(pixels, 2, 2, SvgExportMode.Monolith);

        // Two colors → two <path> elements
        CountOccurrences(svg, "<path").Should().Be(2);

        // Each color has 2 disjoint pixels → 2 sub-paths per <path>
        svg.Should().Contain("fill=\"#ff0000\"");
        svg.Should().Contain("fill=\"#0000ff\"");
    }

    // ════════════════════════════════════════════════════════════════════
    // GREEDY MESH COVERAGE (via Monolith output)
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Monolith_HorizontalStrip_MergesIntoOneWideRect()
    {
        // 5x1 solid red → single rect 5x1
        var svg = SvgExporter.Export(MakeSolid(5, 1, 255, 0, 0), 5, 1, SvgExportMode.Monolith);

        var pathData = ExtractPathData(svg);
        CountOccurrences(pathData, "M").Should().Be(1);
        pathData.Should().Contain("M0 0h5v1h-5Z");
    }

    [Test]
    public void Monolith_VerticalStrip_MergesIntoOneTallRect()
    {
        // 1x5 solid green → single rect 1x5
        var svg = SvgExporter.Export(MakeSolid(1, 5, 0, 255, 0), 1, 5, SvgExportMode.Monolith);

        var pathData = ExtractPathData(svg);
        CountOccurrences(pathData, "M").Should().Be(1);
        pathData.Should().Contain("M0 0h1v5h-1Z");
    }

    [Test]
    public void Monolith_TwoSeparateBlocks_ProducesTwoSubPaths()
    {
        // 5x1: [R R . R R] → two rects: (0,0,2,1) and (3,0,2,1)
        var pixels = MakeTransparent(5, 1);
        SetPixel(pixels, 5, 0, 0, 255, 0, 0);
        SetPixel(pixels, 5, 1, 0, 255, 0, 0);
        SetPixel(pixels, 5, 3, 0, 255, 0, 0);
        SetPixel(pixels, 5, 4, 0, 255, 0, 0);

        var svg = SvgExporter.Export(pixels, 5, 1, SvgExportMode.Monolith);

        var pathData = ExtractPathData(svg);
        CountOccurrences(pathData, "M").Should().Be(2);
    }

    // ════════════════════════════════════════════════════════════════════
    // FILE I/O & BYTE EXPORT
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void ExportToBytes_ReturnsUtf8EncodedSvg()
    {
        var pixels = MakeSolid(2, 2, 100, 200, 50);
        var bytes = SvgExporter.ExportToBytes(pixels, 2, 2);
        var text = Encoding.UTF8.GetString(bytes);

        text.Should().StartWith("<?xml");
        text.Should().Contain("<svg");
        text.Should().Contain("</svg>");
    }

    [Test]
    public void ExportToFile_CreatesFileWithValidSvg()
    {
        var dir = Path.Combine(Path.GetTempPath(), "PixlPunktTests_Svg");
        var path = Path.Combine(dir, "test_output.svg");

        try
        {
            var pixels = MakeSolid(4, 4, 255, 128, 0);
            SvgExporter.ExportToFile(pixels, 4, 4, path, SvgExportMode.Block, 2);

            File.Exists(path).Should().BeTrue();

            var content = File.ReadAllText(path);
            content.Should().StartWith("<?xml");
            content.Should().Contain("viewBox=\"0 0 8 8\"");
            content.Should().Contain("<rect");
            content.Should().Contain("</svg>");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Test]
    public void ExportToFile_CreatesDirectoryIfMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "PixlPunktTests_Svg", "nested", "deep");
        var path = Path.Combine(dir, "nested_test.svg");

        try
        {
            var pixels = MakeSolid(1, 1, 0, 0, 255);
            SvgExporter.ExportToFile(pixels, 1, 1, path);

            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            var root = Path.Combine(Path.GetTempPath(), "PixlPunktTests_Svg");
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // BOTH MODES — CONSISTENCY
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void BothModes_SamePixels_ProduceSameViewBox()
    {
        var pixels = MakeSolid(8, 6, 100, 50, 200);

        var blockSvg = SvgExporter.Export(pixels, 8, 6, SvgExportMode.Block);
        var monoSvg = SvgExporter.Export(pixels, 8, 6, SvgExportMode.Monolith);

        blockSvg.Should().Contain("viewBox=\"0 0 8 6\"");
        monoSvg.Should().Contain("viewBox=\"0 0 8 6\"");
    }

    [Test]
    public void BothModes_FullyTransparent_ProduceNoElements()
    {
        var pixels = MakeTransparent(4, 4);

        var blockSvg = SvgExporter.Export(pixels, 4, 4, SvgExportMode.Block);
        var monoSvg = SvgExporter.Export(pixels, 4, 4, SvgExportMode.Monolith);

        blockSvg.Should().NotContain("<rect");
        monoSvg.Should().NotContain("<path");
    }

    [Test]
    public void Monolith_ProducesSmallerOutput_ThanBlock_ForSolidImage()
    {
        // Monolith should merge a solid 8x8 into 1 path vs 64 rects
        var pixels = MakeSolid(8, 8, 255, 0, 0);

        var blockSvg = SvgExporter.Export(pixels, 8, 8, SvgExportMode.Block);
        var monoSvg = SvgExporter.Export(pixels, 8, 8, SvgExportMode.Monolith);

        monoSvg.Length.Should().BeLessThan(blockSvg.Length);
    }

    // ════════════════════════════════════════════════════════════════════
    // EDGE CASES
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Export_1x1_TransparentPixel_ProducesEmptySvg()
    {
        var svg = SvgExporter.Export(MakeTransparent(1, 1), 1, 1);
        svg.Should().Contain("<svg");
        svg.Should().Contain("</svg>");
        svg.Should().NotContain("<path");
        svg.Should().NotContain("<rect");
    }

    [Test]
    public void Export_ColorHexIsLowercase()
    {
        // RGB (171, 205, 239) → #abcdef
        var pixels = MakeSolid(1, 1, 0xAB, 0xCD, 0xEF);
        var svg = SvgExporter.Export(pixels, 1, 1, SvgExportMode.Block);

        svg.Should().Contain("fill=\"#abcdef\"");
    }

    // ════════════════════════════════════════════════════════════════════
    // STRING UTILITIES
    // ════════════════════════════════════════════════════════════════════

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }

    private static string ExtractPathData(string svg)
    {
        // Extract the first d="..." attribute from a <path> element
        const string marker = " d=\"";
        int start = svg.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return "";
        start += marker.Length;
        int end = svg.IndexOf('"', start);
        return end < 0 ? "" : svg[start..end];
    }
}
