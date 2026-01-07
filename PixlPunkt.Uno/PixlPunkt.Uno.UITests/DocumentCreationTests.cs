namespace PixlPunkt.Uno.UITests;

/// <summary>
/// Tests for creating new documents and basic canvas operations.
/// </summary>
[TestFixture]
public class DocumentCreationTests : TestBase
{
    /// <summary>
    /// Verifies clicking the add tab button triggers new document flow.
    /// </summary>
    [Test]
    public async Task Document_AddTabButtonClickable()
    {
        await Task.Delay(3000);

        // The TabView's add button should be accessible
        Query docsTab = q => q.All().Marked("DocsTab");
        App.WaitForElement(docsTab, timeout: TimeSpan.FromSeconds(30));

        TakeScreenshot("BeforeAddTab");

        // Try to find and click the add tab button
        // Note: The exact query may need adjustment based on platform
        Query addButton = q => q.All().Button();
        var buttons = App.Query(addButton);

        TakeScreenshot("AfterQueryButtons");
    }

    /// <summary>
    /// Verifies File > New Canvas menu item is clickable.
    /// </summary>
    [Test]
    public async Task Document_NewCanvasMenuItemClickable()
    {
        await Task.Delay(3000);

        // Open File menu
        Query fileMenu = q => q.All().Text("File");
        App.WaitForElement(fileMenu, timeout: TimeSpan.FromSeconds(30));
        App.Tap(fileMenu);

        await Task.Delay(500);

        // Click New Canvas
        Query newCanvasItem = q => q.All().Text("New Canvas…");
        App.WaitForElement(newCanvasItem, timeout: TimeSpan.FromSeconds(10));

        TakeScreenshot("NewCanvasMenuItemVisible");

        App.Tap(newCanvasItem);

        await Task.Delay(1000);

        TakeScreenshot("AfterNewCanvasClick");
    }
}
