namespace PixlPunkt.Tests;

using System.IO;
using System.Reflection;
using PixlPunkt.Core.Document;

[TestFixture]
public sealed class DocumentIOVoxelLightingTests
{
    [Test]
    public void VoxelWorkspaceState_SerializesLightingFields_RoundTrip()
    {
        var writeMethod = typeof(DocumentIO).GetMethod(
            "WriteVoxelWorkspaceState",
            BindingFlags.NonPublic | BindingFlags.Static);
        var readMethod = typeof(DocumentIO).GetMethod(
            "ReadVoxelWorkspaceState",
            BindingFlags.NonPublic | BindingFlags.Static);

        writeMethod.Should().NotBeNull();
        readMethod.Should().NotBeNull();

        var source = new VoxelWorkspaceDocumentState
        {
            HasState = true,
            LightingEnabled = true,
            LightPosX = 6.5f,
            LightPosY = 12.25f,
            LightPosZ = -3.75f,
            LightColorBgra = 0xFFBBDDFF,
            ShadowColorBgra = 0xA0203040,
            LightShadowStrength = 0.63f,
            LightIntensity = 2.35f,
            LightFalloff = 0.22f,
            LightCastShadows = true,
            ToolOptionsSectionExpanded = false,
            FaceMappingSectionExpanded = true,
            DisplaySectionExpanded = false,
            VoxelEditSectionExpanded = true,
            ActionsSectionExpanded = false,
        };

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writeMethod!.Invoke(null, [bw, source]);
        }

        ms.Position = 0;
        var roundTrip = new VoxelWorkspaceDocumentState();
        using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            readMethod!.Invoke(null, [br, roundTrip, 21]);
        }

        roundTrip.HasState.Should().BeTrue();
        roundTrip.LightingEnabled.Should().BeTrue();
        roundTrip.LightPosX.Should().BeApproximately(6.5f, 0.0001f);
        roundTrip.LightPosY.Should().BeApproximately(12.25f, 0.0001f);
        roundTrip.LightPosZ.Should().BeApproximately(-3.75f, 0.0001f);
        roundTrip.LightColorBgra.Should().Be(0xFFBBDDFF);
        roundTrip.ShadowColorBgra.Should().Be(0xA0203040);
        roundTrip.LightShadowStrength.Should().BeApproximately(0.63f, 0.0001f);
        roundTrip.LightIntensity.Should().BeApproximately(2.35f, 0.0001f);
        roundTrip.LightFalloff.Should().BeApproximately(0.22f, 0.0001f);
        roundTrip.LightCastShadows.Should().BeTrue();
        roundTrip.ToolOptionsSectionExpanded.Should().BeFalse();
        roundTrip.FaceMappingSectionExpanded.Should().BeTrue();
        roundTrip.DisplaySectionExpanded.Should().BeFalse();
        roundTrip.VoxelEditSectionExpanded.Should().BeTrue();
        roundTrip.ActionsSectionExpanded.Should().BeFalse();
    }
}
