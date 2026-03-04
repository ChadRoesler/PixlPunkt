namespace PixlPunkt.Tests;

using System.IO;
using System.Reflection;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Voxel;

[TestFixture]
public sealed class DocumentIOVoxelModelTests
{
    [Test]
    public void VoxelModelState_SerializesRoundTrip()
    {
        var writeMethod = typeof(DocumentIO).GetMethod(
            "WriteVoxelModelState",
            BindingFlags.NonPublic | BindingFlags.Static);
        var readMethod = typeof(DocumentIO).GetMethod(
            "ReadVoxelModelState",
            BindingFlags.NonPublic | BindingFlags.Static);

        writeMethod.Should().NotBeNull();
        readMethod.Should().NotBeNull();

        var source = new VoxelModelDocumentState();
        source.Initialize(2, 2, 2);
        source.SourceKind = VoxelModelSourceKind.Hybrid;
        source.DirtyFromSource = true;
        source.LastGeneratedUtcTicks = 123456789;

        source.SetOccupied(1, 0, 1, true);
        source.SetFaceColorBgra(1, 0, 1, Face.Front, 0xFF112233);
        source.SetFaceColorBgra(1, 0, 1, Face.Top, 0xFF445566);

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writeMethod!.Invoke(null, [bw, source]);
        }

        ms.Position = 0;
        var roundTrip = new VoxelModelDocumentState();
        using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            readMethod!.Invoke(null, [br, roundTrip]);
        }

        roundTrip.HasModel.Should().BeTrue();
        roundTrip.Width.Should().Be(2);
        roundTrip.Height.Should().Be(2);
        roundTrip.Depth.Should().Be(2);
        roundTrip.SourceKind.Should().Be(VoxelModelSourceKind.Hybrid);
        roundTrip.DirtyFromSource.Should().BeTrue();
        roundTrip.LastGeneratedUtcTicks.Should().Be(123456789);

        roundTrip.IsOccupied(1, 0, 1).Should().BeTrue();
        roundTrip.GetFaceColorBgra(1, 0, 1, Face.Front).Should().Be(0xFF112233);
        roundTrip.GetFaceColorBgra(1, 0, 1, Face.Top).Should().Be(0xFF445566);

        roundTrip.IsStorageValid.Should().BeTrue();
    }
}
