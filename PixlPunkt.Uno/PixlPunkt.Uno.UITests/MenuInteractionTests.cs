namespace PixlPunkt.Uno.UITests;

/// <summary>
/// Tests for menu interactions across platforms.
/// Validates that menus open and contain expected items.
/// </summary>
[TestFixture]
public class MenuInteractionTests : TestBase
{
    /// <summary>
    /// Verifies the File menu can be opened.
    /// </summary>
    [Test]
    public async Task Menu_FileMenuOpens()
    {
        await Task.Delay(3000);

        // Find and tap the File menu
        Query fileMenu = q => q.All().Text("File");
        App.WaitForElement(fileMenu, timeout: TimeSpan.FromSeconds(30));
        App.Tap(fileMenu);

        await Task.Delay(500);

        // Verify menu item is visible
        Query newCanvasItem = q => q.All().Text("New Canvas…");
        App.WaitForElement(newCanvasItem, timeout: TimeSpan.FromSeconds(10));

        TakeScreenshot("FileMenuOpened");
    }

    /// <summary>
    /// Verifies the Edit menu can be opened.
    /// </summary>
    [Test]
    public async Task Menu_EditMenuOpens()
    {
        await Task.Delay(3000);

        Query editMenu = q => q.All().Text("Edit");
        App.WaitForElement(editMenu, timeout: TimeSpan.FromSeconds(30));
        App.Tap(editMenu);

        await Task.Delay(500);

        Query undoItem = q => q.All().Text("Undo");
        App.WaitForElement(undoItem, timeout: TimeSpan.FromSeconds(10));

        TakeScreenshot("EditMenuOpened");
    }

    /// <summary>
    /// Verifies the View menu can be opened.
    /// </summary>
    [Test]
    public async Task Menu_ViewMenuOpens()
    {
        await Task.Delay(3000);

        Query viewMenu = q => q.All().Text("View");
        App.WaitForElement(viewMenu, timeout: TimeSpan.FromSeconds(30));
        App.Tap(viewMenu);

        await Task.Delay(500);

        Query fitToScreenItem = q => q.All().Text("Fit to Screen (Ctrl+0)");
        App.WaitForElement(fitToScreenItem, timeout: TimeSpan.FromSeconds(10));

        TakeScreenshot("ViewMenuOpened");
    }

    /// <summary>
    /// Verifies the Palette menu can be opened.
    /// </summary>
    [Test]
    public async Task Menu_PaletteMenuOpens()
    {
        await Task.Delay(3000);

        Query paletteMenu = q => q.All().Text("Palette");
        App.WaitForElement(paletteMenu, timeout: TimeSpan.FromSeconds(30));
        App.Tap(paletteMenu);

        await Task.Delay(500);

        Query editFgItem = q => q.All().Text("Edit FG…");
        App.WaitForElement(editFgItem, timeout: TimeSpan.FromSeconds(10));

        TakeScreenshot("PaletteMenuOpened");
    }

    /// <summary>
    /// Verifies the Tiles menu can be opened.
    /// </summary>
    [Test]
    public async Task Menu_TilesMenuOpens()
    {
        await Task.Delay(3000);

        Query tilesMenu = q => q.All().Text("Tiles");
        App.WaitForElement(tilesMenu, timeout: TimeSpan.FromSeconds(30));
        App.Tap(tilesMenu);

        await Task.Delay(500);

        Query mergeDuplicatesItem = q => q.All().Text("Merge Duplicate Tiles");
        App.WaitForElement(mergeDuplicatesItem, timeout: TimeSpan.FromSeconds(10));

        TakeScreenshot("TilesMenuOpened");
    }

    /// <summary>
    /// Verifies the Settings menu can be opened.
    /// </summary>
    [Test]
    public async Task Menu_SettingsMenuOpens()
    {
        await Task.Delay(3000);

        Query settingsMenu = q => q.All().Text("Settings");
        App.WaitForElement(settingsMenu, timeout: TimeSpan.FromSeconds(30));
        App.Tap(settingsMenu);

        await Task.Delay(500);

        Query configureItem = q => q.All().Text("Configure…");
        App.WaitForElement(configureItem, timeout: TimeSpan.FromSeconds(10));

        TakeScreenshot("SettingsMenuOpened");
    }

    /// <summary>
    /// Verifies the Help menu can be opened.
    /// </summary>
    [Test]
    public async Task Menu_HelpMenuOpens()
    {
        await Task.Delay(3000);

        Query helpMenu = q => q.All().Text("Help");
        App.WaitForElement(helpMenu, timeout: TimeSpan.FromSeconds(30));
        App.Tap(helpMenu);

        await Task.Delay(500);

        Query aboutItem = q => q.All().Text("About PixlPunkt");
        App.WaitForElement(aboutItem, timeout: TimeSpan.FromSeconds(10));

        TakeScreenshot("HelpMenuOpened");
    }
}
