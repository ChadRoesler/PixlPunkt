# Import/Export Builders

Fluent APIs for registering custom import/export handlers for file formats.

## Main Types
- `ImportBuilders` - Static entry for import handler registration
- `ExportBuilders` - Static entry for export handler registration
- `ImportBuilder`, `ExportBuilder`, `PaletteImportBuilder`, `ImageImportBuilder`, `PaletteExportBuilder` - Fluent builder classes

## Example
```csharp
yield return ImportBuilders.ForPalette("myplugin.import.txtpalette")
    .WithFormat(".txtpal", "Text Palette", "Simple text-based palette")
    .WithHandler(ctx => ImportTextPalette(ctx))
    .Build();

yield return ExportBuilders.ForPalette("myplugin.export.txtpalette")
    .WithFormat(".txtpal", "Text Palette", "Simple text-based palette")
    .WithHandler((ctx, data) => ExportTextPalette(ctx, data))
    .Build();
```
