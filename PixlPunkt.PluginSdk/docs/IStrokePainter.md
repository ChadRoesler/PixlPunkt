# IStrokePainter & PainterBase

For custom painting logic (brushes, erasers, etc). Implements the core painting strategy for stroke-based tools.

## Key Members (IStrokePainter)
- `bool NeedsSnapshot` - Whether a snapshot of the surface is needed
- `void Begin(PixelSurface surface, byte[]? snapshot)` - Called at stroke start
- `void StampAt(int cx, int cy, StrokeContext context)` - Paint at a point
- `void StampLine(int x0, int y0, int x1, int y1, StrokeContext context)` - Paint along a line
- `IRenderResult? End(string description = "Brush Stroke")` - Called at stroke end

## PainterBase
- Abstract base class implementing accumulation, line-drawing, and undo/redo support
- Override `StampAt` for your custom logic

## Example
```csharp
public class SparklePainter : PainterBase
{
    public override bool NeedsSnapshot => false;
    public override void StampAt(int cx, int cy, StrokeContext context)
    {
        // Custom painting logic here
    }
}
```
