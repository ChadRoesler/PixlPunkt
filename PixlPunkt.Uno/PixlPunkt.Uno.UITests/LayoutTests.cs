namespace PixlPunkt.Uno.UITests;

/// <summary>
/// Tests for layout and responsive behavior across platforms.
/// </summary>
[TestFixture]
public class LayoutTests : TestBase
{
    /// <summary>
    /// Verifies the three-column layout is present (left sidebar, center, right sidebar).
    /// </summary>
    [Test]
    public async Task Layout_ThreeColumnLayoutPresent()
    {
        await Task.Delay(3000);

        // Verify left sidebar (ToolRail)
        Query toolRail = q => q.All().Marked("ToolRail");
        App.WaitForElement(toolRail, timeout: TimeSpan.FromSeconds(30));

        // Verify center area (DocsTab)
        Query docsTab = q => q.All().Marked("DocsTab");
        App.WaitForElement(docsTab, timeout: TimeSpan.FromSeconds(30));

        // Verify right sidebar
        Query rightSidebar = q => q.All().Marked("RightSidebar");
        App.WaitForElement(rightSidebar, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("ThreeColumnLayout");
    }

    /// <summary>
    /// Verifies the title bar area is present (Windows-specific, may vary on other platforms).
    /// </summary>
    [Test]
    public async Task Layout_TitleBarPresent()
    {
        await Task.Delay(3000);

        Query titleBarRoot = q => q.All().Marked("TitleBarRoot");
        App.WaitForElement(titleBarRoot, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("TitleBarPresent");
    }

    /// <summary>
    /// Verifies the center logo/splash is present when no document is open.
    /// </summary>
    [Test]
    public async Task Layout_CenterLogoPresent()
    {
        await Task.Delay(3000);

        Query centerLogo = q => q.All().Marked("CenterLogo");
        App.WaitForElement(centerLogo, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("CenterLogoPresent");
    }

    /// <summary>
    /// Verifies the left splitter is functional for collapsing left sidebar.
    /// </summary>
    [Test]
    public async Task Layout_LeftSplitterPresent()
    {
        await Task.Delay(3000);

        Query leftSplitter = q => q.All().Marked("LeftSplitter");
        App.WaitForElement(leftSplitter, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("LeftSplitterPresent");
    }

    /// <summary>
    /// Verifies the right splitter is functional for collapsing right sidebar.
    /// </summary>
    [Test]
    public async Task Layout_RightSplitterPresent()
    {
        await Task.Delay(3000);

        Query rightSplitter = q => q.All().Marked("RightSplitter");
        App.WaitForElement(rightSplitter, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("RightSplitterPresent");
    }

    /// <summary>
    /// Verifies the bottom animation splitter is present.
    /// </summary>
    [Test]
    public async Task Layout_AnimationSplitterPresent()
    {
        await Task.Delay(3000);

        Query animationSplitter = q => q.All().Marked("AnimationSplitter");
        App.WaitForElement(animationSplitter, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("AnimationSplitterPresent");
    }
}
