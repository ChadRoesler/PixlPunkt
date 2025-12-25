# PluginWindowDescriptor & UI

Declarative API for plugin-defined windows and toolbar buttons.

## Main Types
- `PluginWindowDescriptor` - Describes a window (title, content, size, events)
- `PluginWindowOption` - Toolbar button that opens a plugin window
- `WindowSection` - Group options in plugin windows
- `WindowLayoutDirection` - Layout enum (vertical/horizontal)

## Example
```csharp
new PluginWindowOption(
    "advancedSettings",
    "Advanced...",
    Icon.Settings,
    () => new PluginWindowDescriptor(
        Title: "Advanced Settings",
        GetContent: () => new IToolOption[]
        {
            new SliderOption("threshold", "Threshold", 0, 100, _threshold, v => _threshold = (int)v),
            new ToggleOption("preview", "Live Preview", _preview, v => _preview = v)
        }
    )
)
```

See also: `IToolOption` for composing window content.
