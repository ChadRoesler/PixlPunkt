# PixlPunkt.PluginSdk Developer Guide

[![NuGet](https://img.shields.io/nuget/v/PixlPunkt.PluginSdk.svg)](https://www.nuget.org/packages/PixlPunkt.PluginSdk)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Welcome to the **PixlPunkt.PluginSdk**! This SDK enables you to create powerful plugins for the PixlPunkt pixel art editor, including custom tools, effects, import/export handlers, and UI extensions.

---

## Getting Started

1. **Reference the SDK**: Add a reference to `PixlPunkt.PluginSdk` in your plugin project.
2. **Implement `IPlugin`**: Create a class that implements the `IPlugin` interface. This is your plugin entry point.
3. **Register Tools/Effects**: Use the provided builder APIs to register tools, effects, and import/export handlers.

---

## Installation

To install the SDK, run the following command in your terminal:

```bash
dotnet add package PixlPunkt.PluginSdk
```

Or via Package Manager Console in Visual Studio:

```powershell
Install-Package PixlPunkt.PluginSdk
```

---

## Quick Start

1. Create a new .NET class library:

   ```bash
   dotnet new classlib -n MyPixlPunktPlugin
   cd MyPixlPunktPlugin
   dotnet add package PixlPunkt.PluginSdk
   ```

2. Implement the `IPlugin` interface:

   ```csharp
   using PixlPunkt.PluginSdk.Plugins;
   using PixlPunkt.PluginSdk.Tools;
   using PixlPunkt.PluginSdk.Tools.Builders;

   public class MyPlugin : IPlugin
   {
       public string Id => "com.example.myplugin";
       public string DisplayName => "My Plugin";
       public string Version => "1.0.0";
       public string Author => "Your Name";
       public string Description => "A custom PixlPunkt plugin";

       public void Initialize(IPluginContext context)
       {
           context.Log(PluginLogLevel.Info, "Plugin initialized!");
       }

       public IEnumerable<IToolRegistration> GetToolRegistrations()
       {
           yield return ToolBuilders.BrushTool("com.example.brush.custom")
               .WithDisplayName("Custom Brush")
               .WithSettings(new MyBrushSettings())
               .WithPainter(() => new MyBrushPainter())
               .Build();
       }

       public IEnumerable<IEffectRegistration> GetEffectRegistrations() 
           => Enumerable.Empty<IEffectRegistration>();
       
       public IEnumerable<IImportRegistration> GetImportRegistrations() 
           => Enumerable.Empty<IImportRegistration>();
       
       public IEnumerable<IExportRegistration> GetExportRegistrations() 
           => Enumerable.Empty<IExportRegistration>();

       public void Shutdown() { }
   }
   ```

3. Build and deploy to `%AppData%\PixlPunkt\Plugins\`

---

## Key Concepts

### Plugin Entry Point
- Implement the `IPlugin` interface.
- Use `Initialize(IPluginContext context)` for setup.
- Register your tools/effects/importers/exporters in the respective `Get*Registrations` methods.

### Tool Registration
- Use the fluent builder API in `ToolBuilders`:
  - `BrushTool` for painting tools
  - `ShapeTool` for geometric tools
  - `SelectionTool` for selection tools
  - `UtilityTool` for viewport/state tools
- Example:
  ```csharp
  yield return ToolBuilders.BrushTool("myplugin.brush.sparkle")
      .WithDisplayName("Sparkle Brush")
      .WithSettings(new SparkleSettings())
      .WithPainter(() => new SparklePainter())
      .Build();
  ```

### Effect Registration
- Use `EffectBuilders.Effect` to register layer effects.
- Example:
  ```csharp
  yield return EffectBuilders.Effect("myplugin.effect.halftone")
      .WithDisplayName("Halftone")
      .WithFactory<HalftoneEffect>()
      .WithOptions<HalftoneEffect>(e => e.GetOptions())
      .Build();
  ```

### Import/Export Handlers
- Use `ImportBuilders` and `ExportBuilders` for custom file formats.
- Example:
  ```csharp
  yield return ImportBuilders.ForPalette("myplugin.import.txtpalette")
      .WithFormat(".txtpal", "Text Palette", "Simple text-based palette")
      .WithHandler(ctx => ImportTextPalette(ctx))
      .Build();
  ```

### UI Extensions
- Use `PluginWindowDescriptor` and `PluginWindowOption` to add custom plugin windows and toolbar buttons.
- Compose window content using the `IToolOption` system for consistent UI.

---

## Advanced Topics

- **Tool Settings**: Inherit from `ToolSettingsBase` for dynamic toolbar UI and change notification.
- **Custom Painters**: Inherit from `PainterBase` for stroke-based tools.
- **Shape Builders**: Implement `IShapeBuilder` for custom geometric tools.
- **Effect Settings**: Inherit from `EffectSettingsBase` for effect configuration.
- **Logging**: Use `IPluginContext.Log` for host-integrated logging.
- **Keyboard Shortcuts**: Use `KeyBinding` and `VirtualKey` for cross-platform shortcut support.

---

## API Reference (Quick Overview)

Standalone documentation for main APIs:

- [IPlugin](./docs/IPlugin.md)
- [ToolBuilders](./docs/ToolBuilders.md)
- [EffectBuilders](./docs/EffectBuilders.md)
- [IToolOption & ToolOptions](./docs/ToolOptions.md)
- [PluginWindowDescriptor & UI](./docs/PluginWindowDescriptor.md)
- [IPluginContext](./docs/IPluginContext.md)
- [IStrokePainter & PainterBase](./docs/IStrokePainter.md)
- [IShapeBuilder](./docs/IShapeBuilder.md)
- [ToolSettingsBase](./docs/ToolSettingsBase.md)
- [EffectSettingsBase](./docs/EffectSettingsBase.md)
- [Import/Export Builders](./docs/ImportExportBuilders.md)

---

## Best Practices

- Use your vendor prefix for all IDs (e.g., `com.mycompany.brush.sparkle`).
- Keep plugins stateless where possible; use the provided context for host interaction.
- Document your tools and effects for discoverability.
- Test your plugin with the latest PixlPunkt release.

---

## Resources

- [Example Plugin](../PixlPunkt.ExamplePlugin/)
- [PixlPunkt Main Repository](https://github.com/ChadRoesler/PixlPunkt)

---
