# PixlPunkt Plugin SDK

[![NuGet](https://img.shields.io/nuget/v/PixlPunkt.PluginSdk.svg)](https://www.nuget.org/packages/PixlPunkt.PluginSdk/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/ChadRoesler/PixlPunkt/blob/main/LICENSE)

The official SDK for developing plugins for [PixlPunkt](https://github.com/ChadRoesler/PixlPunkt), a modern pixel art editor for Windows.

## Features

- **Custom Tools** - Create new drawing tools with full brush and stroke support
- **Custom Effects** - Add image processing effects and filters
- **Import/Export Handlers** - Support new file formats for images and palettes
- **Keyboard Shortcuts** - Define shortcuts for your tools
- **Tool Options UI** - Declarative UI for tool settings (sliders, toggles, dropdowns, etc.)

## Installation

```bash
dotnet add package PixlPunkt.PluginSdk
```

Or via the NuGet Package Manager:
```
Install-Package PixlPunkt.PluginSdk
```

## Quick Start

### 1. Create a new Class Library project

```bash
dotnet new classlib -n MyPixlPunktPlugin
cd MyPixlPunktPlugin
dotnet add package PixlPunkt.PluginSdk
```

### 2. Enable plugin packaging

Add these properties to your `.csproj`:

```xml
<PropertyGroup>
  <PackAsPlugin>true</PackAsPlugin>
  <PluginId>com.yourname.myplugin</PluginId>
  <PluginDisplayName>My Awesome Plugin</PluginDisplayName>
  <PluginAuthor>Your Name</PluginAuthor>
  <PluginDescription>Does awesome things!</PluginDescription>
</PropertyGroup>
```

### 3. Create your plugin class

```csharp
using PixlPunkt.PluginSdk;

namespace MyPixlPunktPlugin;

public class MyPlugin : PixlPunktPlugin
{
    public override string Id => "com.yourname.myplugin";
    public override string Name => "My Awesome Plugin";
    public override string Author => "Your Name";
    public override Version Version => new(1, 0, 0);
    
    public override void Initialize(IPluginHost host)
    {
        // Register your tools, effects, importers, etc.
        host.RegisterTool(new MyCustomTool());
    }
}
```

### 4. Build and install

```bash
dotnet build
```

This creates a `.punk` file in your output directory. Copy it to:
```
%AppData%\PixlPunkt\Plugins\
```

## Creating a Custom Tool

```csharp
using PixlPunkt.PluginSdk.Tools;
using PixlPunkt.PluginSdk.Tools.Builders;

public class MyCustomTool : PluginToolBase
{
    public override string Id => "mytool";
    public override string DisplayName => "My Tool";
    public override string Description => "A custom drawing tool";
    
    // Optional: Define a keyboard shortcut
    public override KeyBinding? Shortcut => new(VirtualKey.K, Ctrl: true);
    
    public override void OnStrokeBegin(ToolStrokeContext ctx)
    {
        // Called when the user starts drawing
    }
    
    public override void OnStrokeMove(ToolStrokeContext ctx)
    {
        // Called as the user drags
        // Access pixels via ctx.Surface
    }
    
    public override void OnStrokeEnd(ToolStrokeContext ctx)
    {
        // Called when the user releases
    }
}
```

## Documentation

- **[Full Documentation](https://github.com/ChadRoesler/PixlPunkt/blob/main/docs/USER_GUIDE.md)** - Complete user guide
- **[Plugin Development Guide](https://github.com/ChadRoesler/PixlPunkt/blob/main/docs/PLUGIN_DEVELOPMENT.md)** - Detailed plugin development guide
- **[Example Plugin](https://github.com/ChadRoesler/PixlPunkt/tree/main/PixlPunkt.ExamplePlugin)** - Reference implementation
- **[API Reference](https://github.com/ChadRoesler/PixlPunkt/tree/main/PixlPunkt.PluginSdk)** - SDK source code

## Plugin Package Format (.punk)

When you build with `<PackAsPlugin>true</PackAsPlugin>`, the SDK automatically creates a `.punk` file containing:

- Your compiled plugin DLL
- A `manifest.json` with plugin metadata
- Any additional dependencies

The `.punk` format is simply a ZIP archive that PixlPunkt extracts and loads at startup.

## Requirements

- .NET 10.0 or later
- PixlPunkt 1.0.0 or later

## License

This SDK is licensed under the [MIT License](https://github.com/ChadRoesler/PixlPunkt/blob/main/LICENSE).

## Links

- [PixlPunkt on GitHub](https://github.com/ChadRoesler/PixlPunkt)
- [Report Issues](https://github.com/ChadRoesler/PixlPunkt/issues)
- [Changelog](https://github.com/ChadRoesler/PixlPunkt/blob/main/CHANGELOG.md)
