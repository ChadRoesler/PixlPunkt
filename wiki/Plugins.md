# Plugin Development

Create custom tools, effects, and features for PixlPunkt.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/plug_disconnected_16.png" width="16"> Overview

PixlPunkt's plugin system allows you to extend the editor with:

- **Custom Tools** - New brushes, shapes, effects
- **Layer Effects** - Image processing filters
- **Import/Export Handlers** - Support new file formats
- **Palette Operations** - Color manipulation

---

## Getting Started

### Prerequisites

- .NET 10 SDK
- Basic C# knowledge
- Visual Studio, VS Code, or Rider

### Install the SDK

```bash
dotnet new classlib -n MyPlugin
cd MyPlugin
dotnet add package PixlPunkt.PluginSdk
```

### Project Setup

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="PixlPunkt.PluginSdk" Version="1.*" />
  </ItemGroup>
</Project>
```

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/code_16.png" width="16"> Basic Plugin Structure

### Plugin Entry Point

```csharp
using PixlPunkt.PluginSdk;
using PixlPunkt.PluginSdk.Plugins;

public class MyPlugin : IPlugin
{
    public string Id => "com.myname.myplugin";
    public string DisplayName => "My Plugin";
    public string Version => "1.0.0";
    public string Author => "Your Name";
    public string Description => "My awesome plugin!";

    public void Initialize(IPluginContext context)
    {
        // Called when plugin loads
    }

    public IEnumerable<IToolRegistration> GetToolRegistrations()
    {
        // Return custom tools
        yield break;
    }

    public IEnumerable<IEffectRegistration> GetEffectRegistrations()
    {
        // Return custom effects
        yield break;
    }

    public IEnumerable<IImportHandler> GetImportHandlers()
    {
        // Return custom importers
        yield break;
    }

    public IEnumerable<IExportHandler> GetExportHandlers()
    {
        // Return custom exporters
        yield break;
    }
}
```

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> Creating Custom Tools

### Brush Tool Example

```csharp
public IEnumerable<IToolRegistration> GetToolRegistrations()
{
    yield return ToolBuilders.BrushTool("com.myname.rainbow-brush")
        .WithDisplayName("Rainbow Brush")
        .WithDescription("Paints with cycling rainbow colors")
        .WithIcon(LoadIcon("rainbow.png"))
        .WithSettings(new RainbowBrushSettings())
        .WithPainter(() => new RainbowBrushPainter())
        .Build();
}
```

### Brush Painter Implementation

```csharp
public class RainbowBrushPainter : IBrushPainter
{
    private int _hue = 0;

    public void Paint(IPaintContext context, int x, int y)
    {
        // Get color from hue
        var color = HslToRgb(_hue, 100, 50);
        
        // Paint pixel
        context.SetPixel(x, y, color);
        
        // Cycle hue
        _hue = (_hue + 5) % 360;
    }
}
```

### Brush Settings

```csharp
public class RainbowBrushSettings : IBrushSettings
{
    [Setting("Speed", Min = 1, Max = 20)]
    public int Speed { get; set; } = 5;
    
    [Setting("Saturation", Min = 0, Max = 100)]
    public int Saturation { get; set; } = 100;
}
```

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/star_16.png" width="16"> Creating Layer Effects

### Effect Registration

```csharp
public IEnumerable<IEffectRegistration> GetEffectRegistrations()
{
    yield return EffectBuilders.LayerEffect("com.myname.pixelate")
        .WithDisplayName("Pixelate")
        .WithCategory("Filter")
        .WithSettings(new PixelateSettings())
        .WithProcessor(() => new PixelateProcessor())
        .Build();
}
```

### Effect Processor

```csharp
public class PixelateProcessor : IEffectProcessor
{
    public void Process(IEffectContext context, IEffectSettings settings)
    {
        var pixelate = (PixelateSettings)settings;
        int size = pixelate.BlockSize;
        
        var source = context.SourcePixels;
        var dest = context.DestPixels;
        
        for (int y = 0; y < context.Height; y += size)
        {
            for (int x = 0; x < context.Width; x += size)
            {
                // Get average color of block
                var avg = GetAverageColor(source, x, y, size, size);
                
                // Fill block with average
                FillBlock(dest, x, y, size, size, avg);
            }
        }
    }
}
```

### Animatable Effects

```csharp
public class PulseSettings : IEffectSettings, IAnimatableSettings
{
    [Setting("Intensity"), Keyframeable]
    public float Intensity { get; set; } = 1.0f;
    
    [Setting("Speed")]
    public float Speed { get; set; } = 1.0f;
}
```

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/folder_16.png" width="16"> Import/Export Handlers

### Import Handler

```csharp
public class MyFormatImporter : IImportHandler
{
    public string DisplayName => "My Format";
    public string[] Extensions => new[] { ".myf" };
    
    public bool CanImport(string path)
    {
        // Check if file is valid
        return File.Exists(path) && 
               Path.GetExtension(path) == ".myf";
    }
    
    public IImportResult Import(string path, IImportContext context)
    {
        // Read file and create document
        var data = File.ReadAllBytes(path);
        
        // Parse your format...
        var width = ParseWidth(data);
        var height = ParseHeight(data);
        var pixels = ParsePixels(data);
        
        // Create result
        return context.CreateDocument(width, height)
            .WithLayer("Imported", pixels)
            .Build();
    }
}
```

### Export Handler

```csharp
public class MyFormatExporter : IExportHandler
{
    public string DisplayName => "My Format";
    public string Extension => ".myf";
    
    public IExportSettings CreateSettings() => new MyExportSettings();
    
    public void Export(IExportContext context, IExportSettings settings)
    {
        var mySettings = (MyExportSettings)settings;
        
        // Get pixels from context
        var pixels = context.GetCompositePixels();
        
        // Convert to your format...
        var data = ConvertToMyFormat(pixels, context.Width, context.Height);
        
        // Write file
        File.WriteAllBytes(context.OutputPath, data);
    }
}
```

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/wrench_16.png" width="16"> Plugin Context API

### Available Services

```csharp
public void Initialize(IPluginContext context)
{
    // Access document
    var doc = context.ActiveDocument;
    
    // Access selection
    var selection = context.Selection;
    
    // Access palette
    var palette = context.Palette;
    
    // Show dialogs
    context.UI.ShowMessage("Hello!");
    var result = context.UI.ShowConfirm("Continue?");
    
    // Logging
    context.Log.Info("Plugin initialized");
}
```

### Pixel Operations

```csharp
// Get pixel
uint color = context.GetPixel(x, y);

// Set pixel
context.SetPixel(x, y, 0xFF0000FF); // Red, BGRA format

// Batch operations
context.BeginUpdate();
// ... many pixel operations
context.EndUpdate();

// Color utilities
var (r, g, b, a) = ColorUtils.Unpack(color);
uint packed = ColorUtils.Pack(r, g, b, a);
```

---

## Building & Installing

### Build Plugin

```bash
dotnet build -c Release
```

### Install Plugin

Copy the DLL to:
- **Windows:** `%LocalAppData%\PixlPunkt\Plugins\`
- **macOS:** `~/Library/Application Support/PixlPunkt/Plugins/`
- **Linux:** `~/.local/share/PixlPunkt/Plugins/`

### Plugin with Dependencies

If your plugin has dependencies, create a folder:
```
Plugins/
??? MyPlugin/
    ??? MyPlugin.dll
    ??? SomeDependency.dll
```

---

## Debugging Plugins

### Debug Output

```csharp
context.Log.Debug("Variable value: {0}", myValue);
context.Log.Warning("Something unusual happened");
context.Log.Error("Something went wrong", exception);
```

### Attach Debugger

1. Set breakpoints in your plugin code
2. Launch PixlPunkt
3. Attach debugger to PixlPunkt process
4. Trigger your plugin code

### Hot Reload (Development)

Enable in development:
```csharp
#if DEBUG
[assembly: PluginHotReload(true)]
#endif
```

---

## Best Practices

### Performance

- Minimize allocations in paint loops
- Use `Span<T>` for pixel buffers when possible
- Batch pixel operations
- Cache computed values

### User Experience

- Provide meaningful names and descriptions
- Use appropriate default values
- Include tooltips for settings
- Handle errors gracefully

### Compatibility

- Target the PluginSdk version, not PixlPunkt directly
- Test with different document sizes
- Handle edge cases (empty selection, etc.)

---

## Example Plugins

### Included Examples

The `PixlPunkt.ExamplePlugin` project demonstrates:
- Custom brush tool
- Custom layer effect
- Settings with UI
- Import/export handlers

### Download Examples

See the [GitHub repository](https://github.com/ChadRoesler/PixlPunkt/tree/main/PixlPunkt.ExamplePlugin)

---

## See Also

- [[SDK Reference|SDK-Reference]] - Full API documentation
- [Example Plugin](https://github.com/ChadRoesler/PixlPunkt/tree/main/PixlPunkt.ExamplePlugin)
- [[File Formats|Formats]] - Supported formats
- [[Effects]] - Built-in effects
