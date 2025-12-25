# IShapeBuilder

For custom geometric shape tools (rectangle, ellipse, star, etc).

## Key Members
- `string DisplayName` - Name of the shape
- `ApplyModifiers(...)` - Apply Shift/Ctrl modifiers to endpoints
- `BuildOutlinePoints(...)` - Get outline pixel set
- `BuildFilledPoints(...)` - Get filled pixel set

## Example
```csharp
public class StarShapeBuilder : ShapeBuilderBase
{
    public override string DisplayName => "Star";
    protected override HashSet<(int x, int y)> BuildOutlinePointsCore(int x0, int y0, int width, int height)
    {
        // Return outline points for a star
    }
    protected override HashSet<(int x, int y)> BuildFilledPointsCore(int x0, int y0, int width, int height)
    {
        // Return filled points for a star
    }
}
```
