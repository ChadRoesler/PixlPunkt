namespace PixlPunkt.Uno.UITests;

/// <summary>
/// Basic smoke tests for the main page.
/// </summary>
public class Given_MainPage : TestBase
{
    /// <summary>
    /// Basic smoke test to verify app launches and UI elements are accessible.
    /// </summary>
    [Test]
    public async Task When_SmokeTest()
    {
        // NOTICE
        // To run UITests, Run the target platform without debugger. 
        // For WASM: Note the port that is being used and update the Constants.cs file
        // in the UITests project with the correct port number.

        // Platform-appropriate delay for splash screen to complete
        await Task.Delay(PlatformTestConfig.StartupDelay);

        // Verify root element is present
        Query rootGrid = q => q.All().Marked("Root");
        App.WaitForElement(rootGrid, timeout: PlatformTestConfig.ElementTimeout);

        // Verify main menu bar is present
        Query menuBar = q => q.All().Marked("MainMenuBar");
        App.WaitForElement(menuBar, timeout: PlatformTestConfig.ElementTimeout);

        // Verify tool rail is present
        Query toolRail = q => q.All().Marked("ToolRail");
        App.WaitForElement(toolRail, timeout: PlatformTestConfig.ElementTimeout);

        // Take a screenshot after all elements are verified
        TakeScreenshot("SmokeTestComplete");
    }

    /// <summary>
    /// Verifies the initial layout loads correctly with all major panels.
    /// </summary>
    [Test]
    public async Task When_InitialLayoutLoads()
    {
        await Task.Delay(PlatformTestConfig.StartupDelay);

        // Verify all major layout elements
        Query root = q => q.All().Marked("Root");
        Query menuBar = q => q.All().Marked("MainMenuBar");
        Query toolRail = q => q.All().Marked("ToolRail");
        Query docsTab = q => q.All().Marked("DocsTab");
        Query animationPanel = q => q.All().Marked("AnimationPanel");

        App.WaitForElement(root, timeout: PlatformTestConfig.ElementTimeout);
        App.WaitForElement(menuBar, timeout: PlatformTestConfig.ElementTimeout);
        App.WaitForElement(toolRail, timeout: PlatformTestConfig.ElementTimeout);
        App.WaitForElement(docsTab, timeout: PlatformTestConfig.ElementTimeout);
        App.WaitForElement(animationPanel, timeout: PlatformTestConfig.ElementTimeout);

        TakeScreenshot("InitialLayoutComplete");
    }
}
