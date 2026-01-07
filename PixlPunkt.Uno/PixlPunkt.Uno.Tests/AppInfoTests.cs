namespace PixlPunkt.Uno.Tests;

/// <summary>
/// Basic unit tests for app configuration and core functionality.
/// </summary>
public class AppInfoTests
{
    [SetUp]
    public void Setup()
    {
    }

    /// <summary>
    /// Verifies that the test framework is working correctly.
    /// </summary>
    [Test]
    public void TestFramework_IsWorking()
    {
        // Simple assertion to verify test framework is configured correctly
        true.Should().BeTrue();
        "PixlPunkt".Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Verifies basic math operations work (sanity check).
    /// </summary>
    [Test]
    public void BasicMath_IsWorking()
    {
        var result = 2 + 2;
        result.Should().Be(4);
    }
}
