# ToolBuilders

Fluent static API for registering tools of all categories (brush, shape, selection, utility).

## Main Methods
- `BrushTool(string id)` - Start a brush tool registration.
- `ShapeTool(string id)` - Start a shape tool registration.
- `SelectionTool(string id)` - Start a selection tool registration.
- `UtilityTool(string id)` - Start a utility tool registration.
- `TileTool(string id)` - Start a tile tool registration.

## Example
```csharp
yield return ToolBuilders.BrushTool("myplugin.brush.sparkle")
    .WithDisplayName("Sparkle Brush")
    .WithSettings(new SparkleSettings())
    .WithPainter(() => new SparklePainter())
    .Build();
```

See also: `BrushToolBuilder`, `ShapeToolBuilder`, `SelectionToolBuilder`, `UtilityToolBuilder`, `TileToolBuilder` for more options.
