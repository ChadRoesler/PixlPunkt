# ToolSettingsBase

Base class for tool-specific settings with change notification and dynamic UI generation.

## Key Members
- `Icon Icon` - Tool icon for UI
- `string DisplayName` - Tool name
- `string Description` - Tool description
- `KeyBinding? Shortcut` - Keyboard shortcut
- `event Action? Changed` - Raised when a setting changes
- `IEnumerable<IToolOption> GetOptions()` - Returns toolbar options for this tool

## Example
```csharp
public class BrushToolSettings : ToolSettingsBase
{
    public override Icon Icon => Icon.Pen;
    public override string DisplayName => "Brush";
    public int Size { get; set; } = 8;
    public override IEnumerable<IToolOption> GetOptions()
    {
        yield return new SliderOption("size", "Size", 1, 128, Size, v => Size = (int)v);
    }
}
```
