# IToolOption & ToolOptions

Describes a UI option for tool/effect settings. Used for dynamic toolbar and window UI.

## Overview

The `IToolOption` interface and its implementations provide a declarative way to describe settings and controls for tools and effects. These options are rendered as dynamic UI controls in the PixlPunkt toolbar or plugin windows.

### IToolOption Interface

```csharp
public interface IToolOption
{
    string Id { get; }           // Unique identifier for this option
    string Label { get; }        // Display label shown in the UI
    int Order { get; }           // Sort order within the toolbar
    string Group { get; }        // Optional grouping for organizing options
    string? Tooltip { get; }     // Optional tooltip text
}
```

All option types implement this interface.

## Main Option Types

- `SliderOption` - Numeric slider (int, double, byte)
    - Properties: `min`, `max`, `value`, `step`, `onChanged`, `showNumberBox`, `showLabel`
- `ToggleOption` - Checkbox
    - Properties: `value`, `onChanged`
- `DropdownOption` - ComboBox
    - Properties: `items`, `selectedIndex`, `onChanged`, `showLabel`
- `ButtonOption` - Button
    - Properties: `onClicked`, `icon` (optional)
- `ShapeOption` - Shape selector (for brush/shape tools)
- `LabelOption`, `DynamicLabelOption` - Read-only text (static or dynamic)
- `ColorOption`, `PaletteOption`, `ColorPickerWindowOption`, `GradientPickerWindowOption` - Color pickers and palette editors
- `SeparatorOption` - Visual divider between option groups
- `IconOption`, `IconButtonOption`, `IconToggleOption` - Icon-based controls (buttons, toggles)

## Option Type Details

### SliderOption
A numeric slider for int, double, or byte values.
```csharp
new SliderOption(
    id: "size",
    label: "Size",
    min: 1,
    max: 128,
    value: Size,
    onChanged: v => Size = (int)v
)
```
- Optional: `Step`, `ShowNumberBox`, `ShowLabel`, `Tooltip`, `Order`, `Group`

### ToggleOption
A checkbox for boolean values.
```csharp
new ToggleOption(
    id: "preview",
    label: "Live Preview",
    value: Preview,
    onChanged: v => Preview = v
)
```

### DropdownOption
A ComboBox for selecting from a list of items.
```csharp
new DropdownOption(
    id: "mode",
    label: "Mode",
    items: new[] { "Normal", "Multiply", "Screen" },
    selectedIndex: 0,
    onChanged: idx => Mode = idx
)
```

### ButtonOption
A button for triggering actions or opening dialogs.
```csharp
new ButtonOption(
    id: "apply",
    label: "Apply",
    icon: Icon.Checkmark,
    onClicked: () => ApplySettings()
)
```

### ColorOption
A color picker with inline swatch.
```csharp
new ColorOption(
    id: "color",
    label: "Color",
    color: 0xFF0000FF,
    onChanged: c => Color = c
)
```

### SeparatorOption
A visual divider between option groups.
```csharp
new SeparatorOption(order: 10)
```

## Example: Defining Tool Options

```csharp
public override IEnumerable<IToolOption> GetOptions()
{
    yield return new SliderOption("size", "Size", 1, 128, Size, v => Size = (int)v, step: 1, showNumberBox: true);
    yield return new ToggleOption("preview", "Live Preview", Preview, v => Preview = v);
    yield return new DropdownOption("mode", "Mode", new[] { "Normal", "Multiply" }, Mode, idx => Mode = idx);
    yield return new SeparatorOption(order: 10);
    yield return new ButtonOption("apply", "Apply", Icon.Checkmark, () => ApplySettings());
}
```

## See Also
- [`ToolSettingsBase`](./ToolSettingsBase.md) - For generating options from tool settings
- [`EffectSettingsBase`](./EffectSettingsBase.md) - For generating options from effect settings
- [`PluginWindowDescriptor`](./PluginWindowDescriptor.md) - For composing plugin window content
