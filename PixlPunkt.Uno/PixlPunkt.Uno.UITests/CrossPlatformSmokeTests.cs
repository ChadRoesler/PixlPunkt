using FluentAssertions;

namespace PixlPunkt.Uno.UITests;

/// <summary>
/// Cross-platform smoke tests that adapt to the current test platform.
/// These tests verify basic functionality works across Windows, macOS, Linux, Android, iOS, and WASM.
/// </summary>
[TestFixture]
public class CrossPlatformSmokeTests : TestBase
{
    /// <summary>
    /// Basic smoke test that verifies the app launches and core UI is accessible.
    /// </summary>
    [Test]
    public async Task Smoke_AppLaunchesSuccessfully()
    {
        // Platform-appropriate startup delay
        await Task.Delay(PlatformTestConfig.StartupDelay);

        // Root grid must be present on all platforms
        Query rootGrid = q => q.All().Marked("Root");
        App.WaitForElement(rootGrid, timeout: PlatformTestConfig.ElementTimeout);

        TakeScreenshot($"AppLaunched_{GetPlatformName()}");
    }

    /// <summary>
    /// Verifies the main menu bar is accessible and functional.
    /// </summary>
    [Test]
    public async Task Smoke_MenuBarAccessible()
    {
        await Task.Delay(PlatformTestConfig.StartupDelay);

        Query menuBar = q => q.All().Marked("MainMenuBar");
        App.WaitForElement(menuBar, timeout: PlatformTestConfig.ElementTimeout);

        // Try to find menu items
        Query fileText = q => q.All().Text("File");
        var fileElements = App.Query(fileText);

        fileElements.Should().NotBeEmpty("File menu should be present");

        TakeScreenshot($"MenuBarAccessible_{GetPlatformName()}");
    }

    /// <summary>
    /// Verifies the tool panel is accessible.
    /// </summary>
    [Test]
    public async Task Smoke_ToolPanelAccessible()
    {
        await Task.Delay(PlatformTestConfig.StartupDelay);

        Query toolRail = q => q.All().Marked("ToolRail");
        App.WaitForElement(toolRail, timeout: PlatformTestConfig.ElementTimeout);

        TakeScreenshot($"ToolPanelAccessible_{GetPlatformName()}");
    }

    /// <summary>
    /// Verifies side panels are accessible.
    /// </summary>
    [Test]
    public async Task Smoke_SidePanelsAccessible()
    {
        await Task.Delay(PlatformTestConfig.StartupDelay);

        // Palette panel
        Query paletteCard = q => q.All().Marked("PaletteCard");
        App.WaitForElement(paletteCard, timeout: PlatformTestConfig.ElementTimeout);

        // Layers panel
        Query layersCard = q => q.All().Marked("LayersCard");
        App.WaitForElement(layersCard, timeout: PlatformTestConfig.ElementTimeout);

        TakeScreenshot($"SidePanelsAccessible_{GetPlatformName()}");
    }

    /// <summary>
    /// Verifies animation panel is present.
    /// </summary>
    [Test]
    public async Task Smoke_AnimationPanelPresent()
    {
        await Task.Delay(PlatformTestConfig.StartupDelay);

        Query animationPanel = q => q.All().Marked("AnimationPanel");
        App.WaitForElement(animationPanel, timeout: PlatformTestConfig.ElementTimeout);

        TakeScreenshot($"AnimationPanelPresent_{GetPlatformName()}");
    }

    /// <summary>
    /// Verifies basic menu interaction works.
    /// </summary>
    [Test]
    public async Task Smoke_MenuInteractionWorks()
    {
        await Task.Delay(PlatformTestConfig.StartupDelay);

        // Find and tap File menu
        Query fileMenu = q => q.All().Text("File");
        App.WaitForElement(fileMenu, timeout: PlatformTestConfig.ElementTimeout);
        App.Tap(fileMenu);

        await Task.Delay(PlatformTestConfig.AnimationDelay);

        // Verify menu opened by checking for a menu item
        Query newCanvasItem = q => q.All().Text("New Canvas…");
        var menuItems = App.Query(newCanvasItem);

        menuItems.Should().NotBeEmpty("File menu should open and show 'New Canvas…' item");

        TakeScreenshot($"MenuInteraction_{GetPlatformName()}");

        // Dismiss menu by tapping elsewhere
        Query root = q => q.All().Marked("Root");
        App.Tap(root);
    }

    /// <summary>
    /// Verifies the Help > About dialog can be accessed.
    /// </summary>
    [Test]
    public async Task Smoke_AboutDialogAccessible()
    {
        await Task.Delay(PlatformTestConfig.StartupDelay);

        // Open Help menu
        Query helpMenu = q => q.All().Text("Help");
        App.WaitForElement(helpMenu, timeout: PlatformTestConfig.ElementTimeout);
        App.Tap(helpMenu);

        await Task.Delay(PlatformTestConfig.AnimationDelay);

        // Find About item
        Query aboutItem = q => q.All().Text("About PixlPunkt");
        App.WaitForElement(aboutItem, timeout: PlatformTestConfig.ElementTimeout);

        TakeScreenshot($"AboutMenuItemVisible_{GetPlatformName()}");
    }

    /// <summary>
    /// Gets a friendly name for the current test platform.
    /// </summary>
    private static string GetPlatformName()
    {
        if (PlatformTestConfig.IsAndroid) return "Android";
        if (PlatformTestConfig.IsIOS) return "iOS";
        if (PlatformTestConfig.IsWindows) return "Windows";
        if (PlatformTestConfig.IsMacOS) return "macOS";
        if (PlatformTestConfig.IsLinux) return "Linux";
        if (PlatformTestConfig.IsWebAssembly) return "WASM";
        return "Unknown";
    }
}
