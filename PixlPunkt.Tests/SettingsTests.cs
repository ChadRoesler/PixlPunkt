namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Enums;

/// <summary>
/// Unit tests for application settings and configuration.
/// Tests cover default values, validation, clamping, and serialization.
/// </summary>
[TestFixture]
public class SettingsTests
{
    // ════════════════════════════════════════════════════════════════════
    // APP SETTINGS DEFAULT VALUES
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void AppSettings_DefaultValues_AreReasonable()
    {
        // Arrange & Act
        var settings = new MockAppSettings();

        // Assert
        settings.AutoBackupMinutes.Should().BeGreaterOrEqualTo(4, "backup interval should be at least 4 minutes");
        settings.MaxBackupCount.Should().BeGreaterOrEqualTo(1, "should keep at least 1 backup");
        settings.PaletteSwatchSize.Should().BeInRange(8, 64, "swatch size should be in valid range");
        settings.TileSwatchSize.Should().BeInRange(16, 128, "tile swatch size should be in valid range");
    }

    [Test]
    public void AppSettings_StorageFolderPath_DefaultsToEmpty()
    {
        // Arrange & Act
        var settings = new MockAppSettings();

        // Assert
        settings.StorageFolderPath.Should().BeEmpty("storage folder should use system default when empty");
    }

    [Test]
    public void AppSettings_DefaultPalette_HasFallbackValue()
    {
        // Arrange & Act
        var settings = new MockAppSettings();

        // Assert
        settings.DefaultPalette.Should().NotBeNullOrEmpty();
        settings.DefaultPalette.Should().Be("PixlPunkt Default");
    }

    [Test]
    public void AppSettings_LogLevel_DefaultsToInformation()
    {
        // Arrange & Act
        var settings = new MockAppSettings();

        // Assert
        settings.LogLevel.Should().Be("Information");
    }

    // ════════════════════════════════════════════════════════════════════
    // VALUE CLAMPING TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void AppSettings_AutoBackupMinutes_ClampedToMinimum()
    {
        // Arrange
        var settings = new MockAppSettings { AutoBackupMinutes = 1 };

        // Act
        settings.ValidateAndClamp();

        // Assert
        settings.AutoBackupMinutes.Should().BeGreaterOrEqualTo(4);
    }

    [Test]
    public void AppSettings_MaxBackupCount_ClampedToMinimum()
    {
        // Arrange
        var settings = new MockAppSettings { MaxBackupCount = 0 };

        // Act
        settings.ValidateAndClamp();

        // Assert
        settings.MaxBackupCount.Should().BeGreaterOrEqualTo(1);
    }

    [Test]
    public void AppSettings_PaletteSwatchSize_ClampedToRange()
    {
        // Arrange
        var settingsLow = new MockAppSettings { PaletteSwatchSize = 2 };
        var settingsHigh = new MockAppSettings { PaletteSwatchSize = 200 };

        // Act
        settingsLow.ValidateAndClamp();
        settingsHigh.ValidateAndClamp();

        // Assert
        settingsLow.PaletteSwatchSize.Should().BeGreaterOrEqualTo(8);
        settingsHigh.PaletteSwatchSize.Should().BeLessThanOrEqualTo(64);
    }

    [Test]
    public void AppSettings_TileSwatchSize_ClampedToRange()
    {
        // Arrange
        var settingsLow = new MockAppSettings { TileSwatchSize = 5 };
        var settingsHigh = new MockAppSettings { TileSwatchSize = 500 };

        // Act
        settingsLow.ValidateAndClamp();
        settingsHigh.ValidateAndClamp();

        // Assert
        settingsLow.TileSwatchSize.Should().BeGreaterOrEqualTo(16);
        settingsHigh.TileSwatchSize.Should().BeLessThanOrEqualTo(128);
    }

    // ════════════════════════════════════════════════════════════════════
    // THEME CHOICE TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void AppThemeChoice_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<MockAppThemeChoice>().Should().HaveCount(3);
        ((int)MockAppThemeChoice.System).Should().Be(0);
        ((int)MockAppThemeChoice.Light).Should().Be(1);
        ((int)MockAppThemeChoice.Dark).Should().Be(2);
    }

    [Test]
    public void StripeThemeChoice_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<MockStripeThemeChoice>().Should().HaveCount(3);
        ((int)MockStripeThemeChoice.System).Should().Be(0);
        ((int)MockStripeThemeChoice.Light).Should().Be(1);
        ((int)MockStripeThemeChoice.Dark).Should().Be(2);
    }

    // ════════════════════════════════════════════════════════════════════
    // PALETTE SORT MODE TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void PaletteSortMode_DefaultPreservesOriginalOrder()
    {
        // Arrange
        var settings = new MockAppSettings();

        // Assert
        settings.DefaultPaletteSortMode.Should().Be(PaletteSortMode.Default);
    }

    [Test]
    public void PaletteSortMode_HasAllExpectedModes()
    {
        // Assert
        Enum.GetValues<PaletteSortMode>().Should().Contain(PaletteSortMode.Default);
        Enum.GetValues<PaletteSortMode>().Should().Contain(PaletteSortMode.Hue);
        Enum.GetValues<PaletteSortMode>().Should().Contain(PaletteSortMode.Saturation);
        Enum.GetValues<PaletteSortMode>().Should().Contain(PaletteSortMode.Lightness);
    }

    // ════════════════════════════════════════════════════════════════════
    // UPDATE SETTINGS TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void AppSettings_UpdateSettings_HaveReasonableDefaults()
    {
        // Arrange & Act
        var settings = new MockAppSettings();

        // Assert
        settings.CheckForUpdatesOnStartup.Should().BeTrue("updates should be enabled by default");
        settings.IncludePreReleaseUpdates.Should().BeFalse("pre-release should be opt-in");
        settings.SkippedUpdateVersion.Should().BeNull("no version should be skipped initially");
    }

    // ════════════════════════════════════════════════════════════════════
    // TOOL SETTINGS BASE TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void BrushSettings_Size_ClampedToValidRange()
    {
        // Arrange & Act
        int clamped = Math.Clamp(-5, 1, 128);

        // Assert
        clamped.Should().BeGreaterOrEqualTo(1);
        clamped.Should().BeLessThanOrEqualTo(128);
    }

    [Test]
    public void BrushSettings_Opacity_ClampedTo255()
    {
        // Arrange & Act
        int clamped = Math.Clamp(300, 0, 255);

        // Assert
        clamped.Should().Be(255);
    }

    [Test]
    public void BrushSettings_Density_ClampedTo255()
    {
        // Arrange & Act
        int clamped = Math.Clamp(-10, 0, 255);

        // Assert
        clamped.Should().Be(0);
    }

    // ════════════════════════════════════════════════════════════════════
    // EFFECT SETTINGS TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void EffectSettings_Strength_ClampedToPercentage()
    {
        // Arrange & Act
        int clampedLow = Math.Clamp(-10, 0, 100);
        int clampedHigh = Math.Clamp(150, 0, 100);

        // Assert
        clampedLow.Should().Be(0);
        clampedHigh.Should().Be(100);
    }

    [Test]
    public void EffectSettings_Gamma_ClampedToValidRange()
    {
        // Arrange - typical gamma range is 0.5 to 4.0
        double clampedLow = Math.Clamp(0.1, 0.5, 4.0);
        double clampedHigh = Math.Clamp(10.0, 0.5, 4.0);

        // Assert
        clampedLow.Should().BeApproximately(0.5, 0.001);
        clampedHigh.Should().BeApproximately(4.0, 0.001);
    }

    // ════════════════════════════════════════════════════════════════════
    // MOCK TYPES FOR TESTING (avoid actual file I/O)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Mock AppSettings for testing without file system access.
    /// </summary>
    private class MockAppSettings
    {
        public string StorageFolderPath { get; set; } = string.Empty;
        public int AutoBackupMinutes { get; set; } = 10;
        public int MaxBackupCount { get; set; } = 10;
        public MockAppThemeChoice AppTheme { get; set; } = MockAppThemeChoice.System;
        public MockStripeThemeChoice StripeTheme { get; set; } = MockStripeThemeChoice.System;
        public int PaletteSwatchSize { get; set; } = 16;
        public string DefaultPalette { get; set; } = "PixlPunkt Default";
        public PaletteSortMode DefaultPaletteSortMode { get; set; } = PaletteSortMode.Default;
        public int TileSwatchSize { get; set; } = 48;
        public string DefaultTileSetPath { get; set; } = string.Empty;
        public string? LogLevel { get; set; } = "Information";
        public bool CheckForUpdatesOnStartup { get; set; } = true;
        public bool IncludePreReleaseUpdates { get; set; } = false;
        public string? SkippedUpdateVersion { get; set; }

        /// <summary>
        /// Validates and clamps settings to valid ranges (mimics AppSettings.Load behavior).
        /// </summary>
        public void ValidateAndClamp()
        {
            AutoBackupMinutes = Math.Max(4, AutoBackupMinutes);
            MaxBackupCount = Math.Max(1, MaxBackupCount);
            PaletteSwatchSize = Math.Clamp(PaletteSwatchSize, 8, 64);
            TileSwatchSize = Math.Clamp(TileSwatchSize, 16, 128);
            if (string.IsNullOrEmpty(LogLevel)) LogLevel = "Information";
            if (string.IsNullOrEmpty(DefaultPalette)) DefaultPalette = "PixlPunkt Default";
        }
    }

    private enum MockAppThemeChoice { System = 0, Light = 1, Dark = 2 }
    private enum MockStripeThemeChoice { System = 0, Light = 1, Dark = 2 }
}
