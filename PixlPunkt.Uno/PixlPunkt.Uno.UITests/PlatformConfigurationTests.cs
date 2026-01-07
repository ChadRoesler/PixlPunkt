namespace PixlPunkt.Uno.UITests;

/// <summary>
/// Platform configuration tests to verify the app loads correctly on different platforms.
/// These tests validate core UI elements are present and accessible.
/// </summary>
[TestFixture]
public class PlatformConfigurationTests : TestBase
{
    /// <summary>
    /// Verifies the main window loads and the root grid is accessible.
    /// </summary>
    [Test]
    public async Task Platform_MainWindowLoads()
    {
        // Allow splash screen to complete
        await Task.Delay(3000);

        // Verify root element exists
        Query rootGrid = q => q.All().Marked("Root");
        App.WaitForElement(rootGrid, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("MainWindowLoaded");
    }

    /// <summary>
    /// Verifies the menu bar is present and accessible.
    /// </summary>
    [Test]
    public async Task Platform_MenuBarPresent()
    {
        await Task.Delay(3000);

        Query menuBar = q => q.All().Marked("MainMenuBar");
        App.WaitForElement(menuBar, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("MenuBarPresent");
    }

    /// <summary>
    /// Verifies the tool rail is present and accessible.
    /// </summary>
    [Test]
    public async Task Platform_ToolRailPresent()
    {
        await Task.Delay(3000);

        Query toolRail = q => q.All().Marked("ToolRail");
        App.WaitForElement(toolRail, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("ToolRailPresent");
    }

    /// <summary>
    /// Verifies the document tabs control is present.
    /// </summary>
    [Test]
    public async Task Platform_DocumentTabsPresent()
    {
        await Task.Delay(3000);

        Query docsTab = q => q.All().Marked("DocsTab");
        App.WaitForElement(docsTab, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("DocumentTabsPresent");
    }

    /// <summary>
    /// Verifies the right sidebar panels are present (Preview, Palette, Tiles, Layers, History).
    /// </summary>
    [Test]
    public async Task Platform_RightSidebarPanelsPresent()
    {
        await Task.Delay(3000);

        // Check each panel card
        Query previewCard = q => q.All().Marked("PreviewCard");
        Query paletteCard = q => q.All().Marked("PaletteCard");
        Query tilesCard = q => q.All().Marked("TilesCard");
        Query layersCard = q => q.All().Marked("LayersCard");
        Query historyCard = q => q.All().Marked("HistoryCard");

        App.WaitForElement(previewCard, timeout: TimeSpan.FromSeconds(30));
        App.WaitForElement(paletteCard, timeout: TimeSpan.FromSeconds(30));
        App.WaitForElement(tilesCard, timeout: TimeSpan.FromSeconds(30));
        App.WaitForElement(layersCard, timeout: TimeSpan.FromSeconds(30));
        App.WaitForElement(historyCard, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("RightSidebarPanelsPresent");
    }

    /// <summary>
    /// Verifies the animation panel is present.
    /// </summary>
    [Test]
    public async Task Platform_AnimationPanelPresent()
    {
        await Task.Delay(3000);

        Query animationPanel = q => q.All().Marked("AnimationPanel");
        App.WaitForElement(animationPanel, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("AnimationPanelPresent");
    }

    /// <summary>
    /// Verifies the tool options bar is present.
    /// </summary>
    [Test]
    public async Task Platform_ToolOptionsBarPresent()
    {
        await Task.Delay(3000);

        Query optionsBar = q => q.All().Marked("OptionsBar");
        App.WaitForElement(optionsBar, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("ToolOptionsBarPresent");
    }

    /// <summary>
    /// Verifies the palette panel is accessible and renders.
    /// </summary>
    [Test]
    public async Task Platform_PalettePanelAccessible()
    {
        await Task.Delay(3000);

        Query palettePanel = q => q.All().Marked("PalettePanel");
        App.WaitForElement(palettePanel, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("PalettePanelAccessible");
    }

    /// <summary>
    /// Verifies the layers panel is accessible and renders.
    /// </summary>
    [Test]
    public async Task Platform_LayersPanelAccessible()
    {
        await Task.Delay(3000);

        Query layersPanel = q => q.All().Marked("LayersPanel");
        App.WaitForElement(layersPanel, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("LayersPanelAccessible");
    }

    /// <summary>
    /// Verifies the tile panel is accessible and renders.
    /// </summary>
    [Test]
    public async Task Platform_TilePanelAccessible()
    {
        await Task.Delay(3000);

        Query tilePanel = q => q.All().Marked("TilePanel");
        App.WaitForElement(tilePanel, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("TilePanelAccessible");
    }

    /// <summary>
    /// Verifies the preview control is accessible and renders.
    /// </summary>
    [Test]
    public async Task Platform_PreviewControlAccessible()
    {
        await Task.Delay(3000);

        Query previewControl = q => q.All().Marked("PreviewControl");
        App.WaitForElement(previewControl, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("PreviewControlAccessible");
    }

    /// <summary>
    /// Verifies the history panel is accessible and renders.
    /// </summary>
    [Test]
    public async Task Platform_HistoryPanelAccessible()
    {
        await Task.Delay(3000);

        Query historyPanel = q => q.All().Marked("HistoryPanel");
        App.WaitForElement(historyPanel, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("HistoryPanelAccessible");
    }

    /// <summary>
    /// Verifies the collapsible splitters are present for layout flexibility.
    /// </summary>
    [Test]
    public async Task Platform_SplittersPresent()
    {
        await Task.Delay(3000);

        Query leftSplitter = q => q.All().Marked("LeftSplitter");
        Query rightSplitter = q => q.All().Marked("RightSplitter");
        Query animationSplitter = q => q.All().Marked("AnimationSplitter");

        App.WaitForElement(leftSplitter, timeout: TimeSpan.FromSeconds(30));
        App.WaitForElement(rightSplitter, timeout: TimeSpan.FromSeconds(30));
        App.WaitForElement(animationSplitter, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("SplittersPresent");
    }
}
