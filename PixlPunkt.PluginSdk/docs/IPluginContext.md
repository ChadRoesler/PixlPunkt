# IPluginContext

Provides plugins with access to host services and document state.

## Key Members
- `string HostVersion` - PixlPunkt version string
- `int ApiVersion` - Plugin API version
- `string PluginDirectory` - Directory where the plugin is installed
- `string DataDirectory` - Directory for plugin-specific persistent data
- `ICanvasContext? Canvas` - Access to the current document/canvas
- `void Log(PluginLogLevel level, string message)` - Log a message
- `void LogError(string message, Exception exception)` - Log an error/exception

## Example
```csharp
public void Initialize(IPluginContext context)
{
    context.Log(PluginLogLevel.Info, "Plugin initialized!");
    var version = context.HostVersion;
}
```
