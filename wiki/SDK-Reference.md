# Plugin SDK Reference

Complete API reference for the PixlPunkt Plugin SDK.

---

## Namespaces

| Namespace | Description |
|-----------|-------------|
| `PixlPunkt.PluginSdk` | Core plugin interfaces |
| `PixlPunkt.PluginSdk.Plugins` | Plugin registration |
| `PixlPunkt.PluginSdk.Tools` | Tool abstractions |
| `PixlPunkt.PluginSdk.Effects` | Effect system |
| `PixlPunkt.PluginSdk.IO` | Import/export handlers |
| `PixlPunkt.PluginSdk.Drawing` | Drawing utilities |
| `PixlPunkt.PluginSdk.Color` | Color manipulation |

---

## IPlugin Interface

The main plugin entry point.

```csharp
public interface IPlugin
{
    /// <summary>Unique plugin identifier (reverse domain notation)</summary>
    string Id { get; }
    
    /// <summary>Display name shown in UI</summary>
    string DisplayName { get; }
    
    /// <summary>Plugin version (semver recommended)</summary>
    string Version { get; }
    
    /// <summary>Plugin author</summary>
    string Author { get; }
    
    /// <summary>Plugin description</summary>
    string Description { get; }
    
    /// <summary>Called when plugin loads</summary>
    void Initialize(IPluginContext context);
    
    /// <summary>Return custom tools</summary>
    IEnumerable<IToolRegistration> GetToolRegistrations();
    
    /// <summary>Return custom effects</summary>
    IEnumerable<IEffectRegistration> GetEffectRegistrations();
    
    /// <summary>Return custom importers</summary>
    IEnumerable<IImportHandler> GetImportHandlers();
    
    /// <summary>Return custom exporters</summary>
    IEnumerable<IExportHandler> GetExportHandlers();
}
```

---

## IPluginContext

Provides access to PixlPunkt services.

```csharp
public interface IPluginContext
{
    /// <summary>Currently active document (null if none)</summary>
    IDocument? ActiveDocument { get; }
    
    /// <summary>Current selection</summary>
    ISelection Selection { get; }
    
    /// <summary>Current palette</summary>
    IPalette Palette { get; }
    
    /// <summary>Foreground color (BGRA)</summary>
    uint ForegroundColor { get; set; }
    
    /// <summary>Background color (BGRA)</summary>
    uint BackgroundColor { get; set; }
    
    /// <summary>UI services</summary>
    IUIServices UI { get; }
    
    /// <summary>Logging services</summary>
    ILogServices Log { get; }
    
    /// <summary>Settings storage</summary>
    IPluginSettings Settings { get; }
}
```

---

## Tool Registration

### ToolBuilders

```csharp
// Brush tool (paints pixels)
ToolBuilders.BrushTool(string id)
    .WithDisplayName(string name)
    .WithDescription(string desc)
    .WithIcon(byte[] pngData)
    .WithShortcut(string shortcut)
    .WithCategory(string category)
    .WithSettings(IBrushSettings settings)
    .WithPainter(Func<IBrushPainter> factory)
    .Build();

// Shape tool (draws shapes)
ToolBuilders.ShapeTool(string id)
    .WithDisplayName(string name)
    .WithShapeRenderer(Func<IShapeRenderer> factory)
    .Build();

// Selection tool
ToolBuilders.SelectionTool(string id)
    .WithDisplayName(string name)
    .WithSelector(Func<ISelector> factory)
    .Build();
```

### IBrushPainter

```csharp
public interface IBrushPainter
{
    /// <summary>Called when brush stroke starts</summary>
    void BeginStroke(IPaintContext context);
    
    /// <summary>Paint at position (called for each point)</summary>
    void Paint(IPaintContext context, int x, int y);
    
    /// <summary>Called when brush stroke ends</summary>
    void EndStroke(IPaintContext context);
}
```

### IPaintContext

```csharp
public interface IPaintContext
{
    /// <summary>Document width</summary>
    int Width { get; }
    
    /// <summary>Document height</summary>
    int Height { get; }
    
    /// <summary>Get pixel at position (BGRA)</summary>
    uint GetPixel(int x, int y);
    
    /// <summary>Set pixel at position (BGRA)</summary>
    void SetPixel(int x, int y, uint color);
    
    /// <summary>Current brush settings</summary>
    IBrushSettings Settings { get; }
    
    /// <summary>Current foreground color</summary>
    uint ForegroundColor { get; }
    
    /// <summary>Current background color</summary>
    uint BackgroundColor { get; }
    
    /// <summary>Is position within bounds?</summary>
    bool IsInBounds(int x, int y);
}
```

---

## Effect Registration

### EffectBuilders

```csharp
EffectBuilders.LayerEffect(string id)
    .WithDisplayName(string name)
    .WithDescription(string desc)
    .WithCategory(string category)  // "Stylize", "Filter", "Color"
    .WithSettings(IEffectSettings settings)
    .WithProcessor(Func<IEffectProcessor> factory)
    .WithPreviewSupport(bool enabled)
    .Build();
```

### IEffectProcessor

```csharp
public interface IEffectProcessor
{
    /// <summary>Process the effect</summary>
    void Process(IEffectContext context, IEffectSettings settings);
}
```

### IEffectContext

```csharp
public interface IEffectContext
{
    /// <summary>Source pixel buffer (read-only)</summary>
    ReadOnlySpan<byte> SourcePixels { get; }
    
    /// <summary>Destination pixel buffer (write)</summary>
    Span<byte> DestPixels { get; }
    
    /// <summary>Image width</summary>
    int Width { get; }
    
    /// <summary>Image height</summary>
    int Height { get; }
    
    /// <summary>Bytes per row (stride)</summary>
    int Stride { get; }
    
    /// <summary>Current animation frame (for animated effects)</summary>
    int Frame { get; }
    
    /// <summary>Total animation frames</summary>
    int TotalFrames { get; }
}
```

### Effect Settings Attributes

```csharp
public class MyEffectSettings : IEffectSettings
{
    [Setting("Intensity", Min = 0.0, Max = 1.0)]
    public double Intensity { get; set; } = 0.5;
    
    [Setting("Radius", Min = 1, Max = 100)]
    public int Radius { get; set; } = 5;
    
    [Setting("Color")]
    public uint Color { get; set; } = 0xFF000000;
    
    [Setting("Mode"), EnumSetting]
    public BlendMode Mode { get; set; } = BlendMode.Normal;
    
    [Setting("Animated"), Keyframeable]  // Can be animated
    public float AnimatedValue { get; set; }
}
```

---

## Import/Export Handlers

### IImportHandler

```csharp
public interface IImportHandler
{
    /// <summary>Format display name</summary>
    string DisplayName { get; }
    
    /// <summary>Supported file extensions</summary>
    string[] Extensions { get; }
    
    /// <summary>Check if file can be imported</summary>
    bool CanImport(string path);
    
    /// <summary>Import file and return document</summary>
    IImportResult Import(string path, IImportContext context);
}
```

### IExportHandler

```csharp
public interface IExportHandler
{
    /// <summary>Format display name</summary>
    string DisplayName { get; }
    
    /// <summary>File extension (without dot)</summary>
    string Extension { get; }
    
    /// <summary>Create default export settings</summary>
    IExportSettings CreateSettings();
    
    /// <summary>Show settings dialog (optional)</summary>
    bool ShowSettingsDialog(IExportSettings settings);
    
    /// <summary>Export document</summary>
    void Export(IExportContext context, IExportSettings settings);
}
```

### IExportContext

```csharp
public interface IExportContext
{
    /// <summary>Output file path</summary>
    string OutputPath { get; }
    
    /// <summary>Document width</summary>
    int Width { get; }
    
    /// <summary>Document height</summary>
    int Height { get; }
    
    /// <summary>Number of animation frames</summary>
    int FrameCount { get; }
    
    /// <summary>Get composite pixels for frame</summary>
    byte[] GetCompositePixels(int frame = 0);
    
    /// <summary>Get frame duration in milliseconds</summary>
    int GetFrameDuration(int frame);
}
```

---

## Color Utilities

### ColorUtils

```csharp
public static class ColorUtils
{
    /// <summary>Pack RGBA to uint (BGRA format)</summary>
    public static uint Pack(byte r, byte g, byte b, byte a = 255);
    
    /// <summary>Unpack uint to RGBA</summary>
    public static (byte r, byte g, byte b, byte a) Unpack(uint color);
    
    /// <summary>Convert HSL to RGB</summary>
    public static (byte r, byte g, byte b) HslToRgb(float h, float s, float l);
    
    /// <summary>Convert RGB to HSL</summary>
    public static (float h, float s, float l) RgbToHsl(byte r, byte g, byte b);
    
    /// <summary>Blend two colors</summary>
    public static uint Blend(uint src, uint dst, BlendMode mode);
    
    /// <summary>Interpolate between colors</summary>
    public static uint Lerp(uint a, uint b, float t);
}
```

### BlendMode Enum

```csharp
public enum BlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    Darken,
    Lighten,
    ColorDodge,
    ColorBurn,
    HardLight,
    SoftLight,
    Difference,
    Exclusion,
    Hue,
    Saturation,
    Color,
    Luminosity
}
```

---

## Drawing Utilities

### DrawingUtils

```csharp
public static class DrawingUtils
{
    /// <summary>Draw line using Bresenham's algorithm</summary>
    public static IEnumerable<(int x, int y)> Line(int x0, int y0, int x1, int y1);
    
    /// <summary>Draw circle outline</summary>
    public static IEnumerable<(int x, int y)> Circle(int cx, int cy, int radius);
    
    /// <summary>Fill circle</summary>
    public static IEnumerable<(int x, int y)> FilledCircle(int cx, int cy, int radius);
    
    /// <summary>Draw ellipse</summary>
    public static IEnumerable<(int x, int y)> Ellipse(int cx, int cy, int rx, int ry);
    
    /// <summary>Draw rectangle outline</summary>
    public static IEnumerable<(int x, int y)> Rectangle(int x, int y, int w, int h);
    
    /// <summary>Flood fill from point</summary>
    public static IEnumerable<(int x, int y)> FloodFill(
        Func<int, int, uint> getPixel,
        int x, int y, int width, int height,
        uint targetColor, int tolerance = 0);
}
```

---

## UI Services

### IUIServices

```csharp
public interface IUIServices
{
    /// <summary>Show message dialog</summary>
    void ShowMessage(string message, string title = "");
    
    /// <summary>Show confirmation dialog</summary>
    bool ShowConfirm(string message, string title = "");
    
    /// <summary>Show error dialog</summary>
    void ShowError(string message, string title = "Error");
    
    /// <summary>Show file open dialog</summary>
    string? ShowOpenFileDialog(string filter, string title = "Open");
    
    /// <summary>Show file save dialog</summary>
    string? ShowSaveFileDialog(string filter, string defaultName, string title = "Save");
    
    /// <summary>Show color picker</summary>
    uint? ShowColorPicker(uint initialColor);
    
    /// <summary>Show progress dialog</summary>
    IProgressDialog ShowProgress(string title);
}
```

---

## Logging

### ILogServices

```csharp
public interface ILogServices
{
    void Debug(string message, params object[] args);
    void Info(string message, params object[] args);
    void Warning(string message, params object[] args);
    void Error(string message, params object[] args);
    void Error(string message, Exception ex);
}
```

---

## Settings Storage

### IPluginSettings

```csharp
public interface IPluginSettings
{
    /// <summary>Get setting value</summary>
    T Get<T>(string key, T defaultValue = default);
    
    /// <summary>Set setting value</summary>
    void Set<T>(string key, T value);
    
    /// <summary>Check if setting exists</summary>
    bool Contains(string key);
    
    /// <summary>Remove setting</summary>
    void Remove(string key);
    
    /// <summary>Save settings to disk</summary>
    void Save();
}
```

---

## Assembly Attributes

```csharp
// Mark assembly as containing plugins
[assembly: PixlPunktPlugin]

// Enable hot reload during development
[assembly: PluginHotReload(true)]

// Specify minimum SDK version
[assembly: PluginSdkVersion("1.0.0")]
```

---

## See Also

- [[Plugin Development|Plugins]] - Getting started with plugins
- [Example Plugin Source](https://github.com/ChadRoesler/PixlPunkt/tree/main/PixlPunkt.ExamplePlugin)
- [NuGet Package](https://nuget.org/packages/PixlPunkt.PluginSdk)
