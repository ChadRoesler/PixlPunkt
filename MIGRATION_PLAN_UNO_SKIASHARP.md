# PixlPunkt Migration Plan: WinUI/Win2D ? Uno Platform/SkiaSharp

## Overview

This document outlines the complete migration strategy for converting PixlPunkt from a Windows-only WinUI 3 application with Win2D rendering to a cross-platform Uno Platform application with SkiaSharp rendering.

**Current Stack:**
- UI Framework: WinUI 3 (Windows App SDK)
- Rendering: Win2D (Microsoft.Graphics.Canvas)
- Target: Windows 10/11 only
- .NET Version: .NET 10

**Target Stack:**
- UI Framework: Uno Platform
- Rendering: SkiaSharp
- Targets: Windows, macOS, Linux, WebAssembly (WASM), optionally iOS/Android
- .NET Version: .NET 10

---

## Phase 1: Solution Restructuring

### Step 1.1: Create Uno Platform Project Structure

Create a new Uno Platform solution structure alongside the existing code:

```
PixlPunkt/
??? PixlPunkt.sln                      # Updated solution
??? PixlPunkt.Core/                    # NEW: Shared business logic (platform-agnostic)
?   ??? PixlPunkt.Core.csproj
?   ??? Document/
?   ??? Imaging/
?   ??? Effects/
?   ??? Tools/
?   ??? ...
??? PixlPunkt/                         # Main Uno Platform app
?   ??? PixlPunkt.csproj               # Converted to Uno.Sdk
?   ??? Platforms/
?   ?   ??? Desktop/                   # Windows/macOS/Linux
?   ?   ??? WebAssembly/               # Browser target
?   ?   ??? Mobile/                    # iOS/Android (optional)
?   ??? UI/
?   ??? ...
??? PixlPunkt.PluginSdk/               # Keep as-is (already platform-agnostic)
??? PixlPunkt.ExamplePlugin/
??? PixlPunkt.Tests/
```

### Step 1.2: Project File Changes

**New PixlPunkt.csproj (Uno.Sdk-based):**

```xml
<Project Sdk="Uno.Sdk">
  <PropertyGroup>
    <TargetFrameworks>
      net10.0-desktop;
      net10.0-browserwasm;
      net10.0-windows10.0.19041
    </TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UnoSingleProject>true</UnoSingleProject>
    <ApplicationTitle>PixlPunkt</ApplicationTitle>
    <ApplicationId>com.pixlpunkt.app</ApplicationId>
  </PropertyGroup>
</Project>
```

---

## Phase 2: Rendering Layer Abstraction

### Step 2.1: Create Rendering Abstractions

Define platform-agnostic rendering interfaces that can be implemented by either Win2D or SkiaSharp:

**ICanvasRenderer.cs:**
```csharp
public interface ICanvasRenderer
{
    void Clear(Color color);
    void DrawLine(float x1, float y1, float x2, float y2, Color color, float strokeWidth);
    void DrawRect(Rect rect, Color color, float strokeWidth);
    void FillRect(Rect rect, Color color);
    void DrawImage(ICanvasBitmap bitmap, Rect destRect, Rect srcRect, float opacity);
    void DrawText(string text, float x, float y, Color color, ITextFormat format);
    void PushClip(Rect clipRect);
    void PopClip();
    void PushTransform(Matrix3x2 transform);
    void PopTransform();
}
```

**ICanvasBitmap.cs:**
```csharp
public interface ICanvasBitmap : IDisposable
{
    int Width { get; }
    int Height { get; }
    byte[] GetPixels();
    void SetPixels(byte[] pixels);
}
```

### Step 2.2: Win2D to SkiaSharp Mapping

| Win2D Concept | SkiaSharp Equivalent |
|---------------|---------------------|
| `CanvasControl` | `SKXamlCanvas` or `SKCanvasView` |
| `CanvasDrawingSession` | `SKCanvas` |
| `CanvasBitmap` | `SKBitmap` |
| `CanvasRenderTarget` | `SKSurface` |
| `CanvasImageBrush` | `SKShader` |
| `CanvasTextFormat` | `SKPaint` + `SKFont` |
| `CanvasImageInterpolation` | `SKFilterMode` / `SKSamplingOptions` |
| `CanvasAntialiasing` | `SKPaint.IsAntialias` |
| `ICanvasEffect` | `SKImageFilter` / `SKColorFilter` |
| `Matrix3x2` | `SKMatrix` |

---

## Phase 3: SkiaSharp Implementation

### Step 3.1: PixelSurface Updates

Modify `PixelSurface.cs` to support SkiaSharp:

```csharp
public class PixelSurface
{
    public byte[] Pixels { get; }
    public int Width { get; }
    public int Height { get; }
    
    // NEW: SkiaSharp interop
    public SKBitmap ToSKBitmap()
    {
        var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var handle = bitmap.GetPixels();
        Marshal.Copy(Pixels, 0, handle, Pixels.Length);
        return bitmap;
    }
    
    public SKImage ToSKImage()
    {
        using var bitmap = ToSKBitmap();
        return SKImage.FromBitmap(bitmap);
    }
}
```

### Step 3.2: CanvasViewHost Conversion

Convert `CanvasViewHost` from Win2D to SkiaSharp:

**Before (Win2D):**
```csharp
private void CanvasView_Draw(CanvasControl sender, CanvasDrawEventArgs args)
{
    var ds = args.DrawingSession;
    ds.Clear(clearColor);
    ds.DrawImage(bmp, dest, src, 1.0f, CanvasImageInterpolation.NearestNeighbor);
}
```

**After (SkiaSharp):**
```csharp
private void SKCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
{
    var canvas = e.Surface.Canvas;
    canvas.Clear(clearColor.ToSKColor());
    
    using var paint = new SKPaint { FilterQuality = SKFilterQuality.None };
    canvas.DrawImage(skImage, destRect.ToSKRect(), paint);
}
```

### Step 3.3: Checkerboard Background Pattern

**Win2D (using CanvasImageBrush):**
```csharp
_stripeBrush = new CanvasImageBrush(device, rt)
{
    ExtendX = CanvasEdgeBehavior.Wrap,
    ExtendY = CanvasEdgeBehavior.Wrap
};
ds.FillRectangle(dest, _stripeBrush);
```

**SkiaSharp (using SKShader):**
```csharp
private SKShader CreateCheckerboardShader()
{
    using var surface = SKSurface.Create(new SKImageInfo(16, 16));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.White);
    using var paint = new SKPaint { Color = new SKColor(232, 232, 232) };
    canvas.DrawRect(0, 0, 8, 8, paint);
    canvas.DrawRect(8, 8, 8, 8, paint);
    
    using var image = surface.Snapshot();
    return image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
}
```

---

## Phase 4: Effects System Conversion

### Step 4.1: Blend Mode Mapping

| Win2D BlendMode | SkiaSharp SKBlendMode |
|-----------------|----------------------|
| Normal | `SKBlendMode.SrcOver` |
| Multiply | `SKBlendMode.Multiply` |
| Screen | `SKBlendMode.Screen` |
| Overlay | `SKBlendMode.Overlay` |
| Add | `SKBlendMode.Plus` |
| Subtract | Custom shader |
| Difference | `SKBlendMode.Difference` |

### Step 4.2: Layer Compositing

```csharp
public void CompositeLayer(SKCanvas canvas, SKImage layerImage, byte opacity, BlendMode blend)
{
    using var paint = new SKPaint
    {
        BlendMode = MapBlendMode(blend),
        Color = new SKColor(255, 255, 255, opacity)
    };
    canvas.DrawImage(layerImage, 0, 0, paint);
}
```

---

## Phase 5: XAML/UI Migration

### Step 5.1: Namespace Updates

| WinUI Namespace | Uno Equivalent |
|-----------------|----------------|
| `Microsoft.UI.Xaml` | `Microsoft.UI.Xaml` (same via Uno) |
| `Microsoft.UI.Xaml.Controls` | `Microsoft.UI.Xaml.Controls` |
| `Windows.UI` | `Windows.UI` |
| `Windows.Foundation` | `Windows.Foundation` |

### Step 5.2: Control Replacements

| WinUI Control | Uno/SkiaSharp Replacement |
|---------------|--------------------------|
| `CanvasControl` | `SKXamlCanvas` |
| `TabView` | Built-in or custom |
| Community Toolkit Controls | Uno Community Toolkit |

### Step 5.3: Platform-Specific Code

Use conditional compilation or partial classes:

```csharp
#if WINDOWS
    // Windows-specific code
#elif __WASM__
    // WebAssembly-specific code
#elif __DESKTOP__
    // Desktop (Skia) specific code
#endif
```

---

## Phase 6: File I/O and Native APIs

### Step 6.1: File Pickers

**WinUI:**
```csharp
var picker = new FileSavePicker();
picker.FileTypeChoices.Add("PNG", [".png"]);
var file = await picker.PickSaveFileAsync();
```

**Uno Platform:**
```csharp
// Use Uno.Extensions.Storage or native APIs per platform
var result = await FileSavePickerService.SaveAsync("image.png", stream);
```

### Step 6.2: Clipboard

```csharp
#if WINDOWS
    var dataPackage = new DataPackage();
    dataPackage.SetBitmap(/* ... */);
    Clipboard.SetContent(dataPackage);
#else
    // Use Uno's clipboard APIs or platform invoke
#endif
```

---

## Phase 7: Testing & Validation

### Step 7.1: Core Functionality Tests

1. ? Canvas renders correctly (checkerboard, layers)
2. ? Painting operations work (brush, fill, shapes)
3. ? Layer compositing with blend modes
4. ? Effects pipeline (blur, color adjust, etc.)
5. ? Selection tools and transformations
6. ? Animation timeline and playback
7. ? File save/load operations

### Step 7.2: Platform-Specific Tests

- **Windows**: Full feature parity
- **WebAssembly**: Core editing, limited file I/O
- **macOS/Linux**: Desktop experience via Skia

---

## Migration Order (Recommended)

1. **Week 1-2**: Solution restructuring, add Uno SDK
2. **Week 2-3**: Create rendering abstractions
3. **Week 3-5**: Port CanvasViewHost and core rendering
4. **Week 5-6**: Convert effects and compositing
5. **Week 6-7**: Update UI controls and XAML
6. **Week 7-8**: Platform-specific code and file I/O
7. **Week 8-10**: Testing, optimization, polish

---

## Key Dependencies to Add

```xml
<ItemGroup>
  <!-- SkiaSharp -->
  <PackageReference Include="SkiaSharp" Version="3.0.0-preview.x" />
  <PackageReference Include="SkiaSharp.Views.Uno.WinUI" Version="3.0.0-preview.x" />
  
  <!-- Uno Platform -->
  <PackageReference Include="Uno.WinUI" Version="5.x" />
  <PackageReference Include="Uno.Extensions.Storage" Version="5.x" />
  
  <!-- Community Toolkit (Uno-compatible) -->
  <PackageReference Include="CommunityToolkit.WinUI.UI.Controls" Version="x.x" />
</ItemGroup>
```

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Performance regression | Profile early, use SKPicture for caching |
| Missing Win2D effects | Implement via SkiaSharp shaders or custom code |
| Platform inconsistencies | Comprehensive cross-platform test suite |
| Community Toolkit gaps | Fork/adapt controls or build alternatives |

---

## Files to Modify Summary

### High Priority (Core Rendering)
- `PixlPunkt.csproj` - Convert to Uno.Sdk
- `CanvasViewHost.Rendering.cs` - Port Win2D ? SkiaSharp
- `CanvasViewHost.xaml` - Replace CanvasControl
- `PixelSurface.cs` - Add SkiaSharp interop
- `PatternBackgroundService.cs` - Convert to SKShader

### Medium Priority (Effects & Compositing)
- `Compositor.cs` - Update blend mode handling
- All `*Effect.cs` files - Port to SkiaSharp filters
- `BrushStrokeShapeRenderer.cs` - Convert drawing calls

### Lower Priority (UI & Platform)
- All `.xaml` files - Namespace updates, control swaps
- Dialog files - Platform-specific file picker handling
- `App.xaml.cs` - Platform initialization

---

## Next Steps

1. Start with **Step 1**: Create the Uno Platform project structure
2. Add SkiaSharp packages and verify basic rendering
3. Create the rendering abstraction interfaces
4. Begin porting `CanvasViewHost` incrementally

Ready to begin when you are! ??
