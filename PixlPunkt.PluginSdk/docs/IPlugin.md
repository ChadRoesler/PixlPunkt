# IPlugin

The main entry point for all PixlPunkt plugins. Implement this interface to register your tools, effects, importers, and exporters.

## Key Members
- `string Id` - Unique plugin identifier (e.g., `com.example.myplugin`).
- `string DisplayName` - Human-readable name for the plugin.
- `string Version` - Semantic version string.
- `string Author` - Author or organization.
- `string Description` - Short description for the plugin manager.
- `void Initialize(IPluginContext context)` - Called once after loading. Use for setup.
- `IEnumerable<IToolRegistration> GetToolRegistrations()` - Register tools.
- `IEnumerable<IEffectRegistration> GetEffectRegistrations()` - Register effects.
- `IEnumerable<IImportRegistration> GetImportRegistrations()` - Register import handlers.
- `IEnumerable<IExportRegistration> GetExportRegistrations()` - Register export handlers.
- `void Shutdown()` - Cleanup before unloading.

## Example
```csharp
public class MyPlugin : IPlugin
{
    public string Id => "com.example.myplugin";
    public string DisplayName => "My Plugin";
    public string Version => "1.0.0";
    public string Author => "Example Co.";
    public string Description => "Adds custom tools and effects.";
    public void Initialize(IPluginContext context) { /* ... */ }
    public IEnumerable<IToolRegistration> GetToolRegistrations() { /* ... */ }
    public IEnumerable<IEffectRegistration> GetEffectRegistrations() { /* ... */ }
    public IEnumerable<IImportRegistration> GetImportRegistrations() { /* ... */ }
    public IEnumerable<IExportRegistration> GetExportRegistrations() { /* ... */ }
    public void Shutdown() { /* ... */ }
}
```
