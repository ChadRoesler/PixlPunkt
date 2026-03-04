# Voxel Tools

Voxel tools are an additive plugin surface for the in-tab voxel workspace. Existing plugins can keep using `IPlugin` only; add `IVoxelToolProvider` when you want to register voxel tools.

## Entry Point

- Implement `PixlPunkt.PluginSdk.Plugins.IVoxelToolProvider`.
- Return registrations from `GetVoxelToolRegistrations()`.

```csharp
public sealed class MyPlugin : IPlugin, IVoxelToolProvider
{
    public IEnumerable<IVoxelToolRegistration> GetVoxelToolRegistrations()
    {
        yield return VoxelToolBuilders.FaceTool("com.example.voxel.tint")
            .WithDisplayName("Voxel Tint")
            .WithSettings(new VoxelTintSettings())
            .WithHandler(ctx => new VoxelTintTool(ctx))
            .Build();
    }
}
```

## Builder API

Use `PixlPunkt.PluginSdk.Voxel.Tools.Builders.VoxelToolBuilders`:

- `FaceTool(string id)` - face-first tools (paint/dropper/erase-style).
- `EditTool(string id)` - voxel edit tools (create/delete/move/select-style).
- `UtilityTool(string id)` - utility/config tools.

All builders support:

- `WithDisplayName(string)`
- `WithSettings(ToolSettingsBase)`
- `WithBehavior(...)` (input pattern and host behavior hints)
- `WithHandler(Func<IVoxelToolContext, IVoxelToolHandler>)`
- `Build()`

## Runtime Contracts

- `IVoxelToolRegistration`: static metadata + factory.
- `IVoxelToolHandler`: pointer lifecycle (`PointerPressed`, `PointerMoved`, `PointerReleased`, `Cancel`).
- `IVoxelToolContext`: host services for:
  - face/voxel picking (`TryPickFace`, `TryPickVoxel`)
  - voxel edits (`SetFaceColor`, `SetVoxel`, `ClearVoxel`, `MoveSelection`)
  - selection edits (`SetSelection`, `ClearSelection`, `ExpandSelectionConnected`)
  - palette colors (`Foreground`, `Background`)
  - viewport and redraw (`ViewportState`, `RequestRedraw`, `RequestRebuildRenderCache`)
  - history (`BeginHistoryTransaction`, `CommitHistoryTransaction`, `CancelHistoryTransaction`)
  - lighting utility state (`LightingSettings`, `UpdateLightingSettings`)

## Best Practices

- Use vendor-prefixed IDs (`vendor.voxel.toolname`).
- Wrap write operations in history transactions for clean undo/redo units.
- Use `RequestRedraw()` after visual state changes that do not modify data.
- Keep tool handlers stateless where possible; put user-editable parameters in `ToolSettingsBase`.

## Reference Example

- `PixlPunkt.ExamplePlugin/Tools/Voxel/VoxelFaceTintTool.cs`
- `PixlPunkt.ExamplePlugin/ExamplePlugin.cs` (`GetVoxelToolRegistrations`)
