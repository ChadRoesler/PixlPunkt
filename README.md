<p align="center">
  <img src="PixlPunkt/Assets/Icons/PixlPunkt.ico" alt="PixlPunkt Logo" width="128" height="128">
</p>

<h1 align="center">PixlPunkt</h1>

<p align="center">
  <strong>A modern pixel art editor for Windows</strong>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#documentation">Documentation</a> •
  <a href="#installation">Installation</a> •
  <a href="#plugin-development">Plugin Development</a> •
  <a href="#contributing">Contributing</a> •
  <a href="#license">License</a>
</p>

---

## Documentation

| Document | Description |
|----------|-------------|
| **[Quick Start Guide](docs/QUICK_START.md)** | Get up and running in 5 minutes |
| **[User Guide](docs/USER_GUIDE.md)** | Complete feature documentation |
| **[Plugin SDK](PixlPunkt.PluginSdk/DEVELOPER_README.md)** | Create your own plugins |

---

## Features

### Drawing Tools
- **Brush Tool** - Configurable size, shape, opacity, density, and pixel-perfect mode
- **Eraser** - Clean up your artwork with precision, supports opacity and custom brushes
- **Shape Tools** - Rectangle, ellipse tools with fill options, opacity, and density
- **Fill Tool** - Flood fill with tolerance and contiguous mode
- **Selection Tools** - Rectangle, lasso, magic wand, paint selection with transform support

### Gradient Fill Tool
Create stunning gradients with pixel-art-friendly dithering:
- **Gradient Types** - Linear, Radial, Angular, Diamond
- **Dithering Styles**:
  - Ordered: Bayer 2×2, 4×4, 8×8, Checker, Diagonal, Crosshatch
  - Error Diffusion: Floyd-Steinberg, Atkinson, **Riemersma** (Hilbert curve!)
  - Stochastic: Blue Noise
- **Multi-Color Gradients** - Custom gradients use your entire palette
- **Controls** - Adjustable strength and pattern scale

### Layer System
- Multiple layers with opacity and blend modes
- Layer folders for organization
- **Animatable layer effects** (drop shadow, outline, glow, scanlines, and more)
- Non-destructive editing

### Layer Effects
Apply non-destructive effects to any layer:
- **Stylize**: Drop Shadow, Outline, Glow/Bloom, Chromatic Aberration
- **Filter**: Scan Lines, Grain, Vignette, CRT, Pixelate
- **Color**: Color Adjust, Palette Quantize, ASCII Art
- **All effects are animatable!**

### Animation System
Full-featured animation with two modes:

**Canvas Animation** (Aseprite-style):
- Layer-based keyframes with pixel data
- Animated layer effects
- Onion skinning
- **Stage (Camera) System** with interpolated transforms
- **Audio reference tracks** with waveform display

**Tile Animation**:
- Frame sequences from tile coordinates
- Reels with custom timing
- Export to sprite sheets

### Tile System
- Create and manage tile sets
- Per-layer tile mappings
- Tile tessellation tools
- Import Tiled (.tmx/.tsx) files

### Color Management
- HSL color picker with ladder controls
- Palette presets (NES, GameBoy, CGA, PICO-8, and more)
- Custom palette import/export
- Color extraction from images
- Gradient generator

### Plugin System
- Extensible architecture via Plugin SDK
- Create custom brushes, shapes, effects, and tools
- Import/export handler support for custom file formats

### File Support
- Native `.pxp` format with full feature preservation
- **Import**: PNG, Aseprite, PyxelEdit, ICO, CUR, Tiled
- **Export**: PNG, GIF, MP4, AVI, WMV, BMP, JPEG, TIFF
- Custom brush export (`.pbx`)

---

## Installation

### Requirements
- Windows 10 version 1809 (build 17763) or later
- .NET 10 Runtime

### Download
Download the latest release from the [Releases](https://github.com/ChadRoesler/PixlPunkt/releases) page.

### Build from Source
```bash
git clone https://github.com/ChadRoesler/PixlPunkt.git
cd PixlPunkt
dotnet build
```

---

## Plugin Development

PixlPunkt supports plugins through the **PixlPunkt.PluginSdk** NuGet package.

### Quick Start

1. Create a new .NET class library project
2. Add the SDK reference:
   ```xml
   <PackageReference Include="PixlPunkt.PluginSdk" Version="1.0.0" />
   ```
3. Implement the `IPlugin` interface:
   ```csharp
   public class MyPlugin : IPlugin
   {
       public string Id => "com.example.myplugin";
       public string DisplayName => "My Plugin";
       public string Version => "1.0.0";
       public string Author => "Your Name";
       public string Description => "A custom plugin";

       public void Initialize(IPluginContext context) { }
       
       public IEnumerable<IToolRegistration> GetToolRegistrations()
       {
           yield return ToolBuilders.BrushTool("com.example.brush.custom")
               .WithDisplayName("Custom Brush")
               .WithSettings(new MyBrushSettings())
               .WithPainter(() => new MyBrushPainter())
               .Build();
       }
       
       // ... other registration methods
   }
   ```

4. Build and place the `.dll` in `%AppData%\PixlPunkt\Plugins\`

### Documentation
- [Plugin SDK Developer Guide](PixlPunkt.PluginSdk/DEVELOPER_README.md)
- [Example Plugin](PixlPunkt.ExamplePlugin/)

---

## Project Structure

```
PixlPunkt/
├── PixlPunkt/                 # Main application
│   ├── Core/                  # Core logic (document, imaging, tools, animation)
│   │   ├── Animation/         # Canvas & tile animation systems
│   │   ├── Painting/          # Brush painters, dithering algorithms
│   │   └── Tools/             # Tool implementations and settings
│   ├── UI/                    # WinUI 3 user interface
│   │   ├── Animation/         # Timeline and keyframe UI
│   │   ├── CanvasHost/        # Main canvas rendering
│   │   └── ColorPick/         # Color picker and gradient editor
│   └── Constants/             # Application constants
├── PixlPunkt.PluginSdk/       # Plugin SDK (NuGet package)
│   ├── Plugins/               # Plugin interfaces
│   ├── Tools/                 # Tool abstractions
│   ├── Effects/               # Effect system
│   └── IO/                    # Import/export handlers
└── PixlPunkt.ExamplePlugin/   # Example plugin implementation
```

---

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### Development Setup
1. Clone the repository
2. Open `PixlPunkt.sln` in Visual Studio 2022
3. Restore NuGet packages
4. Build and run

### Code Style
- Follow existing code conventions
- Use XML documentation for public APIs
- Write meaningful commit messages

---

## Roadmap

- [ ] SkiaSharp rendering backend
- [ ] Uno platform migration
- [ ] Symmetry tools
- [ ] Sprite sheet export improvements

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## Acknowledgments

- Built with [WinUI 3](https://docs.microsoft.com/en-us/windows/apps/winui/winui3/)
- Icons from [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons)
- Community Toolkit for WinUI

---

<p align="center">
  The PixlPunkt Team © 2025
</p>
