namespace PixlPunkt.Tests;

using System;
using System.IO;
using FluentAssertions;
using PixlPunkt.Core.Document;

/// <summary>
/// Reads the sample font that lives in the repository, whatever version it was last saved at.
/// Splitting the em from the drawing cell appended four fields to the format, so a file written
/// before that has to keep loading. A round-trip test cannot show this, because it always writes
/// the current version; only a real file on disk can. The sample is the user's working font and
/// gets re-saved, so the version is read from its header rather than assumed.
/// </summary>
[TestFixture]
public class FontLegacyFormatTests
{
    /// <summary>Walks up from the test binary to the repository root, or null outside a checkout.</summary>
    private static string? FindSampleFont()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "docs", "TestFont.pxpf");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>The format version in the file header, after the four-byte magic.</summary>
    private static int VersionOf(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        reader.ReadBytes(4);
        return reader.ReadInt32();
    }

    /// <summary>The version that introduced the em box, and with it the four appended fields.</summary>
    private const int EmBoxVersion = 23;

    [Test]
    public void TheSampleFontLoads_AndItsEmBoxFitsInsideItsCell()
    {
        string? path = FindSampleFont();
        if (path is null)
        {
            Assert.Ignore("docs/TestFont.pxpf is not present; nothing to check against.");
            return;
        }

        var doc = DocumentIO.Load(path);
        var em = FontMetricsOps.EmBox(doc);

        doc.FontState.HasState.Should().BeTrue("the sample is a font document");
        doc.FontState.Glyphs.Should().NotBeEmpty();

        em.Width.Should().BePositive();
        em.Height.Should().BePositive();
        (em.X + em.Width).Should().BeLessThanOrEqualTo(doc.TileSize.Width, "the em cannot spill out of its cell");
        (em.Y + em.Height).Should().BeLessThanOrEqualTo(doc.TileSize.Height);
        FontMetricsOps.EmHeightOf(doc).Should().Be(em.Height);

        if (VersionOf(path) < EmBoxVersion)
        {
            // Nothing wrote the em fields, and zero is how the state says "the whole cell".
            doc.FontState.EmWidth.Should().Be(0);
            doc.FontState.EmHeight.Should().Be(0);
            em.Width.Should().Be(doc.TileSize.Width);
            em.Height.Should().Be(doc.TileSize.Height);
        }
    }

    [Test]
    public void ResavingAnOldFont_KeepsItsGlyphsAndSpacing()
    {
        string? path = FindSampleFont();
        if (path is null)
        {
            Assert.Ignore("docs/TestFont.pxpf is not present; nothing to check against.");
            return;
        }

        var original = DocumentIO.Load(path);
        using var stream = new MemoryStream(DocumentIO.SaveToBytes(original));
        var reloaded = DocumentIO.Load(stream);

        reloaded.FontState.Glyphs.Count.Should().Be(original.FontState.Glyphs.Count);
        reloaded.FontState.BaselineY.Should().Be(original.FontState.BaselineY);
        reloaded.FontState.ToplineY.Should().Be(original.FontState.ToplineY);
        reloaded.FontState.Monospace.Should().Be(original.FontState.Monospace);
        reloaded.TileSize.Should().Be(original.TileSize);
    }
}
