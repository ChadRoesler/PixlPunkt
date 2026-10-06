namespace PixlPunkt.Tests;

using System.IO;
using FluentAssertions;
using PixlPunkt.Constants;
using PixlPunkt.Core.Document;
using Windows.Graphics;

/// <summary>
/// The `.pxpv` volumetric project: what marks a document as one, and how that survives a save.
/// </summary>
/// <remarks>
/// The distinction being protected here is between a document that *is* a volumetric project and
/// an ordinary document that merely has the voxel workspace open. They look similar from the
/// inside and only one of them should change its file extension.
/// </remarks>
[TestFixture]
public class VolumetricFormatTests
{
    private static CanvasDocument NewDoc() => new("v", 16, 16,
        new SizeInt32 { Width = 8, Height = 8 }, new SizeInt32 { Width = 2, Height = 2 });

    private static CanvasDocument RoundTrip(CanvasDocument doc)
    {
        using var stream = new MemoryStream(DocumentIO.SaveToBytes(doc));
        return DocumentIO.Load(stream);
    }

    // ── which extension a document claims ───────────────────────────

    [Test]
    public void AnOrdinaryDocumentSavesAsPxp()
    {
        DocumentIO.ExtensionFor(NewDoc()).Should().Be(FileExtensions.PixlPunktDocument);
    }

    [Test]
    public void AVolumetricProjectSavesAsPxpv()
    {
        var doc = NewDoc();
        doc.VolumetricState.HasState = true;

        DocumentIO.ExtensionFor(doc).Should().Be(FileExtensions.PixlPunktVolumetric);
    }

    [Test]
    public void UsingTheVoxelWorkspaceDoesNotMakeADocumentVolumetric()
    {
        // The whole point of a separate marker. Opening the voxel preview on an ordinary drawing
        // must not quietly change what it saves as.
        var doc = NewDoc();
        doc.VoxelWorkspace.HasState = true;

        doc.VolumetricState.HasState.Should().BeFalse();
        DocumentIO.ExtensionFor(doc).Should().Be(FileExtensions.PixlPunktDocument);
    }

    [Test]
    public void AFontIsStillAFontEvenWithVolumetricStateSet()
    {
        // Nonsense in practice, but the order has to be decided rather than left to chance.
        var doc = NewDoc();
        doc.FontState.HasState = true;
        doc.VolumetricState.HasState = true;

        DocumentIO.ExtensionFor(doc).Should().Be(FileExtensions.PixlPunktFont);
    }

    [Test]
    public void EveryNativeExtensionIsRecognised()
    {
        DocumentIO.IsNativeExtension(FileExtensions.PixlPunktDocument).Should().BeTrue();
        DocumentIO.IsNativeExtension(FileExtensions.PixlPunktFont).Should().BeTrue();
        DocumentIO.IsNativeExtension(FileExtensions.PixlPunktVolumetric).Should().BeTrue();
        DocumentIO.IsNativeExtension(".PXPV").Should().BeTrue("extensions are not case sensitive");
        DocumentIO.IsNativeExtension(".png").Should().BeFalse();
        DocumentIO.IsNativeExtension(null).Should().BeFalse();
    }

    // ── surviving a save ────────────────────────────────────────────

    [Test]
    public void TheProjectMarkerSurvivesASave()
    {
        var doc = NewDoc();
        doc.VolumetricState.HasState = true;
        doc.VolumetricState.ProjectName = "Dungeon";

        var loaded = RoundTrip(doc);

        loaded.VolumetricState.HasState.Should().BeTrue();
        loaded.VolumetricState.ProjectName.Should().Be("Dungeon");
        DocumentIO.ExtensionFor(loaded).Should().Be(FileExtensions.PixlPunktVolumetric);
    }

    [Test]
    public void TheKindSurvivesASave()
    {
        var doc = NewDoc();
        doc.VolumetricState.HasState = true;
        doc.VolumetricState.Kind = VolumetricKind.Geometry;

        RoundTrip(doc).VolumetricState.Kind.Should().Be(VolumetricKind.Geometry);
    }

    [Test]
    public void AnOrdinaryDocumentRoundTripsWithNoProjectMarker()
    {
        var loaded = RoundTrip(NewDoc());

        loaded.VolumetricState.HasState.Should().BeFalse();
        loaded.VolumetricState.ProjectName.Should().BeEmpty();
        loaded.VolumetricState.Kind.Should().Be(VolumetricKind.Voxel);
    }

    [Test]
    public void AFontDocumentIsUnaffectedByTheNewBlock()
    {
        var doc = NewDoc();
        doc.FontState.HasState = true;
        doc.FontState.FamilyName = "Probe";
        doc.FontState.GetOrAdd('A').CellIndex = 0;

        var loaded = RoundTrip(doc);

        loaded.FontState.HasState.Should().BeTrue();
        loaded.FontState.FamilyName.Should().Be("Probe");
        loaded.FontState.Glyphs.Should().ContainKey('A');
        loaded.VolumetricState.HasState.Should().BeFalse();
    }

    // ── the state itself ────────────────────────────────────────────

    [Test]
    public void TheDefaultKindIsVoxel()
    {
        // Voxels are the first thing the format describes, not the only thing it may describe.
        new VolumetricDocumentState().Kind.Should().Be(VolumetricKind.Voxel);
    }

    [Test]
    public void StateCanBeCopiedAndCloned()
    {
        var source = new VolumetricDocumentState
        {
            HasState = true,
            ProjectName = "Tower",
            Kind = VolumetricKind.Geometry,
        };

        var clone = source.Clone();
        clone.Should().BeEquivalentTo(source);

        var target = new VolumetricDocumentState();
        target.CopyFrom(source);
        target.Should().BeEquivalentTo(source);
    }
}
